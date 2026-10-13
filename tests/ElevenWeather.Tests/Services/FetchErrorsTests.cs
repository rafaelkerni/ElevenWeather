using System.Net.Http;
using System.Text.Json;
using ElevenWeather.Services;

namespace ElevenWeather.Tests.Services;

public sealed class FetchErrorsTests
{
    public static TheoryData<Exception> Recoverable => new()
    {
        new HttpRequestException(),
        new JsonException(),
        new TaskCanceledException(),
        new KeyNotFoundException(),
        new InvalidOperationException(),
        new IndexOutOfRangeException(),
        new FormatException(),
    };

    public static TheoryData<Exception> Fatal => new()
    {
        new OutOfMemoryException(),
        new NullReferenceException(),
        new ArgumentException(),
        // Cancelamento pedido pelo chamador é tratado à parte e precisa propagar
        new OperationCanceledException(),
    };

    [Theory]
    [MemberData(nameof(Recoverable))]
    public void ExpectedApiFailures_AreRecoverable(Exception ex) => Assert.True(FetchErrors.IsRecoverable(ex));

    [Theory]
    [MemberData(nameof(Fatal))]
    public void ProgrammingErrors_AreNotSwallowed(Exception ex) => Assert.False(FetchErrors.IsRecoverable(ex));
}
