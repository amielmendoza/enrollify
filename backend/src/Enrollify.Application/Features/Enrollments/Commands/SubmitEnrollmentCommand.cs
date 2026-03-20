using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record SubmitEnrollmentCommand(Guid EnrollmentId, Guid UserId) : IRequest<EnrollmentDto>;

public class SubmitEnrollmentCommandHandler : IRequestHandler<SubmitEnrollmentCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public SubmitEnrollmentCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentDto> Handle(SubmitEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId && e.StudentId == student.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status != EnrollmentStatus.Draft)
            throw new InvalidOperationException("Enrollment can only be submitted from Draft status.");

        var unsubmitted = enrollment.Requirements.Where(r => !r.IsSubmitted).Select(r => r.DocumentName).ToList();
        if (unsubmitted.Any())
            throw new InvalidOperationException($"Please upload all requirements first: {string.Join(", ", unsubmitted)}");

        enrollment.Status = EnrollmentStatus.Submitted;

        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = EnrollmentStatus.Draft,
            ToStatus = EnrollmentStatus.Submitted,
            Remarks = "Submitted by student"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(enrollment);
    }

    private static EnrollmentDto MapToDto(Enrollment e) => new(e.Id, e.StudentId, e.Student.FullName,
        e.SchoolYear, e.GradeLevel, e.SectionId, e.Section?.Name, e.Status, e.Remarks, e.PaymentPlan, e.CreatedAt,
        e.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy)).ToList());
}
