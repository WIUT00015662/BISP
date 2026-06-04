using Bisp.Api.Data;
using Bisp.Api.Dtos;
using Bisp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bisp.Api.Controllers;

[ApiController]
[Route("api/games")]
public sealed class GamesController : ControllerBase
{
    private readonly AppDbContext _db;

    public GamesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<GameListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null,
        [FromQuery] string? genre = null,
        [FromQuery] string? store = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Games
            .Include(g => g.StorePrices)
                .ThenInclude(p => p.Store)
            .Include(g => g.ExternalGameIds)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.Name.Contains(search));

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(g => g.Genres != null && g.Genres.Contains(genre));

        if (!string.IsNullOrWhiteSpace(store))
            query = query.Where(g => g.StorePrices.Any(p => p.Store!.Code == store));

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var games = await query
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = games.Select(g =>
        {
            var bestPrice = g.StorePrices
                .OrderBy(p => p.CurrentPrice)
                .FirstOrDefault();

            var bestStoreCode = bestPrice?.Store?.Code;
            var bestStoreUrl = bestStoreCode is null
                ? null
                : BuildStoreUrl(bestStoreCode,
                    g.ExternalGameIds.FirstOrDefault(e => e.Provider == bestStoreCode)?.ExternalId);

            var bestDiscount = g.StorePrices
                .Select(p => PricingService.CalculateDiscountPercent(p.RegularPrice, p.CurrentPrice))
                .Where(d => d is not null)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            return new GameListItemDto(
                g.Id,
                g.IgdbId,
                g.Name,
                g.Genres?.Split(", ", StringSplitOptions.RemoveEmptyEntries) ?? [],
                g.CoverImageId,
                bestPrice?.CurrentPrice,
                bestPrice?.RegularPrice,
                bestDiscount,
                bestStoreCode,
                bestStoreUrl,
                g.StorePrices.Count);
        }).ToArray();

        return Ok(new PagedResult<GameListItemDto>(items, page, pageSize, totalCount, totalPages));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GameDetailDto>> Detail(Guid id, CancellationToken cancellationToken)
    {
        var game = await _db.Games
            .Include(g => g.StorePrices)
                .ThenInclude(p => p.Store)
            .Include(g => g.ExternalGameIds)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (game is null) return NotFound();

        var externalIds = game.ExternalGameIds
            .GroupBy(e => e.Provider)
            .ToDictionary(g => g.Key, g => g.First().ExternalId);

        var prices = game.StorePrices.Select(p =>
        {
            var storeCode = p.Store!.Code;
            externalIds.TryGetValue(storeCode, out var externalId);

            return new StorePriceDto(
                storeCode,
                p.Store.Name,
                BuildStoreUrl(storeCode, externalId),
                p.CurrentPrice,
                p.RegularPrice,
                PricingService.CalculateDiscountPercent(p.RegularPrice, p.CurrentPrice),
                p.Currency,
                p.LastUpdatedUtc);
        }).ToArray();

        var bestPrice = game.StorePrices.OrderBy(p => p.CurrentPrice).FirstOrDefault();
        var bestDiscount = prices.Select(p => p.DiscountPercent).Where(d => d is not null).OrderByDescending(d => d).FirstOrDefault();

        return Ok(new GameDetailDto(
            game.Id,
            game.IgdbId,
            game.Name,
            game.Genres?.Split(", ", StringSplitOptions.RemoveEmptyEntries) ?? [],
            game.CoverImageId,
            game.Summary,
            bestPrice?.CurrentPrice,
            bestPrice?.RegularPrice,
            bestDiscount,
            prices));
    }

    private static string? BuildStoreUrl(string storeCode, string? externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId)) return null;

        return storeCode switch
        {
            "steam" => $"https://store.steampowered.com/app/{externalId}",
            "gog" => $"https://www.gog.com/en/game/{externalId}",
            "epic" => $"https://store.epicgames.com/en-US/p/{externalId}",
            _ => null
        };
    }
}
