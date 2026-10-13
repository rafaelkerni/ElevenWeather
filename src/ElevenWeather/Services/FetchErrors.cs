using System.Net.Http;
using System.Text.Json;

namespace ElevenWeather.Services;

internal static class FetchErrors
{
    /// <summary>
    /// Falhas esperadas de uma chamada às APIs externas: rede, JSON inválido ou JSON válido com
    /// formato inesperado (campo ausente, tipo errado, arrays de tamanhos diferentes).
    /// </summary>
    public static bool IsRecoverable(Exception ex) => ex is HttpRequestException
        or JsonException
        or TaskCanceledException
        or KeyNotFoundException
        or InvalidOperationException
        or IndexOutOfRangeException
        or FormatException;
}
