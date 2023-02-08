using Serilog.Sinks.SystemConsole.Themes;

namespace Surreal.Cli;

sealed class HslConsoleTheme : AnsiConsoleTheme
{
    public HslConsoleTheme(IReadOnlyDictionary<ConsoleThemeStyle, Hsl> styles) : base(styles.ToDictionary(s => s.Key, s =>
    {
        var (r, g, b) = s.Value.ToRGB();
        return $"\u001b[38;2;{r};{g};{b}m";
    })) { }
}

