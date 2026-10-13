namespace ElevenWeather.Models;

/// <summary>Temperatura observada por uma estação de aeroporto (METAR), já com a distância até o local configurado.</summary>
public sealed record MetarObservation(
    string StationId,
    string StationName,
    double TemperatureC,
    double ElevationMeters,
    double DistanceKm,
    DateTime ObservedAtUtc);
