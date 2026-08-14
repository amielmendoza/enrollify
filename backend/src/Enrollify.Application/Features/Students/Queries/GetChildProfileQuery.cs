using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Queries;

public record GetChildProfileQuery(Guid StudentId, Guid ParentUserId) : IRequest<StudentDto>;

public class GetChildProfileQueryHandler : IRequestHandler<GetChildProfileQuery, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public GetChildProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StudentDto> Handle(GetChildProfileQuery request, CancellationToken cancellationToken)
    {
        var s = await _context.Students.FirstOrDefaultAsync(
            s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

        return new StudentDto(s.Id, s.LRN, s.FirstName, s.MiddleName, s.LastName,
            s.BirthDate, s.Gender, s.Address, s.ContactNumber, s.Email,
            s.GuardianName, s.GuardianContact, s.FullName);
    }
}
