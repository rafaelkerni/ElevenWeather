using System.ComponentModel;
using System.Globalization;

namespace ElevenWeather.Models;

/// <summary>
/// pt/en/es string table + current language. Implements INotifyPropertyChanged with the
/// special "Item[]" indexer so XAML bindings like {Binding Path=[key]} update themselves
/// on a language switch, without needing to recreate the window.
/// </summary>
public sealed class AppLocalization : INotifyPropertyChanged
{
    public static AppLocalization Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Fired after any language switch — for C# (non-XAML) code to recalculate text.</summary>
    public event Action? LanguageChanged;

    private AppLanguage _current = DetectSystemLanguage();

    private AppLocalization() { }

    public AppLanguage Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            LanguageChanged?.Invoke();
        }
    }

    public string this[string key] => Get(key, _current);

    public static string Get(string key) => Get(key, Instance._current);

    private static string Get(string key, AppLanguage language) =>
        Strings.TryGetValue(key, out var values) ? values[(int)language] : key;

    /// <summary>Matches Windows' language when it's one of the 3 supported; English as the factory default otherwise.</summary>
    private static AppLanguage DetectSystemLanguage() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "pt" => AppLanguage.Portuguese,
        "es" => AppLanguage.Spanish,
        "en" => AppLanguage.English,
        _ => AppLanguage.English,
    };

    // Array indices: [0]=Portuguese, [1]=English, [2]=Spanish — same order as the AppLanguage enum.
    private static readonly Dictionary<string, string[]> Strings = new()
    {
        ["ui.currentLocation"] = new[] { "Local atual", "Current location", "Ubicación actual" },
        ["ui.loading"] = new[] { "Carregando...", "Loading...", "Cargando..." },
        ["ui.airQuality"] = new[] { "Qualidade do ar", "Air quality", "Calidad del aire" },
        ["ui.viewFullForecast"] = new[] { "Ver a previsão completa", "See full forecast", "Ver el pronóstico completo" },
        ["ui.hourly"] = new[] { "De hora em hora", "Hourly", "Por hora" },
        ["ui.daily"] = new[] { "Diariamente", "Daily", "Diariamente" },
        ["ui.searchCity"] = new[] { "Buscar cidade...", "Search city...", "Buscar ciudad..." },
        ["ui.searching"] = new[] { "Buscando...", "Searching...", "Buscando..." },
        ["ui.noCityFound"] = new[] { "Nenhuma cidade encontrada", "No city found", "No se encontró ninguna ciudad" },
        ["ui.today"] = new[] { "Hoje", "Today", "Hoy" },
        ["ui.moreOptions"] = new[] { "Mais opções", "More options", "Más opciones" },
        ["ui.previousPage"] = new[] { "Página anterior", "Previous page", "Página anterior" },
        ["ui.nextPage"] = new[] { "Próxima página", "Next page", "Página siguiente" },

        ["prompt.autoStartTitle"] = new[] { "Iniciar com o Windows", "Start with Windows", "Iniciar con Windows" },
        ["prompt.autoStartMessage"] = new[]
        {
            "Deseja que o ElevenWeather inicie automaticamente com o Windows? Você pode mudar isso a qualquer momento no menu do widget.",
            "Would you like ElevenWeather to start automatically with Windows? You can change this anytime from the widget's menu.",
            "¿Quieres que ElevenWeather se inicie automáticamente con Windows? Puedes cambiar esto en cualquier momento desde el menú del widget.",
        },

        ["menu.refresh"] = new[] { "Atualizar agora", "Refresh now", "Actualizar ahora" },
        ["menu.startWithWindows"] = new[] { "Iniciar com o Windows", "Start with Windows", "Iniciar con Windows" },
        ["menu.language"] = new[] { "Idioma", "Language", "Idioma" },
        ["menu.temperatureUnit"] = new[] { "Unidade", "Unit", "Unidad" },
        ["menu.timeFormat"] = new[] { "Formato de hora", "Time format", "Formato de hora" },
        ["menu.iconSet"] = new[] { "Ícones", "Icons", "Iconos" },
        ["menu.exit"] = new[] { "Sair", "Exit", "Salir" },

        ["iconSet.msnUnavailableHint"] = new[]
        {
            "Copiados do app Clima (Bing Weather) instalado no Windows ao selecionar — são da Microsoft e não vêm inclusos.",
            "Copied from the Weather (Bing Weather) app installed on Windows when selected — they belong to Microsoft and aren't bundled.",
            "Se copian de la app El Tiempo (Bing Weather) instalada en Windows al seleccionar — son de Microsoft y no vienen incluidos.",
        },
        ["iconSet.msnNotInstalled"] = new[]
        {
            "Não foi possível copiar os ícones: o app Clima (Microsoft.BingWeather) não está instalado no Windows. Instale-o pela Microsoft Store e tente novamente.",
            "Couldn't copy the icons: the Weather app (Microsoft.BingWeather) isn't installed on Windows. Install it from the Microsoft Store and try again.",
            "No se pudieron copiar los íconos: la app El Tiempo (Microsoft.BingWeather) no está instalada en Windows. Instálala desde Microsoft Store e inténtalo de nuevo.",
        },
        ["iconSet.msnUnexpectedLayout"] = new[]
        {
            "O app Clima (Bing Weather, versão {0}) está instalado, mas os ícones não estão no formato esperado — provavelmente uma atualização da Microsoft. Abra uma issue em https://github.com/rafaelkerni/ElevenWeather/issues informando essa versão.",
            "The Weather app (Bing Weather, version {0}) is installed, but its icons aren't in the expected layout — probably a Microsoft update. Please open an issue at https://github.com/rafaelkerni/ElevenWeather/issues mentioning this version.",
            "La app El Tiempo (Bing Weather, versión {0}) está instalada, pero sus íconos no tienen el formato esperado — probablemente una actualización de Microsoft. Abre un issue en https://github.com/rafaelkerni/ElevenWeather/issues indicando esta versión.",
        },
        ["iconSet.msnCopyFailed"] = new[]
        {
            "Não foi possível salvar os ícones no disco (Bing Weather, versão {0}). Verifique as permissões da pasta de dados do aplicativo e tente novamente.",
            "Couldn't save the icons to disk (Bing Weather, version {0}). Check the permissions of the app data folder and try again.",
            "No se pudieron guardar los íconos en el disco (Bing Weather, versión {0}). Verifica los permisos de la carpeta de datos de la aplicación e inténtalo de nuevo.",
        },

        ["weekday.sun"] = new[] { "Dom", "Sun", "Dom" },
        ["weekday.mon"] = new[] { "Seg", "Mon", "Lun" },
        ["weekday.tue"] = new[] { "Ter", "Tue", "Mar" },
        ["weekday.wed"] = new[] { "Qua", "Wed", "Mié" },
        ["weekday.thu"] = new[] { "Qui", "Thu", "Jue" },
        ["weekday.fri"] = new[] { "Sex", "Fri", "Vie" },
        ["weekday.sat"] = new[] { "Sáb", "Sat", "Sáb" },

        ["weather.clear"] = new[] { "Limpo", "Clear", "Despejado" },
        ["weather.mostlyClear"] = new[] { "Predominantemente limpo", "Mostly clear", "Mayormente despejado" },
        ["weather.partlyCloudy"] = new[] { "Parcialmente nublado", "Partly cloudy", "Parcialmente nublado" },
        ["weather.cloudy"] = new[] { "Nublado", "Cloudy", "Nublado" },
        ["weather.fog"] = new[] { "Nevoeiro", "Fog", "Niebla" },
        ["weather.drizzle"] = new[] { "Garoa", "Drizzle", "Llovizna" },
        ["weather.freezingDrizzle"] = new[] { "Garoa congelante", "Freezing drizzle", "Llovizna helada" },
        ["weather.rain"] = new[] { "Chuva", "Rain", "Lluvia" },
        ["weather.heavyRain"] = new[] { "Chuva forte", "Heavy rain", "Lluvia fuerte" },
        ["weather.freezingRain"] = new[] { "Chuva congelante", "Freezing rain", "Lluvia helada" },
        ["weather.snow"] = new[] { "Neve", "Snow", "Nieve" },
        ["weather.heavySnow"] = new[] { "Neve forte", "Heavy snow", "Nieve fuerte" },
        ["weather.snowGrains"] = new[] { "Grãos de neve", "Snow grains", "Granos de nieve" },
        ["weather.rainShowers"] = new[] { "Pancadas de chuva", "Rain showers", "Chubascos" },
        ["weather.heavyRainShowers"] = new[] { "Pancadas fortes de chuva", "Heavy rain showers", "Chubascos fuertes" },
        ["weather.snowShowers"] = new[] { "Pancadas de neve", "Snow showers", "Chubascos de nieve" },
        ["weather.thunderstorm"] = new[] { "Tempestade", "Thunderstorm", "Tormenta" },
        ["weather.thunderstormHail"] = new[] { "Tempestade com granizo", "Thunderstorm with hail", "Tormenta con granizo" },
        ["weather.unavailable"] = new[] { "Indisponível", "Unavailable", "No disponible" },

        ["aqi.good"] = new[] { "Boa", "Good", "Buena" },
        ["aqi.moderate"] = new[] { "Moderada", "Moderate", "Moderada" },
        ["aqi.unhealthySensitive"] = new[] { "Insalubre para grupos sensíveis", "Unhealthy for sensitive groups", "No saludable para grupos sensibles" },
        ["aqi.unhealthy"] = new[] { "Insalubre", "Unhealthy", "No saludable" },
        ["aqi.veryUnhealthy"] = new[] { "Muito insalubre", "Very unhealthy", "Muy insalubre" },
        ["aqi.hazardous"] = new[] { "Perigosa", "Hazardous", "Peligrosa" },
    };
}
