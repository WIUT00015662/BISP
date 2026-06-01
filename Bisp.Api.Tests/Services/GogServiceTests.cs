using Bisp.Api.Services;
using Bisp.Api.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bisp.Api.Tests.Services;

public class GogServiceTests
{
    private static GogService CreateService(HttpResponseMessage response)
    {
        var client = new HttpClient(new FakeHttpMessageHandler(response));
        return new GogService(client, NullLogger<GogService>.Instance);
    }

    [Fact]
    public async Task GetPriceAsync_ParsesCentsString_Correctly()
    {
        var json = """{"prices":{"items":[{"finalPrice":"1999","basePrice":"1999"}]}}""";
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("1207658986");

        Assert.NotNull(result);
        Assert.Equal(19.99m, result.Value.CurrentPrice);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsSalePrice_WhenBaseAndFinalDiffer()
    {
        var json = """{"prices":{"items":[{"finalPrice":"999","basePrice":"2999"}]}}""";
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("1207658986");

        Assert.NotNull(result);
        Assert.Equal(9.99m, result.Value.CurrentPrice);
        Assert.Equal(29.99m, result.Value.RegularPrice);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsNull_OnNon200Response()
    {
        var service = CreateService(FakeHttpMessageHandler.NotFound());

        var result = await service.GetPriceAsync("1207658986");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsNull_WhenPricesItemsEmpty()
    {
        var json = """{"prices":{"items":[]}}""";
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("1207658986");

        Assert.Null(result);
    }
}
