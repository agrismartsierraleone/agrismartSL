using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Farms;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Domain.Harvest;
using AgrismartAPI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Controllers;

/// <summary>
/// "My farms": after signing in, a farmer adds, edits and deletes their farms (location, size and crops) so
/// the AI assistant can personalize its answers. Only the caller's own farms are ever returned or changed.
/// </summary>
/// <remarks>
/// Uses only services the app already registers (DbContext, current user, crop catalogues), so no existing
/// file needed to change for this endpoint.
/// </remarks>
[ApiController]
[Route("farms")]
[Tags("My farms")]
[Produces("application/json")]
[Authorize]
public class FarmsController(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    ICropRepository harvestCropRepository,
    IFertilizerCropRepository fertilizerCropRepository) : ControllerBase
{
    /// <summary>Adds a farm for the signed-in farmer, optionally with its crops.</summary>
    /// <remarks>
    /// Area and plot sizes can be given in hectare (default), acre or squareMeter; they are stored and returned
    /// in hectares. district must be one of Sierra Leone's 16 districts (any capitalisation).
    /// </remarks>
    /// <response code="201">Created; returns the farm.</response>
    /// <response code="400">Invalid values, with field-level errors.</response>
    /// <response code="401">Not signed in.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FarmDto>> AddFarm(FarmRequest request, CancellationToken cancellationToken)
    {
        var valid = await ValidateAsync(request, cancellationToken);

        var farm = Farm.Create(currentUser.UserId, valid.Name, valid.District, request.Latitude, request.Longitude, valid.AreaHectares);
        foreach (var (cropId, plantingDate, plotSizeHectares) in valid.Crops)
        {
            farm.AddCrop(cropId, plantingDate, plotSizeHectares);
        }

        dbContext.Set<Farm>().Add(farm);
        await dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ToDto(farm));
    }

    /// <summary>Lists the signed-in farmer's farms with their crops, oldest first.</summary>
    /// <response code="200">Your farms.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<FarmDto>>> GetMyFarms(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var farms = await dbContext.Set<Farm>()
            .AsNoTracking()
            .Include(farm => farm.Crops)
            .Where(farm => farm.UserId == userId)
            .OrderBy(farm => farm.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(farms.Select(ToDto).ToList());
    }

    /// <summary>Edits one of the signed-in farmer's farms.</summary>
    /// <remarks>
    /// Same body and rules as POST /farms; name, district, location and area are replaced (omitted optional
    /// fields are cleared). crops: omit or send null to keep the current crops; send a list (even []) to replace them.
    /// </remarks>
    /// <response code="200">Updated; returns the farm.</response>
    /// <response code="400">Invalid values, with field-level errors.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Not found, or it belongs to another farmer.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmDto>> EditFarm(Guid id, FarmRequest request, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(id, cancellationToken);
        var valid = await ValidateAsync(request, cancellationToken);

        // Farm has no update method and its file is left unchanged, so the validated values are written through
        // EF's change tracker (the same guards as Farm.Create were applied by FarmRequestValidator).
        var entry = dbContext.Entry(farm);
        entry.Property(f => f.Name).CurrentValue = valid.Name;
        entry.Property(f => f.District).CurrentValue = valid.District;
        entry.Property(f => f.Latitude).CurrentValue = request.Latitude;
        entry.Property(f => f.Longitude).CurrentValue = request.Longitude;
        entry.Property(f => f.AreaHectares).CurrentValue = valid.AreaHectares;

        var crops = farm.Crops.ToList();
        if (request.Crops is not null)
        {
            dbContext.Set<FarmCrop>().RemoveRange(crops);
            crops = [];
            foreach (var (cropId, plantingDate, plotSizeHectares) in valid.Crops)
            {
                var crop = farm.AddCrop(cropId, plantingDate, plotSizeHectares);
                // Explicit Add: the key is client-generated, so EF would otherwise treat it as an existing row.
                dbContext.Set<FarmCrop>().Add(crop);
                crops.Add(crop);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(farm, crops));
    }

    /// <summary>Deletes one of the signed-in farmer's farms and its crops.</summary>
    /// <remarks>Farm records linked to the farm are kept, with their farm link cleared.</remarks>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Not found, or it belongs to another farmer.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFarm(Guid id, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(id, cancellationToken);

        // Track linked records so EF applies the SetNull itself (the database does too; this also covers test providers).
        await dbContext.Set<FarmRecord>().Where(record => record.FarmId == farm.Id).LoadAsync(cancellationToken);
        dbContext.Set<Farm>().Remove(farm);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Tracked, with crops. Another farmer's farm is reported as not found so ids are not leaked.</summary>
    private async Task<Farm> GetOwnedFarmAsync(Guid id, CancellationToken cancellationToken)
    {
        var farm = await dbContext.Set<Farm>()
            .Include(f => f.Crops)
            .SingleOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (farm is null || farm.UserId != currentUser.UserId)
        {
            throw new NotFoundException($"Farm '{id}' was not found.");
        }

        return farm;
    }

    private async Task<FarmRequestValidator.ValidFarm> ValidateAsync(FarmRequest request, CancellationToken cancellationToken)
    {
        var harvestCrops = await harvestCropRepository.GetAllAsync(cancellationToken);
        var fertilizerCrops = await fertilizerCropRepository.GetAllAsync(cancellationToken);
        var knownCropIds = harvestCrops.Select(crop => crop.Key).Concat(fertilizerCrops.Select(crop => crop.Key)).ToHashSet();

        // Same 400 shape as the fertilizer endpoints: { message, errors }.
        return FarmRequestValidator.Validate(request, knownCropIds, out var errors)
               ?? throw FertilizerRequestValidator.ToException(errors);
    }

    private static FarmDto ToDto(Farm farm) => ToDto(farm, farm.Crops);

    private static FarmDto ToDto(Farm farm, IEnumerable<FarmCrop> crops) => new(
        farm.Id, farm.Name, farm.District, farm.Latitude, farm.Longitude, farm.AreaHectares,
        crops.Select(crop => new FarmCropDto(crop.Id, crop.CropId, crop.PlantingDate, crop.PlotSizeHectares)).ToList(),
        farm.CreatedAt);
}
