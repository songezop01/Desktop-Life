namespace DesktopLife.Core;

public enum DisplayPriority { Highest, High, Medium, Desktop }
// Persisted numeric values must stay stable when new companions are added.
public enum PetAppearance { Girl = 0, Cat = 1, BorderCollie = 2 }
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
