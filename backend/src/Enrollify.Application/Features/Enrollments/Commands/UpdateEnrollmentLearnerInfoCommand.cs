using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.ShsStrands;
using Enrollify.Application.Features.Students;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Registrar edits an enrollment's DepEd fields (learner type, previous school, track/strand).
/// Editable only before assessment: assessment may price fees by strand (sub-project 2), so the
/// fields lock at Assessed. "Send back for re-assessment" returns the enrollment to Submitted,
/// which unlocks them again.
/// </summary>
public record UpdateEnrollmentLearnerInfoCommand(Guid EnrollmentId, EnrollmentLearnerInfoDto Info) : IRequest<EnrollmentLearnerInfoDto>;

public class UpdateEnrollmentLearnerInfoCommandValidator : AbstractValidator<UpdateEnrollmentLearnerInfoCommand>
{
    public UpdateEnrollmentLearnerInfoCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.Info).NotNull().SetValidator(new EnrollmentLearnerInfoDtoValidator());
    }
}

public class UpdateEnrollmentLearnerInfoCommandHandler : IRequestHandler<UpdateEnrollmentLearnerInfoCommand, EnrollmentLearnerInfoDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateEnrollmentLearnerInfoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentLearnerInfoDto> Handle(UpdateEnrollmentLearnerInfoCommand request, CancellationToken cancellationToken)
    {
        var e = await _context.Enrollments.FirstOrDefaultAsync(x => x.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (e.Status is not (EnrollmentStatus.Draft or EnrollmentStatus.Submitted))
            throw new InvalidOperationException(
                "Learner details are locked once the enrollment is assessed. Send it back for re-assessment to change them.");

        var info = request.Info;
        // An unchanged pair is kept as-is: it may have been carried from last year and since deactivated,
        // and the registrar must still be able to save other fields. Only a changed pair is validated.
        var sameAsCurrent = e.Track != null && e.Strand != null
            && string.Equals(info.Track?.Trim(), e.Track, StringComparison.OrdinalIgnoreCase)
            && string.Equals(info.Strand?.Trim(), e.Strand, StringComparison.OrdinalIgnoreCase);
        var (track, strand) = sameAsCurrent
            ? (e.Track, e.Strand)
            : await ShsStrandRules.ResolveAsync(_context, e.GradeLevel, info.Track, info.Strand, cancellationToken);

        e.LearnerType = LearnerProfileMapping.Clean(info.LearnerType);
        e.LastGradeLevelCompleted = LearnerProfileMapping.Clean(info.LastGradeLevelCompleted);
        e.LastSchoolYearCompleted = LearnerProfileMapping.Clean(info.LastSchoolYearCompleted);
        e.LastSchoolAttended = LearnerProfileMapping.Clean(info.LastSchoolAttended);
        e.LastSchoolAddress = LearnerProfileMapping.Clean(info.LastSchoolAddress);
        e.LastSchoolId = LearnerProfileMapping.Clean(info.LastSchoolId);
        e.Track = track;
        e.Strand = strand;

        await _context.SaveChangesAsync(cancellationToken);
        return EnrollmentLearnerInfoMapping.ToDto(e);
    }
}
