using System.Security.Claims;
using Bisp.Api.Data;
using Bisp.Api.Dtos;
using Bisp.Api.Models;
using Bisp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bisp.Api.Controllers;

[ApiController]
[Route("api/wishlist")]
[Authorize]
public sealed class WishlistController : ControllerBase
{
    private readonly AppDbContext _db;

    public WishlistController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (await RequireSubscriptionAsync(userId, cancellationToken) is { } err) return err;

        var items = await _db.WishlistItems
            .Include(w => w.Game)
                .ThenInclude(g => g!.StorePrices)
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.CreatedUtc)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(w => MapToDto(w)).ToArray());
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddWishlistItemRequest req, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (await RequireSubscriptionAsync(userId, cancellationToken) is { } err) return err;

        var game = await _db.Games.FindAsync([req.GameId], cancellationToken);
        if (game is null) return NotFound(new { error = "Game not found." });

        var duplicate = await _db.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.GameId == req.GameId, cancellationToken);
        if (duplicate) return Conflict(new { error = "Game is already in your wishlist." });

        var item = new WishlistItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = req.GameId,
            MinDiscountPercent = req.MinDiscountPercent,
            CreatedUtc = DateTime.UtcNow
        };

        _db.WishlistItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return StatusCode(201, new { item.Id });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWishlistItemRequest req, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (await RequireSubscriptionAsync(userId, cancellationToken) is { } err) return err;

        var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);
        if (item is null) return NotFound();

        item.MinDiscountPercent = req.MinDiscountPercent;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var item = await _db.WishlistItems.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);
        if (item is null) return NotFound();

        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("User ID claim missing.");

    private async Task<IActionResult?> RequireSubscriptionAsync(string userId, CancellationToken ct)
    {
        var hasActive = await _db.UserSubscriptions
            .AnyAsync(s => s.UserId == userId && s.Status == "active", ct);

        return hasActive ? null : StatusCode(403, new { error = "subscription_required" });
    }

    private WishlistItemDto MapToDto(WishlistItem w)
    {
        var bestDiscount = w.Game?.StorePrices
            .Select(p => PricingService.CalculateDiscountPercent(p.RegularPrice, p.CurrentPrice))
            .Where(d => d is not null)
            .OrderByDescending(d => d)
            .FirstOrDefault();

        return new WishlistItemDto(
            w.Id,
            w.GameId,
            w.Game?.Name ?? string.Empty,
            w.Game?.CoverImageId,
            w.MinDiscountPercent,
            bestDiscount,
            w.CreatedUtc,
            w.LastNotifiedUtc);
    }
}
