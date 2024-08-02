using FFXIVClientStructs.FFXIV.Client.UI;

namespace Automaton.Utilities;

public static class Colors
{
    public static HaselColor Transparent { get; } = new(Vector4.Zero);
    public static HaselColor White { get; } = new(Vector4.One);
    public static HaselColor Black { get; } = new(0, 0, 0);
    public static HaselColor Orange { get; } = new(1, 0.6f, 0);
    public static HaselColor Gold { get; } = new(0.847f, 0.733f, 0.49f);
    public static HaselColor Green { get; } = new(0, 1, 0);
    public static HaselColor Yellow { get; } = new(1, 1, 0);
    public static HaselColor Red { get; } = new(1, 0, 0);
    public static HaselColor Grey { get; } = new(0.73f, 0.73f, 0.73f);
    public static HaselColor Grey2 { get; } = new(0.87f, 0.87f, 0.87f);
    public static HaselColor Grey3 { get; } = new(0.6f, 0.6f, 0.6f);
    public static HaselColor Grey4 { get; } = new(0.3f, 0.3f, 0.3f);

    public static unsafe bool IsLightTheme
        => RaptureAtkModule.Instance()->AtkUIColorHolder.ActiveColorThemeType == 1;
}
