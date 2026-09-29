namespace AgrismartAPI.Application.Common;

/// <summary>One page of a larger result set. <c>page</c> is 1-based.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
