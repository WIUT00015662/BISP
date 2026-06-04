using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bisp.Api.Options;

namespace Bisp.Api.Services;

public sealed class IgdbGameResult
{
    public long IgdbId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Genres { get; init; }
    public string? CoverImageId { get; init; }
    public string? Summary { get; init; }
    public string? SteamId { get; init; }
    public string? GogId { get; init; }
    public string? EpicId { get; init; }
}

public sealed class IgdbService
{
    private readonly IgdbOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<IgdbService> _logger;

    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly Func<HttpClient> _tokenClientFactory;

    private const int SteamCategory = 1;
    private const int GogCategory = 5;
    private const int EpicCategory = 26;

    public IgdbService(IgdbOptions options, HttpClient http, ILogger<IgdbService> logger,
        Func<HttpClient>? tokenClientFactory = null)
    {
        _options = options;
        _http = http;
        _logger = logger;
        _tokenClientFactory = tokenClientFactory ?? (() => new HttpClient());
    }

    public async Task<IReadOnlyList<IgdbGameResult>> GetTopGamesWithStorePresenceAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("IGDB query start for games. Limit={Limit}, BatchSize=50, MaxOffset=500.", limit);

        var token = await GetAccessTokenAsync(cancellationToken);
        if (token is null)
        {
            _logger.LogError("Cannot query IGDB: no access token.");
            return [];
        }

        var results = new List<IgdbGameResult>();
        int offset = 0;
        const int batchSize = 50;
        int pages = 0;

        while (results.Count < limit && offset < 500)
        {
            // Step 1: fetch popular main games — external_games returns as plain integer IDs
            var gameQuery = $"""
                fields id,name,genres.name,cover.image_id,summary,external_games;
                where game_type = 0 & version_parent = null & external_games != null;
                sort total_rating_count desc;
                limit {batchSize};
                offset {offset};
                """;

            var gamesJson = await PostAsync("games", gameQuery, token, cancellationToken);
            if (gamesJson is null) break;

            var games = JsonSerializer.Deserialize<JsonElement[]>(gamesJson);
            if (games is null || games.Length == 0) break;
            pages++;

            _logger.LogDebug("IGDB page {Page} returned {Count} games.", pages, games.Length);

            // Collect external_game integer IDs from every game in this page
            var gameElements = new Dictionary<long, JsonElement>();
            var externalIds = new List<long>();

            foreach (var game in games)
            {
                var igdbId = game.GetProperty("id").GetInt64();
                gameElements[igdbId] = game;

                if (!game.TryGetProperty("external_games", out var extArr)) continue;
                foreach (var el in extArr.EnumerateArray())
                {
                    // Non-expanded field → plain integer
                    if (el.ValueKind == JsonValueKind.Number)
                        externalIds.Add(el.GetInt64());
                }
            }

            if (externalIds.Count == 0) { offset += batchSize; continue; }

            // Step 2: resolve those IDs, then filter Steam (1), GOG (5), Epic (26) in-memory
            var idList = string.Join(",", externalIds);
            var extQuery = $"""
                fields id,category,external_game_source,uid,game;
                where id = ({idList});
                limit 500;
                """;

            var extJson = await PostAsync("external_games", extQuery, token, cancellationToken);
            if (extJson is null) { offset += batchSize; continue; }

            var externalGames = JsonSerializer.Deserialize<JsonElement[]>(extJson);
            if (externalGames is null) { offset += batchSize; continue; }

            // Map igdbGameId → Steam/GOG/Epic uid
            var steamMap = new Dictionary<long, string>();
            var gogMap = new Dictionary<long, string>();
            var epicMap = new Dictionary<long, string>();

            foreach (var ext in externalGames)
            {
                if (!ext.TryGetProperty("game", out var gameEl)) continue;
                if (!TryGetExternalGameSource(ext, out var source)) continue;
                if (!ext.TryGetProperty("uid", out var uidEl)) continue;

                var gameId = gameEl.GetInt64();
                var uid = uidEl.GetString();
                if (uid is null) continue;

                switch (source)
                {
                    case SteamCategory: steamMap[gameId] = uid; break;
                    case GogCategory: gogMap[gameId] = uid; break;
                    case EpicCategory: epicMap[gameId] = uid; break;
                }
            }

            // Build results — require at least Steam; GOG/Epic optional (price gate in AggregationService handles it)
            foreach (var (igdbId, gameEl) in gameElements)
            {
                if (results.Count >= limit) break;
                if (!steamMap.TryGetValue(igdbId, out var steamId)) continue;

                gogMap.TryGetValue(igdbId, out var gogId);
                epicMap.TryGetValue(igdbId, out var epicId);
                var name = gameEl.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;

                if (gogId is null && epicId is null)
                    _logger.LogDebug("'{Name}' has Steam but no GOG/Epic — will likely fail price gate.", name);

                results.Add(BuildResult(igdbId, gameEl, name, steamId, gogId, epicId));
            }

            offset += batchSize;
        }

