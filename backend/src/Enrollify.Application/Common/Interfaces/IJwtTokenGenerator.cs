using Enrollify.Domain.Entities;

namespace Enrollify.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
