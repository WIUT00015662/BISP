using System.ComponentModel.DataAnnotations;

namespace Bisp.Api.Dtos;

public sealed record WishlistItemDto(
    Guid Id,
    Guid GameId,
    string GameName,
    string? CoverImageId,
    decimal MinDiscountPercent,
    decimal? CurrentBestDiscount,
    DateTime CreatedUtc,
    DateTime? LastNotifiedUtc);

public sealed record AddWishlistItemRequest(
    [Required] Guid GameId,
    [Required, Range(1, 99)] decimal MinDiscountPercent);

public sealed record UpdateWishlistItemRequest(
    [Required, Range(1, 99)] decimal MinDiscountPercent);
