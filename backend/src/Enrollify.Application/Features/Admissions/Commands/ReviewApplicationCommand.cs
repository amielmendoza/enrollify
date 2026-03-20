using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Admissions;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Admissions.Commands;

public record ReviewApplicationCommand(Guid ApplicationId, bool IsApproved, string? Notes) : IRequest<ApplicationDetailDto>;

public class ReviewApplicationCommandHandler : IRequestHandler<ReviewApplicationCommand, ApplicationDetailDto>
{
    private readonly IApplicationDbContext _context;

    public ReviewApplicationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationDetailDto> Handle(ReviewApplicationCommand request, CancellationToken cancellationToken)
    {
        var app = await _context.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == request.ApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Application not found.");

        if (app.Status != "Submitted" && app.Status != "UnderReview")
            throw new InvalidOperationException($"Cannot review application in '{app.Status}' status.");

        app.ReviewedAt = DateTime.UtcNow;
        app.ReviewNotes = request.Notes;

        if (request.IsApproved)
        {
            app.Status = "Approved";

            // Auto-create student
            var student = new Student
            {
                LRN = $"LRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
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
                TenantId = app.TenantId
            };

            // Auto-create user account
            var user = new User
            {
                Email = app.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"),
                FirstName = app.FirstName,
                LastName = app.LastName,
                Role = UserRole.Student,
                TenantId = app.TenantId
            };

            _context.Users.Add(user);
            student.UserId = user.Id;
            _context.Students.Add(student);
            app.StudentId = student.Id;
        }
        else
        {
            app.Status = "Rejected";
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ApplicationDetailDto(
            app.Id, app.ApplicationNumber, app.FirstName, app.MiddleName, app.LastName,
            app.Email, app.ContactNumber, app.Gender, app.DateOfBirth, app.Address,
            app.GradeLevel, app.SchoolYear, app.PreviousSchool, app.PreviousSchoolAddress,
            app.GuardianName, app.GuardianContact, app.GuardianRelationship,
            app.Status, app.CreatedAt, app.ReviewedAt, app.ReviewNotes, app.StudentId);
    }
}
