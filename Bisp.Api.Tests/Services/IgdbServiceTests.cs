using Bisp.Api.Options;
using Bisp.Api.Services;
using Bisp.Api.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bisp.Api.Tests.Services;

public class IgdbServiceTests
{
    private const string FakeTwitchToken = """{"access_token":"test-token","expires_in":5184000,"token_type":"bearer"}""";

    private static IgdbService CreateService(
        HttpResponseMessage igdbResponse,
        HttpResponseMessage? twitchResponse = null)
    {
        var igdbHandler = new FakeHttpMessageHandler(igdbResponse);
        var igdbClient = new HttpClient(igdbHandler)
        {
            BaseAddress = new Uri("https://api.igdb.com/v4/")
        };

        var twitchResp = twitchResponse ?? FakeHttpMessageHandler.JsonOk(FakeTwitchToken);
        var twitchHandler = new FakeHttpMessageHandler(twitchResp);
        Func<HttpClient> tokenClientFactory = () => new HttpClient(twitchHandler);

        var options = new IgdbOptions { ClientId = "test-client-id", ClientSecret = "test-secret" };
        return new IgdbService(options, igdbClient, NullLogger<IgdbService>.Instance, tokenClientFactory);
    }

    private static string BuildGamesResponse(params (int steamUid, int? gogUid)[] games)
    {
        var elements = games.Select((g, i) =>
        {
            var externalGames = $$"""[{"category":1,"uid":"{{g.steamUid}}"}""";
            if (g.gogUid.HasValue)
                externalGames += $$""",{"category":5,"uid":"{{g.gogUid}}"}""";
            externalGames += "]";

            return $$"""
                {
                  "id":{{1000 + i}},
                  "name":"Game {{i + 1}}",
                  "external_games":{{externalGames}}
                }
                """;
        });

        return $"[{string.Join(",", elements)}]";
    }

    [Fact]
    public async Task GetTopGamesWithStorePresenceAsync_ReturnsCrossPlatformGame()
    {
        var igdbJson = BuildGamesResponse((292030, 1207658986));
        var service = CreateService(FakeHttpMessageHandler.JsonOk(igdbJson));

        var results = await service.GetTopGamesWithStorePresenceAsync(1);

        Assert.Single(results);
        Assert.Equal("292030", results[0].SteamId);
        Assert.Equal("1207658986", results[0].GogId);
    }

    [Fact]
    public async Task GetTopGamesWithStorePresenceAsync_FiltersOut_SteamOnlyGames()
    {
        var igdbJson = BuildGamesResponse((123456, null));
        var service = CreateService(FakeHttpMessageHandler.JsonOk(igdbJson));

        var results = await service.GetTopGamesWithStorePresenceAsync(10);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetTopGamesWithStorePresenceAsync_ReturnsEmpty_WhenTokenFails()
    {
        var service = CreateService(
            igdbResponse: FakeHttpMessageHandler.JsonOk("[]"),
            twitchResponse: FakeHttpMessageHandler.ServerError());

        var results = await service.GetTopGamesWithStorePresenceAsync(10);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetTopGamesWithStorePresenceAsync_ReturnsEmpty_WhenIgdbReturnsError()
    {
        var service = CreateService(FakeHttpMessageHandler.ServerError());

        var results = await service.GetTopGamesWithStorePresenceAsync(10);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetTopGamesWithStorePresenceAsync_StopsAtRequestedLimit()
    {
        // 10 cross-platform games in the response, but we only ask for 2
        var games = Enumerable.Range(0, 10).Select(i => (steamUid: 100000 + i, gogUid: (int?)(200000 + i))).ToArray();
        var igdbJson = BuildGamesResponse(games);
        var service = CreateService(FakeHttpMessageHandler.JsonOk(igdbJson));

        var results = await service.GetTopGamesWithStorePresenceAsync(2);

        Assert.Equal(2, results.Count);
    }
}
