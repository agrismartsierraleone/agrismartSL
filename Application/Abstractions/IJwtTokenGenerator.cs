using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
