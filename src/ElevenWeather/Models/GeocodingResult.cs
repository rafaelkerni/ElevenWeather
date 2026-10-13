namespace ElevenWeather.Models;

public sealed record GeocodingResult(string Name, string? Admin1, string Country, double Latitude, double Longitude)
{
    public string DisplayName => Admin1 is null ? $"{Name}, {Country}" : $"{Name}, {Admin1}, {Country}";
}
