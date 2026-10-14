using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using ElevenWeather.Services;
using ElevenWeather.Tests.Helpers;

namespace ElevenWeather.Tests.Services;

/// <summary>Usa uma pasta temporária como "pasta do app" e um handler HTTP falso; nunca toca no exe real nem no GitHub.</summary>
public sealed class UpdateServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ElevenWeather.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeHttpMessageHandler _http = new();

    private string ExePath => Path.Combine(_root, "ElevenWeather.exe");

    public UpdateServiceTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(ExePath, "old");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private UpdateService CreateService(string currentVersion = "0.1.0") =>
        new(_http.CreateClient(), Version.Parse(currentVersion), ExePath);

    private static string ReleaseJson(string tag, bool includeHash)
    {
        var hashAsset = includeHash
            ? """, { "name": "ElevenWeather.exe.sha256", "browser_download_url": "https://github.com/dl/ElevenWeather.exe.sha256" }"""
            : "";
        return $$"""
            {
              "tag_name": "{{tag}}",
              "assets": [
                { "name": "ElevenWeather.exe", "browser_download_url": "https://github.com/dl/ElevenWeather.exe" }{{hashAsset}}
              ]
            }
            """;
    }

    private void SetupRelease(string exeContent = "new", string? publishedHash = null, string tag = "v0.2.0", bool includeHash = true)
    {
        var exe = Encoding.UTF8.GetBytes(exeContent);
        var hash = publishedHash ?? Convert.ToHexString(SHA256.HashData(exe)).ToLowerInvariant();
        _http.OnHost("api.github.com", ReleaseJson(tag, includeHash));
        _http.OnHost("github.com", request => request.RequestUri!.AbsolutePath.EndsWith(".sha256")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{hash}  ElevenWeather.exe") }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(exe) });
    }

    private string[] FilesInAppDir() => Directory.GetFiles(_root).Select(f => Path.GetFileName(f)!).Order().ToArray();

    [Fact]
    public async Task Update_ReplacesExeAndKeepsPreviousAsOld()
    {
        SetupRelease();
        var service = CreateService();

        Assert.Equal(UpdateResult.Installed, await service.TryUpdateAsync());

        Assert.Equal("new", File.ReadAllText(ExePath));
        Assert.Equal("old", File.ReadAllText(service.OldExePath));
        Assert.Equal(new[] { "ElevenWeather.exe", "ElevenWeather.old.exe" }, FilesInAppDir());
    }

    [Theory]
    [InlineData("0.2.0")]
    [InlineData("0.3.0")]
    [InlineData("0.2.0.0")]
    public async Task Update_DoesNothingWhenAlreadyOnSameOrNewerVersion(string currentVersion)
    {
        SetupRelease();

        Assert.Equal(UpdateResult.UpToDate, await CreateService(currentVersion).TryUpdateAsync());

        Assert.Equal("old", File.ReadAllText(ExePath));
        Assert.Single(_http.Requests);
    }

    [Fact]
    public async Task Update_RejectsDownloadWithWrongHash()
    {
        SetupRelease(publishedHash: new string('a', 64));

        Assert.Equal(UpdateResult.Failed, await CreateService().TryUpdateAsync());

        Assert.Equal("old", File.ReadAllText(ExePath));
        Assert.Equal(new[] { "ElevenWeather.exe" }, FilesInAppDir());
    }

    [Fact]
    public async Task Update_RequiresPublishedHash()
    {
        SetupRelease(includeHash: false);

        Assert.Equal(UpdateResult.Failed, await CreateService().TryUpdateAsync());

        Assert.Equal("old", File.ReadAllText(ExePath));
    }

    [Theory]
    [InlineData("""{ "tag_name": "nightly", "assets": [] }""")]
    [InlineData("""{ "assets": [] }""")]
    [InlineData("not json")]
    public async Task Update_FailsOnUnexpectedReleaseInfo(string json)
    {
        _http.OnHost("api.github.com", json);

        Assert.Equal(UpdateResult.Failed, await CreateService().TryUpdateAsync());
    }

    [Fact]
    public async Task Update_FailsWhenGitHubIsUnavailable()
    {
        Assert.Equal(UpdateResult.Failed, await CreateService().TryUpdateAsync());

        Assert.Equal("old", File.ReadAllText(ExePath));
    }

    [Fact]
    public async Task Update_FailsWhenDownloadBreaksAndLeavesNoPartialFile()
    {
        SetupRelease();
        _http.OnHost("github.com", request => request.RequestUri!.AbsolutePath.EndsWith(".sha256")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('a', 64)) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Equal(UpdateResult.Failed, await CreateService().TryUpdateAsync());

        Assert.Equal(new[] { "ElevenWeather.exe" }, FilesInAppDir());
    }

    [Fact]
    public void Cleanup_RemovesLeftoversFromPreviousUpdate()
    {
        var service = CreateService();
        File.WriteAllText(service.OldExePath, "old");
        File.WriteAllText(Path.Combine(_root, "ElevenWeather.new.exe"), "partial");

        service.CleanupPreviousUpdate();

        Assert.Equal(new[] { "ElevenWeather.exe" }, FilesInAppDir());
    }

    [Theory]
    [InlineData("ABCDEF0123456789abcdef0123456789ABCDEF0123456789abcdef0123456789  ElevenWeather.exe", true)]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789\r\n", true)]
    [InlineData("abc  ElevenWeather.exe", false)]
    [InlineData("zzzzzz0123456789abcdef0123456789abcdef0123456789abcdef0123456789", false)]
    [InlineData("", false)]
    public void ParseHash_AcceptsBareHashOrSha256sumFormat(string content, bool valid)
    {
        Assert.Equal(valid, UpdateService.ParseHash(content) is not null);
    }
}
