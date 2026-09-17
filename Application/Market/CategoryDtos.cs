namespace AgrismartAPI.Application.Market;

public record CategoryDto(Guid Id, string Name, string? Description);

public record CreateCategoryRequest(string Name, string? Description);

public record UpdateCategoryRequest(string Name, string? Description);
