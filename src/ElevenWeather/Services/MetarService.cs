using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ElevenWeather.Models;

namespace ElevenWeather.Services;

/// <summary>
/// Observed temperature from nearby airport stations (METAR), used to correct the current reading.
/// Open-Meteo's "current" block is model output, which can sit several degrees off during fog or
/// thermal inversions; a real observation keeps the number on the taskbar honest.
/// </summary>
public sealed class MetarService
{
    private const double SearchRadiusKm = 50;

    /// <summary>Terrain drives temperature, so a station far above or below the site is worse than no station.</summary>
    private const double MaxElevationDeltaMeters = 300;

    private static readonly TimeSpan MaxObservationAge = TimeSpan.FromHours(2);

    private readonly HttpClient _http;

    public MetarService(HttpClient http) => _http = http;

    /// <summary>Recent observations within the search radius, closest first. Empty when none qualify.</summary>
    public async Task<IReadOnlyList<MetarObservation>> FetchNearbyAsync(double lat, double lon, CancellationToken ct = default)
    {
        try
        {
            double latSpan = SearchRadiusKm / 111.0;
            double lonSpan = SearchRadiusKm / (111.0 * Math.Max(Math.Cos(lat * Math.PI / 180.0), 0.05));

            var url = "https://aviationweather.gov/api/data/metar?format=json&bbox=" + string.Join(",", new[]
            {
                lat - latSpan, lon - lonSpan, lat + latSpan, lon + lonSpan,
            }.Select(v => v.ToString("0.####", CultureInfo.InvariantCulture)));

            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<MetarObservation>();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<MetarObservation>();

            var cutoff = DateTime.UtcNow - MaxObservationAge;
            var list = new List<MetarObservation>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!TryGetDouble(item, "temp", out double temp)) continue;
                if (!TryGetDouble(item, "lat", out double stationLat)) continue;
                if (!TryGetDouble(item, "lon", out double stationLon)) continue;
                if (!TryGetDouble(item, "elev", out double elevation)) continue;
                if (!item.TryGetProperty("obsTime", out var obsEl) || obsEl.ValueKind != JsonValueKind.Number) continue;

                var observedAt = DateTimeOffset.FromUnixTimeSeconds(obsEl.GetInt64()).UtcDateTime;
                if (observedAt < cutoff) continue;

                double distance = DistanceKm(lat, lon, stationLat, stationLon);
                if (distance > SearchRadiusKm) continue;

                list.Add(new MetarObservation(
                    item.TryGetProperty("icaoId", out var idEl) ? idEl.GetString() ?? "" : "",
                    item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
                    temp,
                    elevation,
                    distance,
                    observedAt));
            }

            list.Sort((a, b) => a.DistanceKm.CompareTo(b.DistanceKm));
            return list;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (FetchErrors.IsRecoverable(ex))
        {
            return Array.Empty<MetarObservation>();
        }
    }

    /// <summary>
    /// Closest observation at a comparable altitude, or null when none fits — in which case the
    /// caller keeps the forecast model's value rather than trusting a station in another valley.
    /// </summary>
    public static MetarObservation? SelectBest(IReadOnlyList<MetarObservation> observations, double siteElevationMeters)
    {
        if (double.IsNaN(siteElevationMeters)) return null;

        foreach (var observation in observations)
        {
            if (Math.Abs(observation.ElevationMeters - siteElevationMeters) <= MaxElevationDeltaMeters)
                return observation;
        }
        return null;
    }

    private static bool TryGetDouble(JsonElement item, string property, out double value)
    {
        if (item.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.Number)
        {
            value = el.GetDouble();
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>Haversine great-circle distance in kilometres.</summary>
    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
