using System.Diagnostics;
using System.IO;

namespace ElevenWeather.Services;

/// <summary>
/// Copia os ícones do app Bing Weather já instalado para a pasta de dados do usuário.
/// Nada é baixado nem redistribuído: os arquivos pertencem à Microsoft e só são lidos da máquina local.
/// </summary>
public sealed class MsnIconInstaller : IMsnIconInstaller
{
    public static readonly string DefaultDirectory = Path.Combine(SettingsService.DefaultSettingsDir, "Icons", "Msn");

    // Nome do ícone no ElevenWeather -> arquivo original dentro do pacote do Bing Weather.
    private static readonly Dictionary<string, string> SourceFiles = new()
    {
        ["clear_day"] = "AAehR3S.png",
        ["clear_night"] = "AAehwh2.png",
        ["cloudy"] = "AAei3XW.png",
        ["partly_cloudy_day"] = "AAehLNN.png",
        ["fog"] = "AAehOqA.png",
        ["rain"] = "LightRainV3.png",
        ["snow"] = "AAei6xJ.png",
        ["thunderstorm"] = "AAehOqE.png",
    };

    private const string PackageIconsSubPath = @"Assets\notification\normal_icons";

    private readonly string _directory;
    private readonly Func<Task<string?>> _locatePackage;

    /// <param name="directory">Pasta de destino; configurável para os testes.</param>
    /// <param name="locatePackage">Devolve a pasta de instalação do Bing Weather; configurável para os testes.</param>
    public MsnIconInstaller(string? directory = null, Func<Task<string?>>? locatePackage = null)
    {
        _directory = directory ?? DefaultDirectory;
        _locatePackage = locatePackage ?? LocateBingWeatherAsync;
    }

    public bool IsInstalled => SourceFiles.Keys.All(key => File.Exists(GetPath(_directory, key)));

    public static string GetPath(string directory, string snakeCaseKey) => Path.Combine(directory, snakeCaseKey + ".png");

    public async Task<MsnIconInstallResult> InstallAsync()
    {
        var packageRoot = await _locatePackage();
        if (string.IsNullOrWhiteSpace(packageRoot))
            return new MsnIconInstallResult(MsnIconInstallStatus.NotInstalled);

        var version = ParseVersion(packageRoot);
        var sourceDir = Path.Combine(packageRoot, PackageIconsSubPath);
        if (SourceFiles.Values.Any(file => !File.Exists(Path.Combine(sourceDir, file))))
            return new MsnIconInstallResult(MsnIconInstallStatus.UnexpectedLayout, version);

        try
        {
            Directory.CreateDirectory(_directory);
            foreach (var (key, file) in SourceFiles)
                File.Copy(Path.Combine(sourceDir, file), GetPath(_directory, key), overwrite: true);

            return new MsnIconInstallResult(MsnIconInstallStatus.Success, version);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MsnIconInstallResult(MsnIconInstallStatus.CopyFailed, version);
        }
    }

    /// <summary>"...\Microsoft.BingWeather_4.54.63045.0_x64__8wekyb3d8bbwe" -> "4.54.63045.0".</summary>
    internal static string? ParseVersion(string packageRoot)
    {
        var parts = Path.GetFileName(packageRoot.TrimEnd('\\', '/')).Split('_');
        return parts.Length >= 2 && parts[1].Length > 0 ? parts[1] : null;
    }

    private static async Task<string?> LocateBingWeatherAsync()
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            ArgumentList =
            {
                "-NoProfile", "-NonInteractive", "-Command",
                "(Get-AppxPackage -Name Microsoft.BingWeather | Select-Object -First 1).InstallLocation",
            },
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null) return null;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return output.Trim();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or OperationCanceledException or InvalidOperationException)
        {
            return null;
        }
    }
}
