using System.Text.Json;

namespace Bisp.Api.Services;

public sealed class SteamService
{
    private readonly HttpClient _http;
    private readonly ILogger<SteamService> _logger;

    public SteamService(HttpClient http, ILogger<SteamService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(decimal CurrentPrice, decimal? RegularPrice)?> GetPriceAsync(
        string steamAppId,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://store.steampowered.com/api/appdetails?appids={steamAppId}&cc=us&filters=price_overview";
        try
        {
            var json = await _http.GetStringAsync(url, cancellationToken);
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(steamAppId, out var appData)) return null;

            if (!appData.TryGetProperty("success", out var success) || !success.GetBoolean())
            {
                _logger.LogWarning("Steam appid {AppId} returned success:false — no store page.", steamAppId);
                return null;
            }

            if (!appData.TryGetProperty("data", out var data)) return null;

            if (!data.TryGetProperty("price_overview", out var priceOverview))
            {
                _logger.LogInformation("Steam appid {AppId} has no price_overview (likely free-to-play) — skipping.", steamAppId);
                return null;
            }

            // Steam returns prices in cents
            var currentCents = priceOverview.GetProperty("final").GetInt32();
            var regularCents = priceOverview.GetProperty("initial").GetInt32();

            var current = currentCents / 100m;
            var regular = regularCents / 100m;

            return (current, regular != current ? regular : null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Steam price for app {SteamAppId}.", steamAppId);
            return null;
        }
    }
}
