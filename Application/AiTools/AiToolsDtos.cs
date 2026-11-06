using AgrismartAPI.Application.Orders;
using AgrismartAPI.Domain.Orders;

namespace AgrismartAPI.Application.AiTools;

/// <summary>A suggestion for the seller to edit. Nothing is saved, and the seller sets the price.</summary>
public record ListingDraftDto(string Title, string Description, Guid? CategoryId, string? CategoryName, string? Unit);

public record PriceSuggestionRequest(string? ProductName, Guid? CategoryId, string? Unit);

/// <summary>The prices come from real listings and sales. <see cref="SuggestedPrice"/> is null when too few are comparable.</summary>
public record PriceSuggestionDto(string ProductName, decimal? SuggestedPrice, decimal? LowestPrice, decimal? HighestPrice, int ComparableListings, int ComparableSales, string Explanation);

public record EarningsSummaryDto(SellerEarnings Earnings, string Currency, string Summary);

public record DeliveryStopDto(Guid OrderId, string CustomerName, string? CustomerPhone, string? Address, IReadOnlyList<OrderItemDto> Items, OrderStatus Status);

/// <param name="ApproximateDistanceKm">From the starting district; null when no starting district is known.</param>
public record DeliveryTripDto(string District, double? ApproximateDistanceKm, IReadOnlyList<DeliveryStopDto> Stops);

/// <param name="Advice">AI-written advice about the plan; null if the AI was unavailable.</param>
public record DeliveryPlanDto(string? FromDistrict, IReadOnlyList<DeliveryTripDto> Trips, IReadOnlyList<DeliveryStopDto> MissingDistrict, string? Advice);

public record JobDraftRequest(string? Notes);

/// <summary>A suggestion to review before posting. Pay is left out so the AI never makes up a wage.</summary>
public record JobDraftDto(string Title, string Description, string? District, int? WorkersNeeded, IReadOnlyList<string> Skills);

public record WorkerProfileDraftRequest(string? Experience, string? Skills, string? District, string? Availability);

public record WorkerProfileDraftDto(string Headline, string Bio, string? District, IReadOnlyList<string> Skills);

/// <summary>Contains no phone number: a worker shares that by applying to the job.</summary>
public record WorkerMatchDto(Guid WorkerUserId, string Name, string Headline, string District, IReadOnlyList<string> Skills, int Score, string Reason);
