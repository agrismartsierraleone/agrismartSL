using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Audit;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Users;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Market;

public class SellerAppService(
    ISellerRepository sellerRepository,
    IMarketRepository marketRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUser,
    AuditLogAppService auditLogAppService,
    UserNotificationService userNotificationService)
{
    public async Task<IReadOnlyList<SellerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sellers = await sellerRepository.GetAllAsync(cancellationToken);
        return sellers.Select(ToDto).ToList();
    }

    public async Task<SellerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var seller = await sellerRepository.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException($"Seller '{id}' was not found.");

        return ToDto(seller);
    }

    public async Task<SellerDto> CreateAsync(CreateSellerRequest request, CancellationToken cancellationToken = default)
    {
        var seller = Seller.Create(request.Name, request.ContactEmail, request.ContactPhone, request.Location);
        await sellerRepository.AddAsync(seller, cancellationToken);

        await auditLogAppService.LogAsync(currentUser.UserId, currentUser.Email, "SellerCreated", seller.UserId, $"Created seller profile '{seller.Name}'.", cancellationToken);

        return ToDto(seller);
    }

    public async Task<SellerDto> UpdateAsync(Guid id, UpdateSellerRequest request, CancellationToken cancellationToken = default)
    {
        var seller = await sellerRepository.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException($"Seller '{id}' was not found.");

        if (currentUser.Role != UserRole.Admin && seller.UserId != currentUser.UserId)
        {
            throw new ForbiddenException("You can only update your own seller profile.");
        }

        var (previousName, previousContactEmail, previousContactPhone, previousLocation) = (seller.Name, seller.ContactEmail, seller.ContactPhone, seller.Location);

        seller.Update(request.Name, request.ContactEmail, request.ContactPhone, request.Location);
        await sellerRepository.UpdateAsync(seller, cancellationToken);

        await auditLogAppService.LogAsync(currentUser.UserId, currentUser.Email, "SellerUpdated", seller.UserId, $"Updated seller profile '{seller.Name}'.", cancellationToken);

        var changes = new List<string>();
        AddChange(changes, "Name", previousName, seller.Name);
        AddChange(changes, "Contact email", previousContactEmail, seller.ContactEmail);
        AddChange(changes, "Contact phone", previousContactPhone, seller.ContactPhone);
        AddChange(changes, "Location", previousLocation, seller.Location);

        if (changes.Count > 0)
        {
            foreach (var (email, name, role) in await GetNotificationRecipientsAsync(seller, previousContactEmail, cancellationToken))
            {
                await userNotificationService.SendAccountUpdatedEmailAsync(email, name, role, changes, cancellationToken);
            }
        }

        return ToDto(seller);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var seller = await sellerRepository.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException($"Seller '{id}' was not found.");

        if (currentUser.Role != UserRole.Admin && seller.UserId != currentUser.UserId)
        {
            throw new ForbiddenException("You can only delete your own seller profile.");
        }

        if (await marketRepository.ExistsForSellerAsync(id, cancellationToken))
        {
            throw new ConflictException("Cannot delete a seller that still has products assigned to it.");
        }

        await sellerRepository.DeleteAsync(seller, cancellationToken);

        await auditLogAppService.LogAsync(currentUser.UserId, currentUser.Email, "SellerDeleted", seller.UserId, $"Deleted seller profile '{seller.Name}'.", cancellationToken);

        foreach (var (email, name, role) in await GetNotificationRecipientsAsync(seller, null, cancellationToken))
        {
            await userNotificationService.SendSellerProfileDeletedEmailAsync(email, name, role, seller.Name, cancellationToken);
        }
    }

    /// <summary>The linked account, or the contact email (current and previous) of a profile with no account.</summary>
    private async Task<IReadOnlyList<(string Email, string Name, UserRole Role)>> GetNotificationRecipientsAsync(Seller seller, string? previousContactEmail, CancellationToken cancellationToken)
    {
        var owner = seller.UserId is { } userId ? await userRepository.GetByIdAsync(userId, cancellationToken) : null;
        if (owner is not null)
        {
            return [(owner.Email, owner.Name, owner.Role)];
        }

        return new[] { seller.ContactEmail, previousContactEmail }
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(email => (email!, seller.Name, UserRole.Seller))
            .ToList();
    }

    private static void AddChange(List<string> changes, string label, string? before, string? after)
    {
        if ((before ?? string.Empty) == (after ?? string.Empty))
        {
            return;
        }

        changes.Add(string.IsNullOrEmpty(after) ? $"{label} was removed." : $"{label} changed to '{after}'.");
    }

    private static SellerDto ToDto(Seller seller) => new(seller.Id, seller.Name, seller.ContactEmail, seller.ContactPhone, seller.Location, seller.UserId);
}
