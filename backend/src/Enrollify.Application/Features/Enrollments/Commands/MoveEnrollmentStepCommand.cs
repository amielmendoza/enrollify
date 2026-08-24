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
    private readonly IEmailSender _emailSender;

    public MoveEnrollmentStepCommandHandler(IApplicationDbContext context, IEmailSender emailSender)
    {
        _context = context;
        _emailSender = emailSender;
    }

    public async Task<EnrollmentDto> Handle(MoveEnrollmentStepCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        // Tenants without a configured workflow fall back to the standard transition table,
        // so a freshly provisioned school can run the full enrollment flow out of the box.
        var workflow = await _context.WorkflowDefinitions
            .Include(w => w.Steps.OrderBy(s => s.StepOrder))
            .FirstOrDefaultAsync(w => w.IsActive, cancellationToken);

        var toStatus = workflow != null
            ? workflow.Steps.FirstOrDefault(s => s.FromStatus == enrollment.Status)?.ToStatus
            : Workflows.DefaultWorkflow.NextStatus(enrollment.Status);

        if (toStatus == null)
            throw new InvalidOperationException($"No workflow step found for current status '{enrollment.Status}'.");

        // Step-specific validations
        if (enrollment.Status == EnrollmentStatus.Submitted)
        {
            // Moving to Assessed: without a matching fee structure the enrollment would be
            // assessed at a zero balance and sail through the payment gate unpaid.
            var activeFees = await _context.Fees
                .Where(f => f.SchoolYear == enrollment.SchoolYear && f.GradeLevel == enrollment.GradeLevel && f.IsActive)
                .ToListAsync(cancellationToken);
            if (activeFees.Count == 0)
                throw new InvalidOperationException($"No active fees are configured for {enrollment.GradeLevel}, {enrollment.SchoolYear}. Add fees in Settings before assessing this enrollment.");

            // Snapshot the fee catalog at assessment time so later catalog edits do not
            // retroactively change this enrollment's balance. Re-assessment replaces any
            // previous snapshot.
            var existingSnapshot = await _context.EnrollmentFees
                .Where(f => f.EnrollmentId == enrollment.Id)
                .ToListAsync(cancellationToken);
            _context.EnrollmentFees.RemoveRange(existingSnapshot);

            foreach (var fee in activeFees)
            {
                _context.EnrollmentFees.Add(new EnrollmentFee
                {
                    EnrollmentId = enrollment.Id,
                    Name = fee.Name,
                    Description = fee.Description,
                    Amount = fee.Amount
                });
            }

            enrollment.AssessedTotal = activeFees.Sum(f => f.Amount);
            enrollment.AssessedAt = DateTime.UtcNow;
        }

        if (enrollment.Status == EnrollmentStatus.Approved)
        {
            // Prefer the fee total snapshotted at assessment; enrollments assessed before
            // snapshots existed fall back to the live Fee catalog.
            var totalFees = enrollment.AssessedTotal
                ?? await _context.Fees
                    .Where(f => f.SchoolYear == enrollment.SchoolYear && f.GradeLevel == enrollment.GradeLevel && f.IsActive)
                    .SumAsync(f => f.Amount, cancellationToken);
            var totalApproved = await _context.Payments
                .Where(p => p.EnrollmentId == enrollment.Id && p.Status == "Approved")
                .SumAsync(p => p.Amount, cancellationToken);

            // Thresholds come from the shared PaymentGate helper (mirrors PaymentsCalculator):
            // installment plans owe the tenant's PaymentTerm down payment; a Full payment only
            // owes the discounted effective total. ReviewPaymentCommand uses the same helper
            // to auto-advance, so manual and automatic gating can never disagree.
            // Deliberate choice: this gate stays based on assessed fees only. Manual ledger
            // adjustments (see LedgerCalculator / LedgerAdjustment) change the balance owed,
            // not the down-payment threshold required to mark the enrollment as paid.
            var term = enrollment.PaymentPlan != null
                ? await _context.PaymentTerms.FirstOrDefaultAsync(
                    t => t.SchoolYear == enrollment.SchoolYear && t.PlanType == enrollment.PaymentPlan && t.IsActive, cancellationToken)
                : null;

            var minRequired = Payments.PaymentGate.MinimumRequired(enrollment.PaymentPlan, totalFees, term);

            if (totalApproved < minRequired)
                throw new InvalidOperationException($"Cannot mark as paid. Minimum required: {minRequired:N2}. Approved so far: {totalApproved:N2}.");
        }

        if (enrollment.Status == EnrollmentStatus.Paid)
        {
            // Moving to Enrolled: section must be assigned
            if (enrollment.SectionId == null)
                throw new InvalidOperationException("Cannot finalize enrollment. Please assign a section first.");
        }

        var previousStatus = enrollment.Status;
        enrollment.Status = toStatus.Value;
        enrollment.Remarks = request.Remarks;

        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = previousStatus,
            ToStatus = toStatus.Value,
            Remarks = request.Remarks ?? $"Moved to {toStatus.Value}"
        });

        await _context.SaveChangesAsync(cancellationToken);

        // Best-effort notification once the enrollment is finalized; IEmailSender never throws.
        if (enrollment.Status == EnrollmentStatus.Enrolled)
            await SendEnrolledEmailAsync(enrollment, cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.Student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, enrollment.SectionId, enrollment.Section?.Name,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt,
            enrollment.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy)).ToList());
    }

    private async Task SendEnrolledEmailAsync(Enrollment enrollment, CancellationToken ct)
    {
        // Prefer the managing parent's account email when the student was registered by a parent
        // (same resolution as the payment-review notification). Student/Section are already loaded.
        var recipient = enrollment.Student.Email;
        if (enrollment.Student.ParentUserId != null)
        {
            var parentEmail = await _context.Users
                .Where(u => u.Id == enrollment.Student.ParentUserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(parentEmail))
                recipient = parentEmail;
        }

        if (string.IsNullOrWhiteSpace(recipient))
            return; // No recipient email — skip.

        var body = $"{enrollment.Student.FullName} is now officially enrolled for {enrollment.GradeLevel}, SY {enrollment.SchoolYear}.";
        if (enrollment.Section != null)
            body += $"\nSection: {enrollment.Section.Name}";

        await _emailSender.SendAsync(recipient, $"Enrollment finalized — {enrollment.Student.FullName}", body, ct);
    }
}
