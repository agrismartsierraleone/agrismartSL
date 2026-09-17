using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Users;

public class UserAppService(IUserRepository userRepository, ISellerRepository sellerRepository)
{
    public async Task<IReadOnlyList<UserSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);
        return users.Select(ToDto).ToList();
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
