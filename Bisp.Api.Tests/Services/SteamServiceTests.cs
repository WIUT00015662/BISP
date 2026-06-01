using Bisp.Api.Services;
using Bisp.Api.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bisp.Api.Tests.Services;

public class SteamServiceTests
{
    private static SteamService CreateService(HttpResponseMessage response)
    {
        var client = new HttpClient(new FakeHttpMessageHandler(response));
        return new SteamService(client, NullLogger<SteamService>.Instance);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsSalePrice_WhenDiscountActive()
    {
        var json = """{"123456":{"success":true,"data":{"price_overview":{"final":1999,"initial":2999}}}}""";
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("123456");

        Assert.NotNull(result);
        Assert.Equal(19.99m, result.Value.CurrentPrice);
        Assert.Equal(29.99m, result.Value.RegularPrice);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsNullRegularPrice_WhenNoSale()
    {
        var json = """{"456":{"success":true,"data":{"price_overview":{"final":1999,"initial":1999}}}}""";
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("456");

        Assert.NotNull(result);
        Assert.Equal(19.99m, result.Value.CurrentPrice);
        Assert.Null(result.Value.RegularPrice);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsNull_OnNon200Response()
    {
        var service = CreateService(FakeHttpMessageHandler.NotFound());

        var result = await service.GetPriceAsync("123456");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsNull_WhenPriceOverviewMissing()
    {
        var json = """{"123456":{"success":true,"data":{}}}""";
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("123456");

        Assert.Null(result);
    }
}
