namespace ElevenWeather.Models;

/// <summary>A cell of the horizontal forecast list — same shape for both the hourly and daily tabs.</summary>
public sealed record ForecastRow(string TopLabel, string IconKey, string PrimaryText, string SecondaryText);
