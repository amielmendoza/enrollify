using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record MoveEnrollmentStepCommand(Guid EnrollmentId, string? Remarks) : IRequest<EnrollmentDto>;

public class MoveEnrollmentStepCommandHandler : IRequestHandler<MoveEnrollmentStepCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public MoveEnrollmentStepCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EnrollmentDto> Handle(MoveEnrollmentStepCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        var workflow = await _context.WorkflowDefinitions
            .Include(w => w.Steps.OrderBy(s => s.StepOrder))
            .FirstOrDefaultAsync(w => w.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No active workflow definition found.");

        var nextStep = workflow.Steps
            .FirstOrDefault(s => s.FromStatus == enrollment.Status);

        if (nextStep == null)
            throw new InvalidOperationException($"No workflow step found for current status '{enrollment.Status}'.");

        // Step-specific validations
        if (enrollment.Status == EnrollmentStatus.Submitted)
        {
            // Moving to Assessed: admin should have reviewed requirements (optional enforcement)
        }

        if (enrollment.Status == EnrollmentStatus.Approved)
        {
            // Moving to Paid: check balance is zero
            var totalFees = await _context.Fees
                .Where(f => f.SchoolYear == enrollment.SchoolYear && f.GradeLevel == enrollment.GradeLevel && f.IsActive)
                .SumAsync(f => f.Amount, cancellationToken);
            var totalPaid = await _context.Payments
                .Where(p => p.EnrollmentId == enrollment.Id)
                .SumAsync(p => p.Amount, cancellationToken);
            if (totalPaid < totalFees)
                throw new InvalidOperationException($"Cannot mark as paid. Outstanding balance: {(totalFees - totalPaid):N2}. Student must pay in full first.");
        }

        if (enrollment.Status == EnrollmentStatus.Paid)
        {
            // Moving to Enrolled: section must be assigned
            if (enrollment.SectionId == null)
                throw new InvalidOperationException("Cannot finalize enrollment. Please assign a section first.");
        }

        var previousStatus = enrollment.Status;
        enrollment.Status = nextStep.ToStatus;
        enrollment.Remarks = request.Remarks;

        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = previousStatus,
            ToStatus = nextStep.ToStatus,
            Remarks = request.Remarks ?? $"Moved to {nextStep.ToStatus}"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.Student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, enrollment.SectionId, enrollment.Section?.Name,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt,
            enrollment.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy)).ToList());
    }
}
