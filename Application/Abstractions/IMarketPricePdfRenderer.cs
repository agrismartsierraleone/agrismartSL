using AgrismartAPI.Application.Market;
using AgrismartAPI.Domain.Market;

namespace AgrismartAPI.Application.Abstractions;

public interface IMarketPricePdfRenderer
{
    /// <summary>Renders the board as a PDF. <paramref name="crop"/> and <paramref name="district"/> are the filters applied, if any.</summary>
    byte[] Render(MarketPriceBoardDto board, MarketPriceCategory category, string? crop, string? district);
}
