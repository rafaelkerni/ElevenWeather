using ElevenWeather.Services;
using Microsoft.Win32;

namespace ElevenWeather.Tests.Services;

/// <summary>Usa uma subchave descartável em HKCU, nunca a chave Run nem o tema reais.</summary>
public sealed class RegistryServicesTests : IDisposable
{
    private const string TestRoot = @"Software\ElevenWeather.Tests";

    private readonly string _keyPath = $@"{TestRoot}\{Guid.NewGuid():N}";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        using var root = Registry.CurrentUser.OpenSubKey(TestRoot, writable: true);
        if (root is { SubKeyCount: 0, ValueCount: 0 })
            Registry.CurrentUser.DeleteSubKey(TestRoot, throwOnMissingSubKey: false);
    }

    [Fact]
    public void AutoStart_DisabledWhenKeyDoesNotExist()
    {
        Assert.False(new AutoStartService(_keyPath).IsEnabled());
    }

    [Fact]
    public void AutoStart_EnableRegistersQuotedExecutablePath()
    {
        var service = new AutoStartService(_keyPath);

        service.SetEnabled(true);

        Assert.True(service.IsEnabled());
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        var value = Assert.IsType<string>(key!.GetValue("ElevenWeather"));
        Assert.Equal($"\"{Environment.ProcessPath}\"", value);
    }

    [Fact]
    public void AutoStart_DisableRemovesValueAndIsIdempotent()
    {
        var service = new AutoStartService(_keyPath);
        service.SetEnabled(true);

        service.SetEnabled(false);
        service.SetEnabled(false);

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void AutoStart_DefaultUsesWindowsRunKey()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", AutoStartService.DefaultRunKeyPath);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Theme_ReadsSystemUsesLightTheme(int value, bool expectedLight)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_keyPath))
            key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);

        Assert.Equal(expectedLight, SystemThemeWatcher.IsLightTheme(_keyPath));
    }

    [Fact]
    public void Theme_DefaultsToLightWhenValueIsMissingOrInvalid()
    {
        Assert.True(SystemThemeWatcher.IsLightTheme(_keyPath));

        using (var key = Registry.CurrentUser.CreateSubKey(_keyPath))
            key.SetValue("SystemUsesLightTheme", "dark", RegistryValueKind.String);

        Assert.True(SystemThemeWatcher.IsLightTheme(_keyPath));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Widgets_ReadsTaskbarDa(int value, bool expectedEnabled)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_keyPath))
            key.SetValue("TaskbarDa", value, RegistryValueKind.DWord);

        Assert.Equal(expectedEnabled, WindowsWidgetsDetector.IsWidgetsButtonEnabled(_keyPath));
    }

    [Fact]
    public void Widgets_DefaultsToEnabledWhenValueIsMissing()
    {
        Assert.True(WindowsWidgetsDetector.IsWidgetsButtonEnabled(_keyPath));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Widgets_PolicyDisablesWhenAllowNewsAndInterestsIsZero(int value, bool expectedDisabled)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_keyPath))
            key.SetValue("AllowNewsAndInterests", value, RegistryValueKind.DWord);

        Assert.Equal(expectedDisabled, WindowsWidgetsDetector.IsDisabledByPolicy(Registry.CurrentUser, _keyPath));
    }

    [Fact]
    public void Widgets_NoPolicyMeansNotDisabled()
    {
        Assert.False(WindowsWidgetsDetector.IsDisabledByPolicy(Registry.CurrentUser, _keyPath));
    }

    [Fact]
    public void Widgets_RealKeyReadDoesNotThrow()
    {
        Assert.Null(Record.Exception(() => WindowsWidgetsDetector.IsWidgetsButtonEnabled()));
    }

    [Fact]
    public void Theme_RealKeyReadDoesNotThrow()
    {
        Assert.Null(Record.Exception(() => SystemThemeWatcher.IsLightTheme()));
    }
}
