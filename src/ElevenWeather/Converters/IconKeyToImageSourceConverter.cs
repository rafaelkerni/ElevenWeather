using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ElevenWeather.Models;
using ElevenWeather.Services;

namespace ElevenWeather.Converters;

/// <summary>
/// Resolves the logical icon key (e.g. "ClearDay") to the PNG for the currently selected
/// <see cref="IconSetPreference"/> under Resources/Icons/{set}/{icon_key}.png. Falls back to the
/// bundled Meteocons set if the chosen set's file isn't available (e.g. the MSN set, which is copied
/// from the installed Bing Weather app on first use) — the app never crashes for a missing icon.
/// </summary>
public sealed class IconKeyToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key) return null;

        return LoadImage(IconSetPreference.Instance.Current, key)
            ?? LoadImage(IconSet.Meteocons, key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>Whether the icons for the given set can be loaded right now.</summary>
    public static bool IsIconSetAvailable(IconSet set)
    {
        using var stream = GetImageStream(set, "ClearDay");
        return stream is not null;
    }

    private static ImageSource? LoadImage(IconSet set, string key)
    {
        using var stream = GetImageStream(set, key);
        if (stream is null) return null;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static System.IO.Stream? GetImageStream(IconSet set, string key)
    {
        // O conjunto MSN não é embutido: vem da pasta de dados do usuário, copiado sob demanda.
        if (set == IconSet.Msn)
            return OpenMsnFile(key);

        string folder = set == IconSet.FluentEmoji ? "FluentEmoji" : "Meteocons";
        var uri = new Uri($"/Resources/Icons/{folder}/{ToSnakeCase(key)}.png", UriKind.Relative);
        try
        {
            return System.Windows.Application.GetResourceStream(uri)?.Stream;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
    }

    private static System.IO.Stream? OpenMsnFile(string key)
    {
        try
        {
            var path = MsnIconInstaller.GetPath(MsnIconInstaller.DefaultDirectory, ToSnakeCase(key));
            return System.IO.File.Exists(path) ? System.IO.File.OpenRead(path) : null;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>"ClearDay" -> "clear_day" — matches the on-disk naming shared by all three icon sets.</summary>
    internal static string ToSnakeCase(string pascalCase)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < pascalCase.Length; i++)
        {
            char c = pascalCase[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
