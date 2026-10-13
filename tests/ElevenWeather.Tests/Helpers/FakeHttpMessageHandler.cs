using System.Net;
using System.Net.Http;
using System.Text;

namespace ElevenWeather.Tests.Helpers;

/// <summary>Handler HTTP falso: responde de acordo com o host da requisição, sem tocar na rede.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new(StringComparer.OrdinalIgnoreCase);

    public List<HttpRequestMessage> Requests { get; } = new();

    public FakeHttpMessageHandler OnHost(string host, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _routes[host] = responder;
        return this;
    }

    public FakeHttpMessageHandler OnHost(string host, string json, HttpStatusCode status = HttpStatusCode.OK) =>
        OnHost(host, _ => Json(json, status));

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public HttpClient CreateClient() => new(this);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);

        // Host sem rota simula uma API fora do ar
        return _routes.TryGetValue(request.RequestUri!.Host, out var responder)
            ? Task.FromResult(responder(request))
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
