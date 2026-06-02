using Bisp.Api.Data;
using Bisp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bisp.Api.Services;

public sealed class AggregationService
{
    private const int TargetGameCount = 20;

    private readonly AppDbContext _db;
    private readonly IgdbService _igdb;
    private readonly SteamService _steam;
    private readonly GogService _gog;
    private readonly ILogger<AggregationService> _logger;

    public AggregationService(
        AppDbContext db,
        IgdbService igdb,
        SteamService steam,
        GogService gog,
        ILogger<AggregationService> logger)
    {
        _db = db;
        _igdb = igdb;
        _steam = steam;
        _gog = gog;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Aggregation run started at {UtcNow}.", DateTime.UtcNow);

        var stores = await _db.Stores.ToDictionaryAsync(s => s.Code, cancellationToken);
        if (stores.Count == 0)
        {
            _logger.LogError("No stores seeded in database. Aborting aggregation.");
            return 0;
        }

        _logger.LogInformation("Found {Count} store(s) in DB: {Stores}.",
            stores.Count, string.Join(", ", stores.Keys));

        var candidates = await _igdb.GetTopGamesWithStorePresenceAsync(100, cancellationToken);
        if (candidates.Count == 0)
        {
            _logger.LogWarning("IGDB returned no candidates. Check credentials and query.");
            return 0;
        }

        _logger.LogInformation("Processing {Count} IGDB candidates.", candidates.Count);

        int collected = 0;
        int skipped = 0;

        foreach (var candidate in candidates)
        {
            if (collected >= TargetGameCount) break;
            if (cancellationToken.IsCancellationRequested) break;

            var prices = new List<(Store Store, decimal CurrentPrice, decimal? RegularPrice)>();

            if (candidate.SteamId is not null && stores.TryGetValue("steam", out var steamStore))
            {
                var steamPrice = await _steam.GetPriceAsync(candidate.SteamId, cancellationToken);
                if (steamPrice is not null)
                    prices.Add((steamStore, steamPrice.Value.CurrentPrice, steamPrice.Value.RegularPrice));
            }

            if (candidate.GogId is not null && stores.TryGetValue("gog", out var gogStore))
            {
                var gogPrice = await _gog.GetPriceAsync(candidate.GogId, cancellationToken);
                if (gogPrice is not null)
                    prices.Add((gogStore, gogPrice.Value.CurrentPrice, gogPrice.Value.RegularPrice));
            }

            if (prices.Count < 2)
            {
                skipped++;
                _logger.LogWarning(
                    "Skipping '{Name}' (IGDB {IgdbId}): only {Count}/2 store(s) returned prices. Steam={SteamId} GOG={GogId}.",
                    candidate.Name, candidate.IgdbId, prices.Count, candidate.SteamId, candidate.GogId);
                continue;
            }

            await UpsertGameAsync(candidate, prices, cancellationToken);
            collected++;
            _logger.LogInformation("Upserted {Collected}/{Target}: {Name}", collected, TargetGameCount, candidate.Name);
        }

        _logger.LogInformation("Aggregation complete. Upserted {Upserted} game(s), skipped {Skipped} candidate(s).",
            collected, skipped);
        return collected;
    }

    private async Task UpsertGameAsync(
        IgdbGameResult candidate,
        List<(Store Store, decimal CurrentPrice, decimal? RegularPrice)> prices,
        CancellationToken cancellationToken)
    {
        var game = await _db.Games
            .Include(g => g.ExternalGameIds)
            .Include(g => g.StorePrices)
            .FirstOrDefaultAsync(g => g.IgdbId == candidate.IgdbId, cancellationToken);

        if (game is null)
        {
            game = new Game { Id = Guid.NewGuid(), IgdbId = candidate.IgdbId };
            _db.Games.Add(game);
        }

        game.Name = candidate.Name;
        game.Genres = candidate.Genres;
        game.Summary = candidate.Summary;
        game.CoverImageId = candidate.CoverImageId;

        UpsertExternalId(game, "steam", candidate.SteamId);
        UpsertExternalId(game, "gog", candidate.GogId);

        var now = DateTime.UtcNow;
        foreach (var (store, currentPrice, regularPrice) in prices)
        {
            var existing = game.StorePrices.FirstOrDefault(p => p.StoreId == store.Id);
            if (existing is null)
            {
                existing = new GameStorePrice { Id = Guid.NewGuid(), GameId = game.Id, StoreId = store.Id };
                _db.GameStorePrices.Add(existing);
                game.StorePrices.Add(existing);
            }

            existing.CurrentPrice = currentPrice;
            existing.RegularPrice = regularPrice;
            existing.LastUpdatedUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void UpsertExternalId(Game game, string provider, string? externalId)
    {
        if (externalId is null) return;
        var existing = game.ExternalGameIds.FirstOrDefault(e => e.Provider == provider);
        if (existing is null)
        {
            game.ExternalGameIds.Add(new ExternalGameId
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                Provider = provider,
                ExternalId = externalId
            });
        }
        else
        {
            existing.ExternalId = externalId;
        }
    }
}
