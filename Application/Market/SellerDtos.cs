namespace AgrismartAPI.Application.Market;

public record SellerDto(Guid Id, string Name, string? ContactEmail, string? ContactPhone, string? Location, Guid? UserId);

public record CreateSellerRequest(string Name, string? ContactEmail, string? ContactPhone, string? Location);

public record UpdateSellerRequest(string Name, string? ContactEmail, string? ContactPhone, string? Location);
