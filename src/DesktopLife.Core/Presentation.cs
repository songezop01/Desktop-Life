namespace DesktopLife.Core;

public enum DisplayPriority { Highest, High, Medium, Desktop }
public enum PetAppearance { Girl, Cat }
public readonly record struct ForegroundState(bool Fullscreen, bool Maximized, bool Desktop);
public static class DisplayPolicy
{
    public static bool Visible(DisplayPriority priority, ForegroundState state, bool hidden = false) => !hidden && priority switch
    {
        DisplayPriority.Highest => true,
        DisplayPriority.High => !state.Fullscreen,
        DisplayPriority.Medium => !state.Fullscreen && !state.Maximized,
        DisplayPriority.Desktop => true, // Visibility comes from native desktop Z order, not foreground focus.
        _ => false
    };
}
