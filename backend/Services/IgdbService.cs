using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bisp.Api.Options;
using Microsoft.Extensions.Options;

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
    public string? EpicSlug { get; init; }
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

    // IGDB external_games categories
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
        var token = await GetAccessTokenAsync(cancellationToken);
        if (token is null)
        {
            _logger.LogError("Cannot query IGDB: no access token.");
            return [];
        }

        // Fetch in batches of 50 (IGDB max), try up to 3 pages to find enough cross-platform games
        var results = new List<IgdbGameResult>();
        int offset = 0;
        const int batchSize = 50;

        while (results.Count < limit && offset < 500)
        {
            var query = $"""
                fields id,name,genres.name,cover.image_id,summary,external_games.category,external_games.uid;
                where external_games.category = ({SteamCategory},{GogCategory},{EpicCategory}) & category = 0 & version_parent = null;
                sort total_rating_count desc;
                limit {batchSize};
                offset {offset};
                """;

            using var request = new HttpRequestMessage(HttpMethod.Post, "games");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Client-ID", _options.ClientId);
            request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IGDB HTTP request failed.");
                break;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("IGDB returned {StatusCode}", response.StatusCode);
                break;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var games = JsonSerializer.Deserialize<JsonElement[]>(json);
            if (games is null || games.Length == 0) break;

            foreach (var game in games)
            {
                var parsed = ParseGame(game);
                if (parsed is not null)
                    results.Add(parsed);
                if (results.Count >= limit) break;
            }

            offset += batchSize;
        }

        _logger.LogInformation("IGDB returned {Count} cross-platform candidate games.", results.Count);
        return results;
    }

    private static IgdbGameResult? ParseGame(JsonElement game)
    {
        if (!game.TryGetProperty("external_games", out var externalGames))
            return null;

        string? steamId = null, gogId = null, epicSlug = null;

        foreach (var ext in externalGames.EnumerateArray())
        {
            if (!ext.TryGetProperty("category", out var cat)) continue;
            var uid = ext.TryGetProperty("uid", out var u) ? u.GetString() : null;

            switch (cat.GetInt32())
            {
                case SteamCategory: steamId = uid; break;
                case GogCategory: gogId = uid; break;
                case EpicCategory: epicSlug = uid; break;
            }
        }

        // Must have Steam + at least one other store
        if (steamId is null) return null;
        if (gogId is null && epicSlug is null) return null;

        var id = game.GetProperty("id").GetInt64();
        var name = game.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;

        string? genres = null;
        if (game.TryGetProperty("genres", out var genresEl))
        {
            var genreNames = genresEl.EnumerateArray()
                .Select(g => g.TryGetProperty("name", out var gn) ? gn.GetString() : null)
                .Where(g => g is not null)
                .ToList();
            if (genreNames.Count > 0)
                genres = string.Join(", ", genreNames);
        }

        string? coverId = null;
        if (game.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object)
            coverId = cover.TryGetProperty("image_id", out var img) ? img.GetString() : null;

        string? summary = null;
        if (game.TryGetProperty("summary", out var sum))
            summary = sum.GetString();

        return new IgdbGameResult
        {
            IgdbId = id,
            Name = name,
            Genres = genres,
            CoverImageId = coverId,
            Summary = summary,
            SteamId = steamId,
            GogId = gogId,
            EpicSlug = epicSlug
        };
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
                _logger.LogError("Failed to get IGDB access token: {StatusCode}", resp.StatusCode);
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
