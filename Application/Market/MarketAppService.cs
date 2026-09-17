using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Market;

public class MarketAppService(
    IMarketRepository marketRepository,
    ICategoryRepository categoryRepository,
    ISellerRepository sellerRepository,
    IFileStorageService fileStorageService,
    ICurrentUserService currentUser)
{
    private static readonly string[] AllowedImageExtensions = [".png", ".jpg", ".jpeg"];

    public async Task<IReadOnlyList<MarketProductDto>> GetProductsAsync(Guid? categoryId, CancellationToken cancellationToken = default)
    {
        var products = await marketRepository.GetAllAsync(categoryId, cancellationToken);
        return products.Select(ToDto).ToList();
    }

    public async Task<MarketProductDto> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await marketRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException($"Product '{id}' was not found.");

        return ToDto(product);
    }

    public async Task<MarketProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
                        ?? throw new NotFoundException($"Category '{request.CategoryId}' was not found.");

        var seller = await sellerRepository.GetByIdAsync(request.SellerId, cancellationToken)
                     ?? throw new NotFoundException($"Seller '{request.SellerId}' was not found.");

        if (currentUser.Role == UserRole.Seller && seller.UserId != currentUser.UserId)
        {
            throw new ForbiddenException("You can only create products for your own seller profile.");
        }

        var product = MarketProduct.Create(
            request.Name,
            category.Id,
            request.Price,
            request.Unit,
            seller.Id,
            request.Description);

        if (request.Image is not null)
        {
            var imageUrl = await SaveProductImageAsync(request.Image, cancellationToken);
            product.SetImageUrl(imageUrl);
        }

        await marketRepository.AddAsync(product, cancellationToken);
        return ToDto(product, category, seller);
    }

    public async Task<MarketProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await marketRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException($"Product '{id}' was not found.");

        if (currentUser.Role == UserRole.Seller && product.Seller.UserId != currentUser.UserId)
        {
            throw new ForbiddenException("You can only modify your own products.");
        }

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
                        ?? throw new NotFoundException($"Category '{request.CategoryId}' was not found.");

        var seller = await sellerRepository.GetByIdAsync(request.SellerId, cancellationToken)
                     ?? throw new NotFoundException($"Seller '{request.SellerId}' was not found.");

        if (currentUser.Role == UserRole.Seller && seller.UserId != currentUser.UserId)
        {
            throw new ForbiddenException("You can only reassign products to your own seller profile.");
        }

        product.Update(
            request.Name,
            category.Id,
            request.Price,
            request.Unit,
            seller.Id,
            request.Description);

        if (request.Image is not null)
        {
            var imageUrl = await SaveProductImageAsync(request.Image, cancellationToken);
            fileStorageService.DeleteIfLocal(product.ImageUrl);
            product.SetImageUrl(imageUrl);
        }

        await marketRepository.UpdateAsync(product, cancellationToken);
        return ToDto(product, category, seller);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await marketRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException($"Product '{id}' was not found.");

        if (currentUser.Role == UserRole.Seller && product.Seller.UserId != currentUser.UserId)
        {
            throw new ForbiddenException("You can only delete your own products.");
        }

        fileStorageService.DeleteIfLocal(product.ImageUrl);
        await marketRepository.DeleteAsync(product, cancellationToken);
    }

    private async Task<string> SaveProductImageAsync(UploadedFile image, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
        {
            throw new ValidationException("Product image must be a .png, .jpg, or .jpeg file.");
        }

        return await fileStorageService.SaveAsync(image, "products", cancellationToken);
    }

    private static MarketProductDto ToDto(MarketProduct product) => new(
        product.Id,
        product.Name,
        product.CategoryId,
        product.Category.Name,
        product.Price,
        product.Unit,
        product.SellerId,
        product.Seller.Name,
        product.Description,
        product.ImageUrl);

    private static MarketProductDto ToDto(MarketProduct product, Category category, Seller seller) => new(
        product.Id,
        product.Name,
        product.CategoryId,
        category.Name,
        product.Price,
        product.Unit,
        product.SellerId,
        seller.Name,
        product.Description,
        product.ImageUrl);
}
