using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Audit;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Market;

public class SellerAppService(
    ISellerRepository sellerRepository,
    IMarketRepository marketRepository,
    ICurrentUserService currentUser,
    AuditLogAppService auditLogAppService)
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

        seller.Update(request.Name, request.ContactEmail, request.ContactPhone, request.Location);
        await sellerRepository.UpdateAsync(seller, cancellationToken);

        await auditLogAppService.LogAsync(currentUser.UserId, currentUser.Email, "SellerUpdated", seller.UserId, $"Updated seller profile '{seller.Name}'.", cancellationToken);

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
    }

    private static SellerDto ToDto(Seller seller) => new(seller.Id, seller.Name, seller.ContactEmail, seller.ContactPhone, seller.Location, seller.UserId);
}
