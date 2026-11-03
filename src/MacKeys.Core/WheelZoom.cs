namespace MacKeys;

public readonly record struct WheelModifiers(bool LeftAlt, bool RightAlt, bool Ctrl, bool Shift, bool Win, bool Injected);

public static class WheelZoom
{
    public static bool ShouldConvert(WheelModifiers modifiers) =>
        modifiers.LeftAlt
        && !modifiers.RightAlt
        && !modifiers.Ctrl
        && !modifiers.Shift
        && !modifiers.Win
        && !modifiers.Injected;
}
