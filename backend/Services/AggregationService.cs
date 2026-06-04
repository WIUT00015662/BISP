using Bisp.Api.Data;
using Bisp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bisp.Api.Services;

public sealed class AggregationService
{
    private const int TargetGameCount = 20;
    private const int CandidateLimit = 100;

    private readonly AppDbContext _db;
    private readonly IgdbService _igdb;
    private readonly SteamService _steam;
    private readonly GogService _gog;
    private readonly EpicService _epic;
    private readonly ILogger<AggregationService> _logger;

    public AggregationService(
        AppDbContext db,
        IgdbService igdb,
        SteamService steam,
        GogService gog,
        EpicService epic,
        ILogger<AggregationService> logger)
    {
        _db = db;
        _igdb = igdb;
        _steam = steam;
        _gog = gog;
        _epic = epic;
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

        var expectedStores = new[] { "steam", "gog", "epic" };
        var missingStores = expectedStores.Where(code => !stores.ContainsKey(code)).ToArray();
        if (missingStores.Length > 0)
        {
            _logger.LogWarning("Missing expected store(s) in DB: {Stores}.", string.Join(", ", missingStores));
        }

        var candidates = await _igdb.GetTopGamesWithStorePresenceAsync(CandidateLimit, cancellationToken);
        if (candidates.Count == 0)
        {
            _logger.LogWarning("IGDB returned no candidates. Check credentials and query.");
            return 0;
        }

        var steamOnly = candidates.Count(c => c.GogId is null && c.EpicId is null);
        var steamGog = candidates.Count(c => c.GogId is not null);
        var steamEpic = candidates.Count(c => c.EpicId is not null);
        var allThree = candidates.Count(c => c.GogId is not null && c.EpicId is not null);
        var atLeastTwo = candidates.Count - steamOnly;

        _logger.LogInformation(
            "Processing {Count} IGDB candidates. Steam-only: {SteamOnly}, Steam+GOG: {SteamGog}, Steam+Epic: {SteamEpic}, All three: {AllThree}, At least two stores: {AtLeastTwo}.",
            candidates.Count, steamOnly, steamGog, steamEpic, allThree, atLeastTwo);

        int collected = 0;
        int skipped = 0;
        int steamAttempts = 0;
        int steamSuccess = 0;
        int gogAttempts = 0;
        int gogSuccess = 0;
        int epicAttempts = 0;
        int epicSuccess = 0;

        foreach (var candidate in candidates)
        {
            if (collected >= TargetGameCount) break;
            if (cancellationToken.IsCancellationRequested) break;

            var prices = new List<(Store Store, decimal CurrentPrice, decimal? RegularPrice)>();
            string? gogSlugForUrl = null;

            if (candidate.SteamId is not null && stores.TryGetValue("steam", out var steamStore))
            {
                steamAttempts++;
                var steamPrice = await _steam.GetPriceAsync(candidate.SteamId, cancellationToken);
                if (steamPrice is not null)
                {
                    steamSuccess++;
                    prices.Add((steamStore, steamPrice.Value.CurrentPrice, steamPrice.Value.RegularPrice));
                    _logger.LogDebug("Steam price ok for {Name} ({SteamId}).", candidate.Name, candidate.SteamId);
                }
                else
                {
                    _logger.LogDebug("Steam price missing for {Name} ({SteamId}).", candidate.Name, candidate.SteamId);
                }
            }

            if (candidate.GogId is not null && stores.TryGetValue("gog", out var gogStore))
            {
                gogAttempts++;
                var gogPrice = await _gog.GetPriceAsync(candidate.GogId, cancellationToken);
                if (gogPrice is not null)
                {
                    gogSuccess++;
                    prices.Add((gogStore, gogPrice.Value.CurrentPrice, gogPrice.Value.RegularPrice));
                    gogSlugForUrl = await _gog.GetSlugAsync(candidate.GogId, cancellationToken) ?? candidate.GogId;
                    _logger.LogDebug("GOG price ok for {Name} ({GogId}), slug={Slug}.", candidate.Name, candidate.GogId, gogSlugForUrl);
                }
                else
                {
                    _logger.LogDebug("GOG price missing for {Name} ({GogId}).", candidate.Name, candidate.GogId);
                }
            }

            if (candidate.EpicId is not null && stores.TryGetValue("epic", out var epicStore))
            {
                epicAttempts++;
                var epicPrice = await _epic.GetPriceAsync(candidate.EpicId, cancellationToken);
                if (epicPrice is not null)
                {
                    epicSuccess++;
                    prices.Add((epicStore, epicPrice.Value.CurrentPrice, epicPrice.Value.RegularPrice));
                    _logger.LogDebug("Epic price ok for {Name} ({EpicId}).", candidate.Name, candidate.EpicId);
                }
                else
                {
                    _logger.LogDebug("Epic price missing for {Name} ({EpicId}).", candidate.Name, candidate.EpicId);
                }
            }

            if (prices.Count < 2)
            {
                skipped++;
                _logger.LogWarning(
                    "Skipping '{Name}' (IGDB {IgdbId}): only {Count}/2 store(s) returned prices. Steam={SteamId} GOG={GogId} Epic={EpicId}.",
                    candidate.Name, candidate.IgdbId, prices.Count, candidate.SteamId, candidate.GogId, candidate.EpicId);
                continue;
            }

            await UpsertGameAsync(candidate, prices, gogSlugForUrl, cancellationToken);
            collected++;
            _logger.LogDebug("Upserted {Collected}/{Target}: {Name}", collected, TargetGameCount, candidate.Name);
        }

        _logger.LogInformation(
            "Price fetch summary. Steam: {SteamSuccess}/{SteamAttempts}, GOG: {GogSuccess}/{GogAttempts}, Epic: {EpicSuccess}/{EpicAttempts}.",
            steamSuccess, steamAttempts, gogSuccess, gogAttempts, epicSuccess, epicAttempts);
        _logger.LogInformation("Aggregation complete. Upserted {Upserted} game(s), skipped {Skipped} candidate(s).",
            collected, skipped);
        return collected;
    }

    private async Task UpsertGameAsync(
        IgdbGameResult candidate,
        List<(Store Store, decimal CurrentPrice, decimal? RegularPrice)> prices,
        string? gogSlugOverride,
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
        UpsertExternalId(game, "gog", gogSlugOverride ?? candidate.GogId);
        UpsertExternalId(game, "epic", candidate.EpicId);

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
