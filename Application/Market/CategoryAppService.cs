using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Market;

namespace AgrismartAPI.Application.Market;

public class CategoryAppService(ICategoryRepository categoryRepository, IMarketRepository marketRepository)
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        return categories.Select(ToDto).ToList();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await categoryRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException($"Category '{request.Name}' already exists.");
        }

        var category = Category.Create(request.Name, request.Description);
        await categoryRepository.AddAsync(category, cancellationToken);
        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, cancellationToken)
                        ?? throw new NotFoundException($"Category '{id}' was not found.");

        var existing = await categoryRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null && existing.Id != id)
        {
            throw new ConflictException($"Category '{request.Name}' already exists.");
        }

        category.Update(request.Name, request.Description);
        await categoryRepository.UpdateAsync(category, cancellationToken);
        return ToDto(category);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(id, cancellationToken)
                        ?? throw new NotFoundException($"Category '{id}' was not found.");

        if (await marketRepository.ExistsForCategoryAsync(id, cancellationToken))
        {
            throw new ConflictException("Cannot delete a category that still has products assigned to it.");
        }

        await categoryRepository.DeleteAsync(category, cancellationToken);
    }

    private static CategoryDto ToDto(Category category) => new(category.Id, category.Name, category.Description);
}
