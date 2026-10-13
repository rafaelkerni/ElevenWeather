using System.IO;
using ElevenWeather.Models;
using ElevenWeather.Services;

namespace ElevenWeather.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ElevenWeather.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_ReturnsDefaultsWhenFileIsMissing()
    {
        var settings = new SettingsService(_dir).Load();

        Assert.False(settings.HasLocation);
        Assert.Equal(TemperatureUnit.Celsius, settings.TemperatureUnit);
        Assert.Equal(IconSet.Meteocons, settings.IconSet);
        Assert.Equal(TimeFormat.TwentyFourHour, settings.TimeFormat);
        Assert.Null(settings.Language);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        var service = new SettingsService(_dir);
        var original = new AppSettings
        {
            Latitude = -22.9,
            Longitude = -47.06,
            CityName = "Campinas",
            AutoStart = true,
            AutoStartPromptShown = true,
            Language = AppLanguage.Spanish,
            TemperatureUnit = TemperatureUnit.Fahrenheit,
            IconSet = IconSet.FluentEmoji,
            TimeFormat = TimeFormat.TwelveHour,
        };

        service.Save(original);
        var loaded = new SettingsService(_dir).Load();

        Assert.Equal(original.Latitude, loaded.Latitude);
        Assert.Equal(original.Longitude, loaded.Longitude);
        Assert.Equal(original.CityName, loaded.CityName);
        Assert.True(loaded.AutoStart);
        Assert.True(loaded.AutoStartPromptShown);
        Assert.Equal(AppLanguage.Spanish, loaded.Language);
        Assert.Equal(TemperatureUnit.Fahrenheit, loaded.TemperatureUnit);
        Assert.Equal(IconSet.FluentEmoji, loaded.IconSet);
        Assert.Equal(TimeFormat.TwelveHour, loaded.TimeFormat);
    }

    [Fact]
    public void Save_CreatesDirectoryAndOverwrites()
    {
        var service = new SettingsService(_dir);

        service.Save(new AppSettings { CityName = "A" });
        service.Save(new AppSettings { CityName = "B" });

        Assert.True(File.Exists(SettingsPath));
        Assert.Equal("B", service.Load().CityName);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("""{"TemperatureUnit":"Kelvin"}""")]
    public void Load_FallsBackToDefaultsOnCorruptedFile(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, content);

        var settings = new SettingsService(_dir).Load();

        Assert.Equal(TemperatureUnit.Celsius, settings.TemperatureUnit);
        Assert.False(settings.HasLocation);
    }

    [Fact]
    public void Load_KeepsDefaultsForFieldsMissingFromOlderFiles()
    {
        // Arquivo salvo por uma versão antiga, antes de existirem ícones e formato de hora
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{"Latitude":1,"Longitude":2,"CityName":"X"}""");

        var settings = new SettingsService(_dir).Load();

        Assert.True(settings.HasLocation);
        Assert.Equal(IconSet.Meteocons, settings.IconSet);
        Assert.Equal(TimeFormat.TwentyFourHour, settings.TimeFormat);
    }

    [Fact]
    public void Load_FallsBackToDefaultsWhenFileIsLocked()
    {
        var service = new SettingsService(_dir);
        service.Save(new AppSettings { CityName = "Locked" });

        using (new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Null(service.Load().CityName);
            // Salvar com o arquivo travado também não pode lançar
            service.Save(new AppSettings { CityName = "Other" });
        }

        Assert.Equal("Locked", service.Load().CityName);
    }

    [Fact]
    public void DefaultDirectory_IsUnderAppData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.Equal(Path.Combine(appData, "ElevenWeather"), SettingsService.DefaultSettingsDir);
    }
}
