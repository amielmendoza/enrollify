using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Queries;

public record GetStudentByIdQuery(Guid Id) : IRequest<StudentDto>;

public class GetStudentByIdQueryHandler : IRequestHandler<GetStudentByIdQuery, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public GetStudentByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StudentDto> Handle(GetStudentByIdQuery request, CancellationToken cancellationToken)
    {
        var s = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Student with ID '{request.Id}' not found.");

        return new StudentDto(s.Id, s.LRN, s.FirstName, s.MiddleName, s.LastName,
            s.BirthDate, s.Gender, s.Address, s.ContactNumber, s.Email,
            s.GuardianName, s.GuardianContact, s.FullName);
    }
}
