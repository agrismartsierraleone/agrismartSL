using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Infrastructure.Auth;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (value is null || !Guid.TryParse(value, out var userId))
            {
                throw new UnauthorizedAccessException("No authenticated user in the current request context.");
            }

            return userId;
        }
    }

    public string Email
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Email);
            if (string.IsNullOrEmpty(value))
            {
                throw new UnauthorizedAccessException("No authenticated user in the current request context.");
            }

            return value;
        }
    }

    public UserRole Role
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
            if (value is null || !Enum.TryParse<UserRole>(value, out var role))
            {
                throw new UnauthorizedAccessException("No authenticated user in the current request context.");
            }

            return role;
        }
    }
}
