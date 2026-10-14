using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

/// <summary>
/// Registrar confirms a pre-migration LRN is the learner's real DepEd LRN — clears the
/// "Check LRN" flag. Only a well-formed 12-digit LRN can be confirmed; anything else must be
/// corrected through the edit form first.
/// </summary>
public record ConfirmLrnCommand(Guid StudentId) : IRequest<StudentDto>;

public class ConfirmLrnCommandHandler : IRequestHandler<ConfirmLrnCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public ConfirmLrnCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<StudentDto> Handle(ConfirmLrnCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        if (student.LRN == null)
            throw new InvalidOperationException("This student has no LRN to confirm.");
        if (!DepEdLists.IsTwelveDigitLrn(student.LRN))
            throw new InvalidOperationException($"LRN '{student.LRN}' is not 12 digits — correct it in the edit form before confirming.");

        student.LrnNeedsReview = false;
        await _context.SaveChangesAsync(cancellationToken);
        return StudentMapping.ToDto(student);
    }
}
