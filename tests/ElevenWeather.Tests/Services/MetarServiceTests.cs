using System.Globalization;
using System.Net;
using System.Net.Http;
using ElevenWeather.Models;
using ElevenWeather.Services;
using ElevenWeather.Tests.Helpers;

namespace ElevenWeather.Tests.Services;

public sealed class MetarServiceTests
{
    private const string Host = "aviationweather.gov";

    // Ponto de referência: São Paulo (aprox.)
    private const double SiteLat = -23.55;
    private const double SiteLon = -46.63;

    private static long UnixNowMinus(TimeSpan age) => DateTimeOffset.UtcNow.Subtract(age).ToUnixTimeSeconds();

    private static string Station(string id, double lat, double lon, double temp, double elev, long obsTime) =>
        string.Create(CultureInfo.InvariantCulture,
            $$"""{"icaoId":"{{id}}","name":"{{id}} Airport","lat":{{lat}},"lon":{{lon}},"temp":{{temp}},"elev":{{elev}},"obsTime":{{obsTime}}}""");

    private static MetarObservation Obs(string id, double elevation, double distance) =>
        new(id, id, 20, elevation, distance, DateTime.UtcNow);

    [Fact]
    public async Task FetchNearby_FiltersAndSortsByDistance()
    {
        long fresh = UnixNowMinus(TimeSpan.FromMinutes(20));
        var json = "[" + string.Join(",",
            Station("FAR", -23.80, -46.63, 18, 750, fresh),               // ~28 km
            Station("NEAR", -23.60, -46.63, 19, 760, fresh),              // ~5.5 km
            Station("OLD", -23.56, -46.63, 25, 760, UnixNowMinus(TimeSpan.FromHours(3))),
            Station("EDGE", -23.99, -46.63, 25, 760, fresh),              // ~49 km, ainda dentro do raio
            $$"""{"icaoId":"NOTEMP","lat":-23.55,"lon":-46.63,"elev":760,"obsTime":{{fresh}}}""") + "]";

        var handler = new FakeHttpMessageHandler().OnHost(Host, json);
        var service = new MetarService(handler.CreateClient());

        var result = await service.FetchNearbyAsync(SiteLat, SiteLon);

        Assert.Equal(new[] { "NEAR", "FAR", "EDGE" }, result.Select(o => o.StationId));
        Assert.All(result, o => Assert.True(o.DistanceKm <= 50));
        Assert.Equal(19, result[0].TemperatureC);
        Assert.Equal("NEAR Airport", result[0].StationName);
    }

    [Fact]
    public async Task FetchNearby_DropsStationsOutsideRadiusCorners()
    {
        // O bbox é um quadrado; a quina fica a ~70 km, fora do raio circular de 50 km
        long fresh = UnixNowMinus(TimeSpan.FromMinutes(5));
        var json = "[" + Station("CORNER", SiteLat + 0.44, SiteLon + 0.48, 20, 760, fresh) + "]";
        var handler = new FakeHttpMessageHandler().OnHost(Host, json);

        var result = await new MetarService(handler.CreateClient()).FetchNearbyAsync(SiteLat, SiteLon);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchNearby_SendsInvariantCultureBoundingBox()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try
        {
            var handler = new FakeHttpMessageHandler().OnHost(Host, "[]");
            await new MetarService(handler.CreateClient()).FetchNearbyAsync(SiteLat, SiteLon);

            var query = handler.Requests.Single().RequestUri!.Query;
            Assert.Contains("bbox=-24.", query);
            Assert.DoesNotContain(";", query);
            Assert.Equal(4, Uri.UnescapeDataString(query).Split("bbox=")[1].Split(',').Length);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "[]")]
    [InlineData(HttpStatusCode.OK, "{\"error\":\"x\"}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task FetchNearby_ReturnsEmptyOnBadResponse(HttpStatusCode status, string body)
    {
        var handler = new FakeHttpMessageHandler().OnHost(Host, body, status);

        var result = await new MetarService(handler.CreateClient()).FetchNearbyAsync(SiteLat, SiteLon);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchNearby_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakeHttpMessageHandler().OnHost(Host, "[]");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new MetarService(handler.CreateClient()).FetchNearbyAsync(SiteLat, SiteLon, cts.Token));
    }

    [Fact]
    public void SelectBest_PicksClosestStationAtComparableAltitude()
    {
        var observations = new[] { Obs("HIGH", 1200, 3), Obs("OK", 900, 10), Obs("ALSO_OK", 760, 20) };

        Assert.Equal("OK", MetarService.SelectBest(observations, 760)?.StationId);
    }

    [Fact]
    public void SelectBest_AcceptsExactly300MetersDelta()
    {
        Assert.NotNull(MetarService.SelectBest(new[] { Obs("EDGE", 1060, 1) }, 760));
        Assert.Null(MetarService.SelectBest(new[] { Obs("OVER", 1061, 1) }, 760));
    }

    [Fact]
    public void SelectBest_ReturnsNullWithoutSiteElevationOrStations()
    {
        Assert.Null(MetarService.SelectBest(new[] { Obs("A", 760, 1) }, double.NaN));
        Assert.Null(MetarService.SelectBest(Array.Empty<MetarObservation>(), 760));
    }
}
