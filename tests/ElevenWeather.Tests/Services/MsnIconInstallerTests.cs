using System.IO;
using ElevenWeather.Services;

namespace ElevenWeather.Tests.Services;

/// <summary>Usa pastas temporárias; nunca toca na pasta real de ícones nem no app Bing Weather.</summary>
public sealed class MsnIconInstallerTests : IDisposable
{
    private static readonly string[] SourceFiles =
    {
        "AAehR3S.png", "AAehwh2.png", "AAei3XW.png", "AAehLNN.png",
        "AAehOqA.png", "LightRainV3.png", "AAei6xJ.png", "AAehOqE.png",
    };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ElevenWeather.Tests", Guid.NewGuid().ToString("N"));

    private string PackageDir => Path.Combine(_root, "package");
    private string TargetDir => Path.Combine(_root, "target");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void CreatePackage(params string[] files)
    {
        var iconsDir = Path.Combine(PackageDir, "Assets", "notification", "normal_icons");
        Directory.CreateDirectory(iconsDir);
        foreach (var file in files)
            File.WriteAllText(Path.Combine(iconsDir, file), file);
    }

    private MsnIconInstaller CreateInstaller(string? packageRoot) =>
        new(TargetDir, () => Task.FromResult(packageRoot));

    [Fact]
    public async Task Install_CopiesEveryIconUnderTheElevenWeatherNames()
    {
        CreatePackage(SourceFiles);
        var installer = CreateInstaller(PackageDir);
        Assert.False(installer.IsInstalled);

        Assert.True((await installer.InstallAsync()).Succeeded);

        Assert.True(installer.IsInstalled);
        Assert.Equal("LightRainV3.png", File.ReadAllText(Path.Combine(TargetDir, "rain.png")));
        Assert.Equal("AAehR3S.png", File.ReadAllText(Path.Combine(TargetDir, "clear_day.png")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Install_ReportsNotInstalledWhenBingWeatherIsMissing(string? packageRoot)
    {
        var installer = CreateInstaller(packageRoot);

        var result = await installer.InstallAsync();

        Assert.Equal(MsnIconInstallStatus.NotInstalled, result.Status);
        Assert.Null(result.PackageVersion);
        Assert.False(installer.IsInstalled);
    }

    [Fact]
    public async Task Install_ReportsUnexpectedLayoutWithoutCopyingWhenPackageIsMissingIcons()
    {
        CreatePackage(SourceFiles.Skip(1).ToArray());
        var installer = CreateInstaller(PackageDir);

        var result = await installer.InstallAsync();

        Assert.Equal(MsnIconInstallStatus.UnexpectedLayout, result.Status);

        Assert.False(installer.IsInstalled);
        Assert.False(Directory.Exists(TargetDir));
    }

    [Fact]
    public async Task Install_OverwritesPreviouslyCopiedIcons()
    {
        CreatePackage(SourceFiles);
        var installer = CreateInstaller(PackageDir);
        await installer.InstallAsync();

        File.WriteAllText(Path.Combine(TargetDir, "snow.png"), "stale");
        Assert.True((await installer.InstallAsync()).Succeeded);

        Assert.Equal("AAei6xJ.png", File.ReadAllText(Path.Combine(TargetDir, "snow.png")));
    }

    [Fact]
    public async Task Install_ReportsCopyFailedWhenTargetIsNotWritable()
    {
        CreatePackage(SourceFiles);
        // Um arquivo no lugar da pasta de destino impede a criação do diretório
        Directory.CreateDirectory(_root);
        File.WriteAllText(TargetDir, "not a directory");

        var result = await CreateInstaller(PackageDir).InstallAsync();

        Assert.Equal(MsnIconInstallStatus.CopyFailed, result.Status);
    }

    [Theory]
    [InlineData(@"C:\Program Files\WindowsApps\Microsoft.BingWeather_4.54.63045.0_x64__8wekyb3d8bbwe", "4.54.63045.0")]
    [InlineData(@"C:\Program Files\WindowsApps\Microsoft.BingWeather_4.54.63045.0_x64__8wekyb3d8bbwe\", "4.54.63045.0")]
    [InlineData(@"C:\temp\package", null)]
    public void ParseVersion_ReadsTheVersionFromThePackageFolderName(string packageRoot, string? expected)
    {
        Assert.Equal(expected, MsnIconInstaller.ParseVersion(packageRoot));
    }
}
