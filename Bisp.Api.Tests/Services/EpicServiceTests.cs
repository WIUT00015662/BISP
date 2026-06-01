using Bisp.Api.Services;
using Bisp.Api.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bisp.Api.Tests.Services;

public class EpicServiceTests
{
    private static EpicService CreateService(HttpResponseMessage response)
    {
        var client = new HttpClient(new FakeHttpMessageHandler(response));
        return new EpicService(client, NullLogger<EpicService>.Instance);
    }

    private static string BuildGraphQlResponse(string productSlug, string urlSlug, int discount, int original) => $$"""
        {
          "data": {
            "Catalog": {
              "searchStore": {
                "elements": [
                  {
                    "title": "Test Game",
                    "productSlug": "{{productSlug}}",
                    "urlSlug": "{{urlSlug}}",
                    "price": {
                      "totalPrice": {
                        "discountPrice": {{discount}},
                        "originalPrice": {{original}}
                      }
                    }
                  }
                ]
              }
            }
          }
        }
        """;

    [Fact]
    public async Task GetPriceAsync_MatchesByProductSlug()
    {
        var json = BuildGraphQlResponse("cyberpunk-2077/home", "cyberpunk-2077", 3999, 5999);
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("cyberpunk-2077");

        Assert.NotNull(result);
        Assert.Equal(39.99m, result.Value.CurrentPrice);
    }

    [Fact]
    public async Task GetPriceAsync_MatchesByUrlSlug_WhenProductSlugAbsent()
    {
        var json = BuildGraphQlResponse("", "some-game", 1999, 1999);
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("some-game");

        Assert.NotNull(result);
        Assert.Equal(19.99m, result.Value.CurrentPrice);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsSalePrice_WhenDiscountPriceDiffers()
    {
        var json = BuildGraphQlResponse("witcher-3/home", "witcher-3", 999, 3999);
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("witcher-3");

        Assert.NotNull(result);
        Assert.Equal(9.99m, result.Value.CurrentPrice);
        Assert.Equal(39.99m, result.Value.RegularPrice);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsNull_WhenBothPricesZero()
    {
        var json = BuildGraphQlResponse("free-game", "free-game", 0, 0);
        var service = CreateService(FakeHttpMessageHandler.JsonOk(json));

        var result = await service.GetPriceAsync("free-game");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsNull_OnNon200Response()
    {
        var service = CreateService(FakeHttpMessageHandler.ServerError());

        var result = await service.GetPriceAsync("some-game");

        Assert.Null(result);
    }
}
