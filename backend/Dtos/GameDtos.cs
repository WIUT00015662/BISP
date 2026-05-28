namespace Bisp.Api.Dtos;

public sealed record StorePriceDto(
    string StoreCode,
    string StoreName,
    decimal CurrentPrice,
    decimal? RegularPrice,
    decimal? DiscountPercent,
    string Currency,
    DateTime LastUpdatedUtc);

public sealed record GameListItemDto(
    Guid Id,
    long IgdbId,
    string Name,
    string[] Genres,
    string? CoverImageId,
    decimal? BestCurrentPrice,
    decimal? BestRegularPrice,
    decimal? BestDiscountPercent,
    int StoreCount);

public sealed record GameDetailDto(
    Guid Id,
    long IgdbId,
    string Name,
    string[] Genres,
    string? CoverImageId,
    string? Summary,
    decimal? BestCurrentPrice,
    decimal? BestRegularPrice,
    decimal? BestDiscountPercent,
    StorePriceDto[] Prices);

public sealed record PagedResult<T>(
    T[] Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
