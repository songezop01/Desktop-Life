namespace DesktopLife.Core;

public enum WalkDistanceKind { None, Floor, Stair }

public readonly record struct WalkDistanceSample(double X, double Y, bool Grounded);

/// <summary>Measures one continuous movement update, without retaining positions across relocations.</summary>
public static class WalkDistanceSampler
{
    // Match the sprite's existing per-update guard. A restored position must not skip a walk cycle.
    public const double MaximumCanonicalSampleDistance = 80;

    public static double Measure(WalkDistanceSample before, WalkDistanceSample after,
        double canonicalScale, WalkDistanceKind kind, bool continuousMotion = true)
    {
        if (!double.IsFinite(canonicalScale) || canonicalScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(canonicalScale));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!continuousMotion || kind == WalkDistanceKind.None || !before.Grounded || !after.Grounded ||
            !Finite(before) || !Finite(after)) return 0;

        var dx = (after.X - before.X) / canonicalScale;
        var dy = (after.Y - before.Y) / canonicalScale;
        var pathDistance = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(pathDistance) || pathDistance >= MaximumCanonicalSampleDistance) return 0;

        // Ordinary floors use horizontal stride; a reserved stair also travels vertically.
        // This measures the final advance even after its route waypoint has been removed.
        return kind == WalkDistanceKind.Stair ? pathDistance : Math.Abs(dx);
    }

    private static bool Finite(WalkDistanceSample sample) => double.IsFinite(sample.X) && double.IsFinite(sample.Y);
}
