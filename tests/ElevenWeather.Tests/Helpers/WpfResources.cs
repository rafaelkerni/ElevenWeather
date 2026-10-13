using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ElevenWeather.Tests.Helpers;

internal static class WpfResources
{
    /// <summary>
    /// Sem um Application rodando, o WPF lê recursos embutidos (ícones) do testhost; aponta para o
    /// assembly do app. A propriedade pública não aceita troca depois de lida, daí a reflexão.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        var field = typeof(Application).GetField("_resourceAssembly", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Campo interno do WPF não encontrado; revisar WpfResources.");
        field.SetValue(null, typeof(ElevenWeather.App).Assembly);
    }
}
