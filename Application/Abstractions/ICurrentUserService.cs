using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Abstractions;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string Email { get; }

    UserRole Role { get; }
}
