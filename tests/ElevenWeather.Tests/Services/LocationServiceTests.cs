using System.Net;
using ElevenWeather.Models;
using ElevenWeather.Services;
using ElevenWeather.Tests.Helpers;

namespace ElevenWeather.Tests.Services;

public sealed class LocationServiceTests : PreferenceScope
{
    private const string IpHost = "ipwho.is";
    private const string GeocodingHost = "geocoding-api.open-meteo.com";

    [Fact]
    public async Task DetectByIp_ParsesCoordinatesAndCity()
    {
        var handler = new FakeHttpMessageHandler()
            .OnHost(IpHost, """{"success":true,"latitude":-22.9,"longitude":-47.06,"city":"Campinas"}""");

        var result = await new LocationService(handler.CreateClient()).DetectByIpAsync();

        Assert.Equal((-22.9, -47.06, "Campinas"), result);
    }

    [Fact]
    public async Task DetectByIp_AllowsMissingCity()
    {
        var handler = new FakeHttpMessageHandler().OnHost(IpHost, """{"latitude":1,"longitude":2}""");

        var result = await new LocationService(handler.CreateClient()).DetectByIpAsync();

        Assert.Equal("", result?.City);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"success":false,"message":"Reserved range"}""")]
    [InlineData(HttpStatusCode.OK, """{"success":true}""")]
    [InlineData(HttpStatusCode.OK, """{"latitude":"x","longitude":1}""")]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.OK, "<html>")]
    public async Task DetectByIp_ReturnsNullOnFailure(HttpStatusCode status, string body)
    {
        var handler = new FakeHttpMessageHandler().OnHost(IpHost, body, status);

        Assert.Null(await new LocationService(handler.CreateClient()).DetectByIpAsync());
    }

    [Fact]
    public async Task SearchCity_ParsesResults()
    {
        var handler = new FakeHttpMessageHandler().OnHost(GeocodingHost, """
            {"results":[
              {"name":"Campinas","admin1":"São Paulo","country":"Brasil","latitude":-22.9,"longitude":-47.06},
              {"name":"Monaco","country":"Monaco","latitude":43.73,"longitude":7.42}
            ]}
            """);

        var results = await new LocationService(handler.CreateClient()).SearchCityAsync("camp");

        Assert.Equal(2, results.Count);
        Assert.Equal(new GeocodingResult("Campinas", "São Paulo", "Brasil", -22.9, -47.06), results[0]);
        Assert.Null(results[1].Admin1);
    }

    [Fact]
    public async Task SearchCity_ReturnsEmptyWhenNoResultsKey()
    {
        var handler = new FakeHttpMessageHandler().OnHost(GeocodingHost, """{"generationtime_ms":0.5}""");

        Assert.Empty(await new LocationService(handler.CreateClient()).SearchCityAsync("zzzz"));
    }

    [Theory]
    [InlineData("""{"results":[{"name":"Campinas","country":"Brasil"}]}""")]
    [InlineData("""{"results":[{"name":"Campinas","latitude":"x","longitude":1}]}""")]
    [InlineData("""{"results":{}}""")]
    public async Task SearchCity_ReturnsEmptyOnUnexpectedSchema(string body)
    {
        var handler = new FakeHttpMessageHandler().OnHost(GeocodingHost, body);

        Assert.Empty(await new LocationService(handler.CreateClient()).SearchCityAsync("camp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchCity_SkipsRequestForBlankQuery(string query)
    {
        var handler = new FakeHttpMessageHandler();

        Assert.Empty(await new LocationService(handler.CreateClient()).SearchCityAsync(query));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(AppLanguage.Portuguese, "language=pt")]
    [InlineData(AppLanguage.English, "language=en")]
    [InlineData(AppLanguage.Spanish, "language=es")]
    public async Task SearchCity_UsesCurrentLanguageAndEscapesQuery(AppLanguage language, string expectedParam)
    {
        AppLocalization.Instance.Current = language;
        var handler = new FakeHttpMessageHandler().OnHost(GeocodingHost, "{}");

        await new LocationService(handler.CreateClient()).SearchCityAsync("São Paulo & cia");

        var query = handler.Requests.Single().RequestUri!.Query;
        Assert.Contains(expectedParam, query);
        Assert.Contains("name=S%C3%A3o%20Paulo%20%26%20cia", query);
    }
}
