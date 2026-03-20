using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

public record CreateStudentAccountCommand(Guid StudentId, string Email, string Password) : IRequest<Guid>;

public class CreateStudentAccountCommandValidator : AbstractValidator<CreateStudentAccountCommand>
{
    public CreateStudentAccountCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class CreateStudentAccountCommandHandler : IRequestHandler<CreateStudentAccountCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateStudentAccountCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateStudentAccountCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        if (student.UserId.HasValue)
            throw new InvalidOperationException("Student already has an account.");

        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = student.FirstName,
            LastName = student.LastName,
            Role = UserRole.Student
        };

        _context.Users.Add(user);
        student.UserId = user.Id;

        await _context.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
