using System.Globalization;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Market;
using AgrismartAPI.Domain.Market;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AgrismartAPI.Infrastructure.Market;

public class MarketPricePdfRenderer : IMarketPricePdfRenderer
{
    private const string Ink = "#17191C";
    private const string Slate = "#777B86";
    private const string Hairline = "#E6E7EA";
    private const string Leaf = "#1A8955";
    private const string DateFormat = "d MMM yyyy";
    private const string FontFamily = "Lato"; // Ships with QuestPDF, so it needs no font installed on the server.
    private const float CellGap = 6;

    static MarketPricePdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(MarketPriceBoardDto board, MarketPriceCategory category, string? crop, string? district) =>
        Document.Create(container => container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(10).FontColor(Ink));

                page.Header().PaddingBottom(24).Element(header => ComposeHeader(header, board, category, crop, district));
                page.Content().Element(content =>
                {
                    if (board.Items.Count == 0)
                    {
                        content.Text("No market prices were recorded for this selection.").FontColor(Slate);
                        return;
                    }

                    ComposeTable(content, board, category);
                });
                page.Footer().PaddingTop(16).Element(ComposeFooter);
            }))
            .WithMetadata(new DocumentMetadata { Title = $"AgriSmart {category.ToString().ToLowerInvariant()} prices", Author = "AgriSmart" })
            .GeneratePdf();

    private static void ComposeHeader(IContainer container, MarketPriceBoardDto board, MarketPriceCategory category, string? crop, string? district) =>
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("AgriSmart").FontSize(11).SemiBold().FontColor(Leaf);
                column.Item().PaddingTop(2).Text($"{category} prices").FontSize(26);
                column.Item().PaddingTop(4).Text($"{crop ?? $"All {Plural(category)}"} · {district ?? "All districts"}").FontColor(Slate);
            });

            row.AutoItem().AlignBottom().Column(column =>
            {
                column.Item().AlignRight().Text($"As of {FormatDate(board.AsOf)}");
                column.Item().AlignRight().Text($"Prices in {board.Currency}").FontColor(Slate);
            });
        });

    private static void ComposeTable(IContainer container, MarketPriceBoardDto board, MarketPriceCategory category) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(3);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(3);
                columns.RelativeColumn(2.2f);
            });

            table.Header(header =>
            {
                HeaderCell(header.Cell()).Text(category.ToString());
                HeaderCell(header.Cell()).Text("District");
                HeaderCell(header.Cell()).Text("Unit");
                HeaderCell(header.Cell()).AlignRight().Text("Price");
                HeaderCell(header.Cell()).AlignRight().Text("Change");
                HeaderCell(header.Cell(), lastColumn: true).AlignRight().Text("Date");
            });

            foreach (var item in board.Items)
            {
                BodyCell(table.Cell()).Text(item.Crop).SemiBold();
                BodyCell(table.Cell()).Text(item.District);
                BodyCell(table.Cell()).Text(item.Unit).FontColor(Slate);
                BodyCell(table.Cell()).AlignRight().Text(FormatAmount(item.Price)).SemiBold();
                BodyCell(table.Cell()).AlignRight().Text(FormatChange(item));
                BodyCell(table.Cell(), lastColumn: true).AlignRight().Text(FormatDate(item.PriceDate)).FontColor(Slate);
            }
        });

    private static void ComposeFooter(IContainer container) =>
        container.Row(row =>
        {
            row.RelativeItem()
                .Text($"Each row is the latest price in the {MarketPriceAppService.BoardLookbackDays} days to the as-of date. Change compares it with the record before it.")
                .FontSize(8).FontColor(Slate);

            row.AutoItem().PaddingLeft(16).Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Slate));
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });

    private static IContainer HeaderCell(IContainer container, bool lastColumn = false) =>
        container.BorderBottom(1).BorderColor(Ink).PaddingVertical(6).PaddingRight(lastColumn ? 0 : CellGap)
            .DefaultTextStyle(style => style.FontSize(9).FontColor(Slate));

    /// <summary>ShowEntire keeps a row with long text together instead of splitting it across two pages.</summary>
    private static IContainer BodyCell(IContainer container, bool lastColumn = false) =>
        container.ShowEntire().BorderBottom(0.5f).BorderColor(Hairline).PaddingVertical(7).PaddingRight(lastColumn ? 0 : CellGap);

    private static string FormatChange(MarketPriceDto item) =>
        item is { Change: { } change, ChangePercent: { } percent }
            ? $"{change.ToString("+#,##0.00;-#,##0.00;0.00", CultureInfo.InvariantCulture)} ({percent.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)}%)"
            : "New";

    private static string Plural(MarketPriceCategory category) => $"{category.ToString().ToLowerInvariant()}s";

    private static string FormatAmount(decimal amount) => amount.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static string FormatDate(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