        var gogCount = results.Count(r => r.GogId is not null);
        var epicCount = results.Count(r => r.EpicId is not null);
        var steamOnlyCount = results.Count(r => r.GogId is null && r.EpicId is null);
        var allThree = results.Count(r => r.GogId is not null && r.EpicId is not null);
        var atLeastTwo = results.Count - steamOnlyCount;
        _logger.LogInformation(
            "IGDB complete in {ElapsedMs}ms. Candidates: {Total} from {Pages} page(s). Steam-only: {SteamOnly}, Steam+GOG: {GogCount}, Steam+Epic: {EpicCount}, All three: {AllThree}, At least two stores: {AtLeastTwo}.",
            stopwatch.ElapsedMilliseconds, results.Count, pages, steamOnlyCount, gogCount, epicCount, allThree, atLeastTwo);

        return results;
    }

    private static IgdbGameResult BuildResult(
        long igdbId,
        JsonElement game,
        string name,
        string steamId,
        string? gogId,
        string? epicId)
    {
        string? genres = null;
        if (game.TryGetProperty("genres", out var genresEl))
        {
            var names = genresEl.EnumerateArray()
                .Select(g => g.TryGetProperty("name", out var gn) ? gn.GetString() : null)
                .Where(g => g is not null)
                .ToList();
            if (names.Count > 0) genres = string.Join(", ", names);
        }

        string? coverId = null;
        if (game.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object)
            coverId = cover.TryGetProperty("image_id", out var img) ? img.GetString() : null;

        string? summary = game.TryGetProperty("summary", out var sum) ? sum.GetString() : null;

        return new IgdbGameResult
        {
            IgdbId = igdbId,
            Name = name,
            Genres = genres,
            CoverImageId = coverId,
            Summary = summary,
            SteamId = steamId,
            GogId = gogId,
            EpicId = epicId
        };
    }

    private static bool TryGetExternalGameSource(JsonElement externalGame, out int source)
    {
        if (externalGame.TryGetProperty("category", out var categoryEl))
        {
            source = categoryEl.GetInt32();
            return true;
        }

        if (externalGame.TryGetProperty("external_game_source", out var sourceEl))
        {
            source = sourceEl.GetInt32();
            return true;
        }

        source = default;
        return false;
    }

    private async Task<string?> PostAsync(string endpoint, string body, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Client-ID", _options.ClientId);
        request.Content = new StringContent(body, Encoding.UTF8, "text/plain");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IGDB HTTP request to '{Endpoint}' failed.", endpoint);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("IGDB '{Endpoint}' returned {StatusCode}. Body: {Body}",
                endpoint, response.StatusCode, err[..Math.Min(500, err.Length)]);
            return null;
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
            return _accessToken;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
                return _accessToken;

            using var tokenClient = _tokenClientFactory();
            var tokenUrl = $"https://id.twitch.tv/oauth2/token" +
                           $"?client_id={_options.ClientId}" +
                           $"&client_secret={_options.ClientSecret}" +
                           $"&grant_type=client_credentials";

            var resp = await tokenClient.PostAsync(tokenUrl, null, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get IGDB access token: {StatusCode}. Body: {Body}",
                    resp.StatusCode, body);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

            _logger.LogInformation("IGDB access token acquired, expires in {ExpiresIn}s.", expiresIn);
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
