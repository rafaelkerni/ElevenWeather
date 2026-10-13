using System.Net.Http;
using System.Text.Json;
using ElevenWeather.Models;

namespace ElevenWeather.Services;

public sealed class LocationService : ILocationService
{
    private readonly HttpClient _http;

    public LocationService(HttpClient http) => _http = http;

    /// <summary>IP-based geolocation, used only on first run (no API key, no signup).</summary>
    public async Task<(double Lat, double Lon, string City)?> DetectByIpAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync("https://ipwho.is/", ct);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var successEl) && successEl.ValueKind == JsonValueKind.False)
                return null;

            double lat = root.GetProperty("latitude").GetDouble();
            double lon = root.GetProperty("longitude").GetDouble();
            string city = root.TryGetProperty("city", out var cityEl) ? cityEl.GetString() ?? "" : "";
            return (lat, lon, city);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (FetchErrors.IsRecoverable(ex))
        {
            return null;
        }
    }

    /// <summary>City search for manual location configuration.</summary>
    public async Task<IReadOnlyList<GeocodingResult>> SearchCityAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<GeocodingResult>();

        string language = AppLocalization.Instance.Current switch
        {
            AppLanguage.Portuguese => "pt",
            AppLanguage.Spanish => "es",
            _ => "en",
        };
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&language={language}&count=8";
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<GeocodingResult>();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("results", out var results))
                return Array.Empty<GeocodingResult>();

            var list = new List<GeocodingResult>();
            foreach (var item in results.EnumerateArray())
            {
                list.Add(new GeocodingResult(
                    item.GetProperty("name").GetString() ?? "",
                    item.TryGetProperty("admin1", out var admin1) ? admin1.GetString() : null,
                    item.TryGetProperty("country", out var country) ? country.GetString() ?? "" : "",
                    item.GetProperty("latitude").GetDouble(),
                    item.GetProperty("longitude").GetDouble()));
            }
            return list;
        }
        catch (Exception ex) when (FetchErrors.IsRecoverable(ex))
        {
            return Array.Empty<GeocodingResult>();
        }
    }
}
