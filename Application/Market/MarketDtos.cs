using AgrismartAPI.Application.Common;

namespace AgrismartAPI.Application.Market;

public record MarketProductDto(
    Guid Id,
    string Name,
    Guid CategoryId,
    string CategoryName,
    decimal Price,
    string Unit,
    Guid SellerId,
    string SellerName,
    string Description,
    string? ImageUrl);

public record CreateProductRequest(
    string Name,
    Guid CategoryId,
    decimal Price,
    string Unit,
    Guid SellerId,
    string Description,
    UploadedFile? Image);

public record UpdateProductRequest(
    string Name,
    Guid CategoryId,
    decimal Price,
    string Unit,
    Guid SellerId,
    string Description,
    UploadedFile? Image);
