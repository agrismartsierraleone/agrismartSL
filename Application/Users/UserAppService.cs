using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Users;

public class UserAppService(IUserRepository userRepository, ISellerRepository sellerRepository, IPasswordHasher passwordHasher)
{
    public async Task<IReadOnlyList<UserSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);
        return users.Select(ToDto).ToList();
    }

    /// <summary>Admin-only: creates a seller or buyer account directly, without going through public self-registration.</summary>
    public async Task<UserSummaryDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ValidationException("Password must be at least 8 characters long.");
        }

        var existing = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = User.Register(request.Name, request.Email, passwordHasher.Hash(request.Password), request.Role);
        await userRepository.AddAsync(user, cancellationToken);

        if (user.Role == UserRole.Seller)
        {
            await sellerRepository.AddAsync(Seller.Create(user.Name, user.Email, null, null, user.Id), cancellationToken);
        }

        return ToDto(user);
    }

    public async Task<UserSummaryDto> ChangeRoleAsync(Guid userId, ChangeUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException($"User '{userId}' was not found.");

        user.ChangeRole(request.Role);
        await userRepository.UpdateAsync(user, cancellationToken);

        if (request.Role == UserRole.Seller && await sellerRepository.GetByUserIdAsync(user.Id, cancellationToken) is null)
        {
            await sellerRepository.AddAsync(Seller.Create(user.Name, user.Email, null, null, user.Id), cancellationToken);
        }

        return ToDto(user);
    }

    private static UserSummaryDto ToDto(User user) => new(user.Id, user.Name, user.Email, user.Role, user.CreatedAt);
}
