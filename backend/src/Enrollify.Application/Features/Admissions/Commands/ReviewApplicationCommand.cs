using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Admissions;
using Enrollify.Application.Features.Students;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Admissions.Commands;

public record ReviewApplicationCommand(Guid ApplicationId, bool IsApproved, string? Notes) : IRequest<ApplicationDetailDto>;

public class ReviewApplicationCommandHandler : IRequestHandler<ReviewApplicationCommand, ApplicationDetailDto>
{
    private const string DefaultPassword = "ChangeMe123!";

    private readonly IApplicationDbContext _context;
    private readonly IEmailSender _emailSender;

    public ReviewApplicationCommandHandler(IApplicationDbContext context, IEmailSender emailSender)
    {
        _context = context;
        _emailSender = emailSender;
    }

    public async Task<ApplicationDetailDto> Handle(ReviewApplicationCommand request, CancellationToken cancellationToken)
    {
        var app = await _context.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == request.ApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Application not found.");

        if (app.Status != "Submitted" && app.Status != "UnderReview")
            throw new InvalidOperationException($"Cannot review application in '{app.Status}' status.");

        // Email of a user account newly created by this approval (null when an existing account was reused).
        string? newAccountEmail = null;

        // The whole review (status flip, user/student/enrollment creation) commits atomically:
        // a single SaveChangesAsync inside one transaction. A failure anywhere leaves the
        // application Submitted with no orphaned Approved+User+no-Student state, so the
        // registrar can simply retry.
        await _context.ExecuteInTransactionAsync(async ct =>
        {
            app.ReviewedAt = DateTime.UtcNow;
            app.ReviewNotes = request.Notes;

            Student? student = null;

            if (request.IsApproved)
            {
                app.Status = "Approved";

                Guid? studentSelfUserId = null;
                Guid? parentUserId = app.ParentUserId; // Already set if an authenticated parent submitted

                if (app.ApplicationType == "Parent")
                {
                    if (parentUserId == null)
                    {
                        (parentUserId, newAccountEmail) = await ResolveOrCreateParentUserAsync(app, ct);
                    }
                }
                else // "Student"
                {
                    (studentSelfUserId, newAccountEmail) = await CreateStudentUserAsync(app, ct);
                }

                // Approval transcribes data that was already validated at SUBMISSION time against
                // the tenant's application-form configuration — it must never re-validate.
                // Dispatching CreateStudentCommand here would run its FluentValidation rules
                // (e.g. Address NotEmpty) and reject applications whose form never required
                // those fields, so the Student is constructed directly instead.
                student = new Student
                {
                    LRN = await StudentNumbering.NextLrnAsync(_context, ct),
                    FirstName = app.FirstName,
                    MiddleName = app.MiddleName ?? string.Empty,
                    LastName = app.LastName,
                    BirthDate = app.DateOfBirth,
                    Gender = app.Gender,
                    Address = app.Address ?? string.Empty,
                    ContactNumber = app.ContactNumber,
                    Email = app.Email,
                    GuardianName = app.GuardianName,
                    GuardianContact = app.GuardianContact,
                    UserId = studentSelfUserId,
                    ParentUserId = parentUserId,
                    TenantId = app.TenantId
                };
                _context.Students.Add(student);

                app.StudentId = student.Id;
                app.ParentUserId = parentUserId;

                var enrollment = new Enrollment
                {
                    StudentId = student.Id,
                    SchoolYear = app.SchoolYear,
                    GradeLevel = app.GradeLevel,
                    Status = EnrollmentStatus.Draft,
                    Remarks = "Auto-created on application approval",
                    TenantId = app.TenantId
                };
                _context.Enrollments.Add(enrollment);

                var templates = await _context.RequirementTemplates
                    .Where(t => t.IsActive && (t.GradeLevel == null || t.GradeLevel == app.GradeLevel))
                    .OrderBy(t => t.DisplayOrder).ThenBy(t => t.DocumentName)
                    .ToListAsync(ct);

                foreach (var template in templates)
                {
                    _context.EnrollmentRequirements.Add(new EnrollmentRequirement
                    {
                        EnrollmentId = enrollment.Id,
                        DocumentName = template.DocumentName,
                        TenantId = app.TenantId
                    });
                }
            }
            else
            {
                app.Status = "Rejected";
            }

            // Single save for the whole review. Two concurrent approvals can mint the same
            // auto-generated LRN; the (TenantId, LRN) unique index rejects the loser —
            // regenerate and retry a couple of times before giving up.
            const int maxAttempts = 3;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await _context.SaveChangesAsync(ct);
                    break;
                }
                catch (DbUpdateException) when (student != null && attempt < maxAttempts)
                {
                    student.LRN = await StudentNumbering.NextLrnAsync(_context, ct);
                }
            }
        }, cancellationToken);

        // Best-effort notification AFTER the transaction has committed; IEmailSender never throws.
        await SendReviewOutcomeEmailAsync(app, newAccountEmail, cancellationToken);

        Dictionary<string, string?>? custom = null;
        if (!string.IsNullOrWhiteSpace(app.CustomFieldValues))
        {
            try { custom = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(app.CustomFieldValues); }
            catch { /* ignore malformed JSON */ }
        }

        return new ApplicationDetailDto(
            app.Id, app.ApplicationNumber, app.FirstName, app.MiddleName, app.LastName,
            app.Email, app.ContactNumber, app.Gender, app.DateOfBirth, app.Address,
            app.GradeLevel, app.SchoolYear, app.PreviousSchool, app.PreviousSchoolAddress,
            app.GuardianName, app.GuardianContact, app.GuardianRelationship,
            app.Status, app.CreatedAt, app.ReviewedAt, app.ReviewNotes, app.StudentId,
            app.ApplicationType, app.ParentFirstName, app.ParentLastName, app.ParentEmail, app.ParentContactNumber,
            custom);
    }

    /// <returns>The parent user id, plus the account email when a NEW account was created (null when reused).</returns>
    private async Task<(Guid ParentUserId, string? NewAccountEmail)> ResolveOrCreateParentUserAsync(AdmissionApplication app, CancellationToken ct)
    {
        var email = (app.ParentEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
            throw new InvalidOperationException("Parent application is missing parent email — cannot create parent account.");

        var existing = await _context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (existing != null)
        {
            if (existing.Role != UserRole.Parent)
                throw new InvalidOperationException("The parent email is already in use by a non-parent account.");
            if (existing.TenantId != app.TenantId)
                throw new InvalidOperationException("The parent email is registered with another school.");
            return (existing.Id, null);
        }

        var parent = new User
        {
            TenantId = app.TenantId,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            FirstName = app.ParentFirstName ?? string.Empty,
            LastName = app.ParentLastName ?? string.Empty,
            Role = UserRole.Parent,
            IsActive = true
        };
        _context.Users.Add(parent);
        return (parent.Id, email);
    }

    private async Task SendReviewOutcomeEmailAsync(AdmissionApplication app, string? newAccountEmail, CancellationToken ct)
    {
        var recipient = app.ApplicationType == "Parent" && !string.IsNullOrWhiteSpace(app.ParentEmail)
            ? app.ParentEmail
            : app.Email;

        if (string.IsNullOrWhiteSpace(recipient))
            return; // No recipient email on the application — skip.

        if (app.Status == "Approved")
        {
            var body = $"Good news! Application {app.ApplicationNumber} for {app.FirstName} {app.LastName} " +
                       $"({app.GradeLevel}, SY {app.SchoolYear}) has been approved.";
            if (!string.IsNullOrWhiteSpace(app.ReviewNotes))
                body += $"\n\nNotes from the school: {app.ReviewNotes}";
            if (!string.IsNullOrEmpty(newAccountEmail))
                body += $"\n\nAn account was created for {newAccountEmail}. " +
                        $"Sign in with the temporary password {DefaultPassword} and change it after your first login.";

            await _emailSender.SendAsync(recipient, $"Application {app.ApplicationNumber} approved", body, ct);
        }
        else
        {
            var body = $"Application {app.ApplicationNumber} for {app.FirstName} {app.LastName} " +
                       $"({app.GradeLevel}, SY {app.SchoolYear}) has been updated to status: {app.Status}.";
            if (!string.IsNullOrWhiteSpace(app.ReviewNotes))
                body += $"\n\nReview notes: {app.ReviewNotes}";

            await _emailSender.SendAsync(recipient, $"Application {app.ApplicationNumber} status update", body, ct);
        }
    }

    /// <returns>The new student user id and the account email (a student account is always newly created here).</returns>
    private async Task<(Guid StudentUserId, string NewAccountEmail)> CreateStudentUserAsync(AdmissionApplication app, CancellationToken ct)
    {
        var email = (app.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
            throw new InvalidOperationException("Student application is missing email — cannot create student account.");

        var existing = await _context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (existing != null)
            throw new InvalidOperationException("A user with this email already exists. Cannot approve application.");

        var user = new User
        {
            TenantId = app.TenantId,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            FirstName = app.FirstName,
            LastName = app.LastName,
            Role = UserRole.Student,
            IsActive = true
        };
        _context.Users.Add(user);
        return (user.Id, email);
    }
}
