using Bisp.Api.Data;
using Bisp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bisp.Api.Services;

public sealed class NotificationService
{
    private readonly AppDbContext _db;
    private readonly EmailSender _emailSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext db,
        EmailSender emailSender,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task CheckWishlistAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);

        var items = await _db.WishlistItems
            .Include(w => w.User)
            .Include(w => w.Game)
                .ThenInclude(g => g!.StorePrices)
                    .ThenInclude(p => p.Store)
            .Include(w => w.Game)
                .ThenInclude(g => g!.ExternalGameIds)
            .Where(w =>
                w.LastNotifiedUtc == null || w.LastNotifiedUtc < cutoff)
            .Where(w => _db.UserSubscriptions.Any(s =>
                s.UserId == w.UserId && s.Status == "active"))
            .ToListAsync(cancellationToken);

        if (items.Count == 0) return;

        var now = DateTime.UtcNow;
        var notifications = new List<NotificationLog>();

        foreach (var item in items)
        {
            if (item.Game is null || item.User is null) continue;

            var bestDiscount = item.Game.StorePrices
                .Select(p => (
                    Price: p,
                    Discount: PricingService.CalculateDiscountPercent(p.RegularPrice, p.CurrentPrice)
                ))
                .Where(x => x.Discount is not null)
                .OrderByDescending(x => x.Discount)
                .FirstOrDefault();

            if (bestDiscount.Discount is null || bestDiscount.Discount < item.MinDiscountPercent)
                continue;

            var store = bestDiscount.Price.Store;
            var storeCode = store?.Code;
            var externalId = item.Game.ExternalGameIds
                .FirstOrDefault(e => e.Provider == storeCode)?.ExternalId;
            var storeUrl = BuildStoreUrl(storeCode, externalId);

            var subject = $"🎮 {item.Game.Name} is {bestDiscount.Discount:F0}% off on {store?.Name ?? "a store"}!";
            var body = BuildEmailBody(item.Game.Name, store?.Name ?? "Store", bestDiscount.Price, bestDiscount.Discount.Value, item.MinDiscountPercent, storeUrl);

            await _emailSender.SendAsync(item.User.Email!, subject, body, cancellationToken);

            item.LastNotifiedUtc = now;
            notifications.Add(new NotificationLog
            {
                Id = Guid.NewGuid(),
                WishlistItemId = item.Id,
                DiscountPercent = bestDiscount.Discount.Value,
                SentUtc = now
            });

            _logger.LogInformation(
                "Notified {Email} about {Game} at {Discount}% off on {Store}.",
                item.User.Email, item.Game.Name, bestDiscount.Discount.Value, store?.Name);
        }

        if (notifications.Count > 0)
        {
            _db.NotificationLogs.AddRange(notifications);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string BuildEmailBody(
        string gameName, string storeName, GameStorePrice price,
        decimal discount, decimal minDiscountPercent, string? storeUrl)
    {
        var storeButton = storeUrl is null ? "" :
            $"""<p><a href="{storeUrl}" style="display:inline-block;padding:10px 20px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold">View on {storeName}</a></p>""";

        return $"""
            <html><body style="font-family:sans-serif;max-width:600px;margin:auto;padding:20px">
              <h2 style="color:#2563eb">Price Alert: {gameName}</h2>
              <p><strong>{gameName}</strong> is now <span style="color:#16a34a;font-size:1.4em;font-weight:bold">{discount:F0}% OFF</span> on {storeName}!</p>
              <table style="border-collapse:collapse;width:100%;margin:16px 0">
                <tr style="background:#f3f4f6">
                  <td style="padding:8px;border:1px solid #e5e7eb">Regular Price</td>
                  <td style="padding:8px;border:1px solid #e5e7eb;text-decoration:line-through">${price.RegularPrice:F2}</td>
                </tr>
                <tr>
                  <td style="padding:8px;border:1px solid #e5e7eb">Sale Price</td>
                  <td style="padding:8px;border:1px solid #e5e7eb;color:#16a34a;font-weight:bold">${price.CurrentPrice:F2}</td>
                </tr>
              </table>
              {storeButton}
              <p style="color:#6b7280;font-size:0.85em">You set a discount threshold of {minDiscountPercent:F0}% for this game. Unsubscribe from wishlist notifications by removing this game from your wishlist.</p>
            </body></html>
            """;
    }

    private static string? BuildStoreUrl(string? storeCode, string? externalId)
    {
        if (string.IsNullOrWhiteSpace(storeCode) || string.IsNullOrWhiteSpace(externalId)) return null;
        return storeCode switch
        {
            "steam" => $"https://store.steampowered.com/app/{externalId}",
            "gog"   => $"https://www.gog.com/en/game/{externalId}",
            "epic"  => $"https://store.epicgames.com/en-US/p/{externalId}",
            _       => null
        };
    }
}
