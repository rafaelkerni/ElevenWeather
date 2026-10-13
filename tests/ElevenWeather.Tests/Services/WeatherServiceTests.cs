using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using ElevenWeather.Services;
using ElevenWeather.Tests.Helpers;

namespace ElevenWeather.Tests.Services;

public sealed class WeatherServiceTests
{
    private const string ForecastHost = "api.open-meteo.com";
    private const string AqiHost = "air-quality-api.open-meteo.com";
    private const string MetarHost = "aviationweather.gov";

    private const double Lat = -23.55;
    private const double Lon = -46.63;

    private static string ForecastJson(double temp = 20, double feelsLike = 18, double? elevation = 760, int hoursBack = 3, int hoursTotal = 48)
    {
        // Horários começam no passado para exercitar o descarte de horas que já passaram
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(-hoursBack);
        var hours = Enumerable.Range(0, hoursTotal).Select(i => start.AddHours(i)).ToArray();

        var payload = new Dictionary<string, object>
        {
            ["current"] = new { temperature_2m = temp, apparent_temperature = feelsLike, weather_code = 3, is_day = 1 },
            ["daily"] = new
            {
                time = new[] { "2026-10-13", "2026-10-14" },
                weather_code = new[] { 61, 0 },
                temperature_2m_max = new[] { 25.4, 28.1 },
                temperature_2m_min = new[] { 15.2, 16.0 },
            },
            ["hourly"] = new
            {
                time = hours.Select(h => h.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)),
                weather_code = hours.Select(_ => 2),
                temperature_2m = hours.Select(h => (double)h.Hour),
                precipitation_probability = hours.Select(_ => 40),
                is_day = hours.Select(h => h.Hour is >= 6 and < 18 ? 1 : 0),
            },
        };
        if (elevation is { } e) payload["elevation"] = e;
        return JsonSerializer.Serialize(payload);
    }

    private static string MetarJson(double temp, double elevation) =>
        string.Create(CultureInfo.InvariantCulture,
            $$"""[{"icaoId":"SBSP","name":"Congonhas","lat":-23.62,"lon":-46.65,"temp":{{temp}},"elev":{{elevation}},"obsTime":{{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}}]""");

    private static WeatherService CreateService(FakeHttpMessageHandler handler)
    {
        var client = handler.CreateClient();
        return new WeatherService(client, new MetarService(client));
    }

    [Fact]
    public async Task Fetch_ParsesForecastAndAqi()
    {
        var handler = new FakeHttpMessageHandler()
            .OnHost(ForecastHost, ForecastJson())
            .OnHost(AqiHost, """{"current":{"us_aqi":42}}""");

        var snapshot = await CreateService(handler).FetchAsync(Lat, Lon, "São Paulo");

        Assert.NotNull(snapshot);
        Assert.Equal("São Paulo", snapshot.CityName);
        Assert.Equal(20, snapshot.TemperatureC);
        Assert.Equal(18, snapshot.ApparentTemperatureC);
        Assert.Equal(3, snapshot.WeatherCode);
        Assert.True(snapshot.IsDay);
        Assert.Equal(42, snapshot.UsAqi);
        Assert.Null(snapshot.ObservationStation);

        Assert.Equal(2, snapshot.Daily.Count);
        Assert.Equal(new DateOnly(2026, 10, 13), snapshot.Daily[0].Date);
        Assert.Equal(61, snapshot.Daily[0].WeatherCode);
        Assert.Equal(28.1, snapshot.Daily[1].TempMaxC);
    }

    [Fact]
    public async Task Fetch_KeepsAtMost24UpcomingHours()
    {
        var handler = new FakeHttpMessageHandler().OnHost(ForecastHost, ForecastJson(hoursBack: 3, hoursTotal: 48));

        var snapshot = await CreateService(handler).FetchAsync(Lat, Lon, "X");

        var now = DateTime.Now;
        var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
        Assert.Equal(24, snapshot!.Hourly.Count);
        Assert.Equal(currentHour, snapshot.Hourly[0].Time);
        Assert.All(snapshot.Hourly, h => Assert.True(h.Time >= currentHour));
        Assert.Equal(40, snapshot.Hourly[0].PrecipitationProbability);
    }

    [Fact]
    public async Task Fetch_CorrectsTemperatureWithNearbyMetar()
    {
        var handler = new FakeHttpMessageHandler()
            .OnHost(ForecastHost, ForecastJson(temp: 20, feelsLike: 18, elevation: 760))
            .OnHost(MetarHost, MetarJson(temp: 14, elevation: 802));

        var snapshot = await CreateService(handler).FetchAsync(Lat, Lon, "X");

        Assert.Equal(14, snapshot!.TemperatureC);
        // A sensação térmica acompanha o deslocamento da temperatura do ar
        Assert.Equal(12, snapshot.ApparentTemperatureC);
        Assert.Equal("Congonhas", snapshot.ObservationStation);
    }

    [Fact]
    public async Task Fetch_IgnoresMetarAtDifferentAltitude()
    {
        var handler = new FakeHttpMessageHandler()
            .OnHost(ForecastHost, ForecastJson(temp: 20, elevation: 760))
            .OnHost(MetarHost, MetarJson(temp: 26, elevation: 5));

        var snapshot = await CreateService(handler).FetchAsync(Lat, Lon, "X");

        Assert.Equal(20, snapshot!.TemperatureC);
        Assert.Null(snapshot.ObservationStation);
    }

    [Fact]
    public async Task Fetch_IgnoresMetarWhenSiteElevationIsUnknown()
    {
        var handler = new FakeHttpMessageHandler()
            .OnHost(ForecastHost, ForecastJson(temp: 20, elevation: null))
            .OnHost(MetarHost, MetarJson(temp: 14, elevation: 760));

        var snapshot = await CreateService(handler).FetchAsync(Lat, Lon, "X");

        Assert.Equal(20, snapshot!.TemperatureC);
    }

    [Fact]
    public async Task Fetch_SucceedsWhenOnlyAqiFails()
    {
        var handler = new FakeHttpMessageHandler()
            .OnHost(ForecastHost, ForecastJson())
            .OnHost(AqiHost, "garbage");

        var snapshot = await CreateService(handler).FetchAsync(Lat, Lon, "X");

        Assert.NotNull(snapshot);
        Assert.Null(snapshot.UsAqi);
    }

    [Fact]
    public async Task Fetch_ReturnsNullOnFirstFailure()
    {
        var handler = new FakeHttpMessageHandler().OnHost(ForecastHost, "{}", HttpStatusCode.InternalServerError);

        var service = CreateService(handler);

        Assert.Null(await service.FetchAsync(Lat, Lon, "X"));
        Assert.Null(service.LastKnownGood);
    }

    [Fact]
    public async Task Fetch_FallsBackToLastKnownGoodOnFailure()
    {
        bool online = true;
        var handler = new FakeHttpMessageHandler().OnHost(ForecastHost, _ => online
            ? FakeHttpMessageHandler.Json(ForecastJson(temp: 21))
            : throw new HttpRequestException("offline"));
        var service = CreateService(handler);

        var first = await service.FetchAsync(Lat, Lon, "X");
        online = false;
        var second = await service.FetchAsync(Lat, Lon, "X");

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Same(first, service.LastKnownGood);
    }

    [Fact]
    public async Task Fetch_FallsBackToLastKnownGoodOnMalformedJson()
    {
        var body = ForecastJson();
        var handler = new FakeHttpMessageHandler().OnHost(ForecastHost, _ => FakeHttpMessageHandler.Json(body));
        var service = CreateService(handler);

        var first = await service.FetchAsync(Lat, Lon, "X");
        body = "{ broken";
        var second = await service.FetchAsync(Lat, Lon, "X");

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"current":{"temperature_2m":"20"}}""")]
    [InlineData("""{"current":{"temperature_2m":20,"weather_code":3,"is_day":1},"daily":{"time":["2026-10-13"]}}""")]
    public async Task Fetch_FallsBackToLastKnownGoodOnUnexpectedSchema(string unexpectedBody)
    {
        var body = ForecastJson();
        var handler = new FakeHttpMessageHandler().OnHost(ForecastHost, _ => FakeHttpMessageHandler.Json(body));
        var service = CreateService(handler);

        var first = await service.FetchAsync(Lat, Lon, "X");
        body = unexpectedBody;
        var second = await service.FetchAsync(Lat, Lon, "X");

        Assert.Same(first, second);
    }

    [Fact]
    public async Task Fetch_SucceedsWhenAqiHasUnexpectedSchema()
    {
        var handler = new FakeHttpMessageHandler()
            .OnHost(ForecastHost, ForecastJson())
            .OnHost(AqiHost, """{"error":true}""");

        var snapshot = await CreateService(handler).FetchAsync(Lat, Lon, "X");

        Assert.NotNull(snapshot);
        Assert.Null(snapshot.UsAqi);
    }

    [Fact]
    public async Task Fetch_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakeHttpMessageHandler().OnHost(ForecastHost, ForecastJson());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(handler).FetchAsync(Lat, Lon, "X", cts.Token));
    }

    [Fact]
    public async Task Fetch_SendsInvariantCultureCoordinates()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try
        {
            var handler = new FakeHttpMessageHandler().OnHost(ForecastHost, ForecastJson());
            await CreateService(handler).FetchAsync(Lat, Lon, "X");

            var forecastUri = handler.Requests.Single(r => r.RequestUri!.Host == ForecastHost).RequestUri!;
            Assert.Contains("latitude=-23.55&longitude=-46.63", forecastUri.Query);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
