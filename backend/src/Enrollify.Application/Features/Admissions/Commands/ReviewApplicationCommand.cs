using Enrollify.Application.Features.Enrollments;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Admissions;
using Enrollify.Application.Features.Students;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Admissions.Commands;

public record ReviewApplicationCommand(Guid ApplicationId, bool IsApproved, string? Notes, string? ReviewedBy = null) : IRequest<ApplicationDetailDto>;

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
            // The decider. (Mark-under-review stamps whoever picked it up; the decision overwrites it.)
            if (request.ReviewedBy != null) app.ReviewedBy = request.ReviewedBy;
            app.ReviewNotes = request.Notes;

            Student? student = null;
            Student? createdStudent = null; // null when an existing student was matched and reused

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

                // Dedup: a returning family re-applying must not mint a second student record.
                // Match an ACTIVE student by the application's LRN, else by name + date of birth.
                var existingStudent = await FindMatchingActiveStudentAsync(app, ct);
                if (existingStudent != null)
                {
                    var alreadyEnrolled = await _context.Enrollments.AnyAsync(
                        e => e.StudentId == existingStudent.Id
                             && e.SchoolYear == app.SchoolYear
                             && e.Status != EnrollmentStatus.Cancelled, ct);
                    if (alreadyEnrolled)
                        throw new InvalidOperationException(
                            $"This applicant matches existing student {existingStudent.StudentNumber}, who is already enrolled for {app.SchoolYear}; " +
                            "reject this application or cancel the existing enrollment first.");

                    student = existingStudent; // reuse — no new Student row, no new student number
                    // Attach the accounts this approval created/resolved, but never overwrite links.
                    if (studentSelfUserId != null && student.UserId == null)
                        student.UserId = studentSelfUserId;
                    if (parentUserId != null && student.ParentUserId == null)
                        student.ParentUserId = parentUserId;

                    // New DepEd details fill blanks only - never overwrite the registrar's record.
                    LearnerProfileMapping.FillBlanks(app, existingStudent);
                    var appLrn = LearnerProfileMapping.Clean(app.Lrn);
                    if (existingStudent.LRN == null && appLrn != null
                        && !await _context.Students.AnyAsync(s => s.Id != existingStudent.Id && s.LRN == appLrn, ct))
                        existingStudent.LRN = appLrn;
                }
                else
                {
                    // Approval transcribes data that was already validated at SUBMISSION time against
                    // the tenant's application-form configuration — it must never re-validate.
                    // Dispatching CreateStudentCommand here would run its FluentValidation rules
                    // (e.g. Address NotEmpty) and reject applications whose form never required
                    // those fields, so the Student is constructed directly instead.
                    var newLrn = LearnerProfileMapping.Clean(app.Lrn);
                    if (newLrn != null)
                    {
                        // Dedup only matches ACTIVE students; an inactive holder of this LRN would
                        // otherwise surface as a unique-index failure at save.
                        var holder = await _context.Students.FirstOrDefaultAsync(s => s.LRN == newLrn, ct);
                        if (holder != null)
                            throw new InvalidOperationException(
                                $"LRN {newLrn} already belongs to student {holder.StudentNumber}, who is inactive. " +
                                "Reactivate that student, then approve this application again.");
                    }

                    student = new Student
                    {
                        StudentNumber = await StudentNumbering.NextStudentNumberAsync(_context, DateTime.UtcNow, ct),
                        FirstName = app.FirstName,
                        MiddleName = app.MiddleName ?? string.Empty,
                        LastName = app.LastName,
                        BirthDate = app.DateOfBirth,
                        Gender = app.Gender,
                        LRN = newLrn,
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
                    LearnerProfileMapping.Apply(LearnerProfileMapping.ToDto(app), student);
                    createdStudent = student;
                }

                app.StudentId = student.Id;
                app.ParentUserId = parentUserId;

                var enrollment = new Enrollment
                {
                    StudentId = student.Id,
                    SchoolYear = app.SchoolYear,
                    GradeLevel = app.GradeLevel,
                    Status = EnrollmentStatus.Draft,
                    Remarks = existingStudent != null
                        ? $"Auto-created on application approval — linked to existing student {student.StudentNumber}"
                        : "Auto-created on application approval",
                    LearnerType = app.LearnerType ?? (existingStudent != null ? "Continuing" : "New"),
                    LastGradeLevelCompleted = app.LastGradeLevelCompleted,
                    LastSchoolYearCompleted = app.LastSchoolYearCompleted,
                    LastSchoolAttended = app.PreviousSchool,
                    LastSchoolAddress = app.PreviousSchoolAddress,
                    LastSchoolId = app.LastSchoolId,
                    Track = app.Track,
                    Strand = app.Strand,
                    TenantId = app.TenantId
                };
                _context.Enrollments.Add(enrollment);

                await EnrollmentRequirements.AddForAsync(_context, enrollment, ct, app.TenantId);
            }
            else
            {
                app.Status = "Rejected";
            }

            // Single save for the whole review. Two concurrent approvals can mint the same
            // student number; the (TenantId, StudentNumber) unique index rejects the loser —
            // regenerate and retry a couple of times before giving up.
            const int maxAttempts = 3;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await _context.SaveChangesAsync(ct);
                    break;
                }
                catch (DbUpdateException) when (createdStudent != null && attempt < maxAttempts)
                {
                    // Only a freshly minted student number may be regenerated — never a reused student's.
                    createdStudent.StudentNumber = await StudentNumbering.NextStudentNumberAsync(_context, DateTime.UtcNow, ct);
                }
            }
        }, cancellationToken);

        // Best-effort notification AFTER the transaction has committed; IEmailSender never throws.
        await SendReviewOutcomeEmailAsync(app, newAccountEmail, cancellationToken);

        return ApplicationMapping.ToDetailDto(app);
    }

    /// <summary>
    /// Matches an existing ACTIVE student for a re-applying family: the application's LRN first;
    /// otherwise FirstName + LastName + DateOfBirth (trimmed, case-insensitive) - but a name/DOB
    /// candidate that already carries a DIFFERENT LRN is a different learner and never matches.
    /// Inactive students never match - a deactivated record must not resurrect through admissions.
    /// </summary>
    private async Task<Student?> FindMatchingActiveStudentAsync(AdmissionApplication app, CancellationToken ct)
    {
        var providedLrn = LearnerProfileMapping.Clean(app.Lrn);
        if (providedLrn != null)
        {
            var byLrn = await _context.Students.FirstOrDefaultAsync(s => s.IsActive && s.LRN == providedLrn, ct);
            if (byLrn != null) return byLrn;
        }

        var first = app.FirstName.Trim().ToLower();
        var last = app.LastName.Trim().ToLower();
        var candidate = await _context.Students.FirstOrDefaultAsync(s => s.IsActive
            && s.FirstName.ToLower() == first
            && s.LastName.ToLower() == last
            && s.BirthDate == app.DateOfBirth, ct);

        if (candidate != null && providedLrn != null && candidate.LRN != null && candidate.LRN != providedLrn)
            return null;
        return candidate;
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
            IsActive = true,
            MustChangePassword = true // temp password — owner must set their own on first login
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
            IsActive = true,
            MustChangePassword = true // temp password — owner must set their own on first login
        };
        _context.Users.Add(user);
        return (user.Id, email);
    }
}
