using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace ElevenWeather.Services;

public enum UpdateResult
{
    UpToDate,
    Installed,
    Failed,
}

/// <summary>
/// Atualização silenciosa via GitHub Releases: baixa o exe da versão mais nova, confere o SHA-256
/// publicado junto e troca o exe em disco. O processo atual segue rodando; a nova versão vale na próxima inicialização.
/// </summary>
public sealed class UpdateService
{
    public const string DefaultLatestReleaseUrl = "https://api.github.com/repos/rafaelkerni/ElevenWeather/releases/latest";

    private const string ExeAssetName = "ElevenWeather.exe";
    private const string HashAssetName = ExeAssetName + ".sha256";

    private readonly HttpClient _http;
    private readonly Version _currentVersion;
    private readonly string _exePath;
    private readonly string _latestReleaseUrl;

    /// <param name="exePath">Exe a ser substituído; configurável para os testes usarem uma pasta temporária.</param>
    public UpdateService(HttpClient http, Version currentVersion, string exePath, string latestReleaseUrl = DefaultLatestReleaseUrl)
    {
        _http = http;
        _currentVersion = Normalize(currentVersion);
        _exePath = exePath;
        _latestReleaseUrl = latestReleaseUrl;
    }

    public string OldExePath => Path.Combine(Path.GetDirectoryName(_exePath)!, "ElevenWeather.old.exe");

    private string NewExePath => Path.Combine(Path.GetDirectoryName(_exePath)!, "ElevenWeather.new.exe");

    /// <summary>Remove as sobras de uma atualização anterior (o exe antigo só pode ser apagado depois que ele sai de execução).</summary>
    public void CleanupPreviousUpdate()
    {
        TryDelete(OldExePath);
        TryDelete(NewExePath);
    }

    public async Task<UpdateResult> TryUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var release = await FetchLatestReleaseAsync(ct);
            if (release is null) return UpdateResult.Failed;

            var (version, assets) = release.Value;
            if (version <= _currentVersion) return UpdateResult.UpToDate;

            // Sem o hash publicado não há como validar o download, então não atualiza
            if (!assets.TryGetValue(ExeAssetName, out var exeUrl) || !assets.TryGetValue(HashAssetName, out var hashUrl))
                return UpdateResult.Failed;

            var expectedHash = ParseHash(await _http.GetStringAsync(hashUrl, ct));
            if (expectedHash is null) return UpdateResult.Failed;

            await DownloadAsync(exeUrl, NewExePath, ct);
            if (!string.Equals(await ComputeHashAsync(NewExePath, ct), expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(NewExePath);
                return UpdateResult.Failed;
            }

            SwapExecutables();
            return UpdateResult.Installed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException
                                       or UnauthorizedAccessException or JsonException)
        {
            TryDelete(NewExePath);
            return UpdateResult.Failed;
        }
    }

    private async Task<(Version Version, Dictionary<string, string> Assets)?> FetchLatestReleaseAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseUrl);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
        if (tag is null || !Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;

        var assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsProp.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) && asset.TryGetProperty("browser_download_url", out var url)
                    && name.GetString() is { } n && url.GetString() is { } u)
                    assets[n] = u;
            }
        }

        return (Normalize(version), assets);
    }

    private async Task DownloadAsync(string url, string destination, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, ct);
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
    }

    /// <summary>Aceita só o hash ou o formato do sha256sum ("hash  arquivo").</summary>
    internal static string? ParseHash(string content)
    {
        var token = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return token is { Length: 64 } && token.All(Uri.IsHexDigit) ? token : null;
    }

    /// <summary>O Windows permite renomear um exe em execução, então o atual vira .old e o novo assume o nome original.</summary>
    private void SwapExecutables()
    {
        if (File.Exists(OldExePath))
            File.Delete(OldExePath);

        File.Move(_exePath, OldExePath);
        try
        {
            File.Move(NewExePath, _exePath);
        }
        catch
        {
            File.Move(OldExePath, _exePath);
            throw;
        }
    }

    // Compara só X.Y.Z: a versão do assembly vem com um quarto componente que as tags não têm
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Arquivo ainda em uso: a limpeza tenta de novo na próxima inicialização.
        }
    }
}
