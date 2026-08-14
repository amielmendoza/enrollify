using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record CancelEnrollmentCommand(Guid EnrollmentId, string? Reason, string CancelledBy) : IRequest<EnrollmentDto>;

public class CancelEnrollmentCommandHandler : IRequestHandler<CancelEnrollmentCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public CancelEnrollmentCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentDto> Handle(CancelEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status == EnrollmentStatus.Cancelled)
            throw new InvalidOperationException("Enrollment is already cancelled.");

        var previousStatus = enrollment.Status;
        enrollment.Status = EnrollmentStatus.Cancelled;
        enrollment.Remarks = request.Reason;

        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = previousStatus,
            ToStatus = EnrollmentStatus.Cancelled,
            Remarks = $"Cancelled by {request.CancelledBy}: {request.Reason ?? "no reason given"}"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.Student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, enrollment.SectionId, enrollment.Section?.Name,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt,
            enrollment.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy)).ToList());
    }
}
