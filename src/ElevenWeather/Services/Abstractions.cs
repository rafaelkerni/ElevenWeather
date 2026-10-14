using ElevenWeather.Models;

namespace ElevenWeather.Services;

// Contratos dos serviços com efeitos externos (rede, disco, registro, timer),
// para que o WeatherStore e os ViewModels possam ser testados com implementações falsas.

public interface IWeatherService
{
    WeatherSnapshot? LastKnownGood { get; }

    Task<WeatherSnapshot?> FetchAsync(double lat, double lon, string cityName, CancellationToken ct = default);
}

public interface ILocationService
{
    Task<(double Lat, double Lon, string City)?> DetectByIpAsync(CancellationToken ct = default);

    Task<IReadOnlyList<GeocodingResult>> SearchCityAsync(string query, CancellationToken ct = default);
}

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}

public interface IAutoStartService
{
    bool IsEnabled();

    void SetEnabled(bool enabled);
}

/// <summary>Timer que dispara na thread de UI.</summary>
public interface IUiTimer
{
    TimeSpan Interval { get; set; }

    event Action? Tick;

    void Start();

    void Stop();
}

/// <summary>Ícones do MSN/Bing Weather, copiados sob demanda do app instalado no Windows.</summary>
public interface IMsnIconInstaller
{
    /// <summary>Se todos os ícones já estão disponíveis localmente.</summary>
    bool IsInstalled { get; }

    /// <summary>Copia os ícones do Bing Weather instalado e informa o motivo quando não consegue.</summary>
    Task<MsnIconInstallResult> InstallAsync();
}

public enum MsnIconInstallStatus
{
    Success,
    NotInstalled,
    UnexpectedLayout,
    CopyFailed,
}

/// <param name="PackageVersion">Versão do Bing Weather encontrada, quando conhecida.</param>
public sealed record MsnIconInstallResult(MsnIconInstallStatus Status, string? PackageVersion = null)
{
    public bool Succeeded => Status == MsnIconInstallStatus.Success;
}

/// <summary>Atualização do próprio app via GitHub Releases.</summary>
public interface IUpdateService
{
    /// <summary>Baixa e instala a versão mais nova, se houver; ela passa a valer na próxima inicialização.</summary>
    Task<UpdateResult> TryUpdateAsync(CancellationToken ct = default);
}
