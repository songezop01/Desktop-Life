using System.Collections.ObjectModel;

namespace DesktopLife.Core;

/// <summary>Fixed house templates expressed in the active monitor's logical work-area coordinates.</summary>
public sealed class HouseLayout
{
    public const int MaximumFloorCount = 3;
    public const double GirlDesignHeight = 256;
    public const double CatDesignHeight = 80;
    public const double DogDesignHeight = 104;
    public const double FloorSpacing = 352;
    public const double FurnitureSupportClearance = 80;
    public const double StairRun = 240;
    public const double MinimumDesignWidth = 560;
    private const double FloorInset = 20;
    private const double BottomInset = 12;
    private const double TopClearance = 24;
    private const double StairLandingInset = 128;

    private HouseLayout(BodyBounds workArea, double sceneScale, HouseFloor[] floors, HouseStair[] stairs)
    {
        WorkArea = workArea;
        SceneScale = sceneScale;
        Floors = Array.AsReadOnly(floors);
        Stairs = Array.AsReadOnly(stairs);
        // Stair contact is selected explicitly by the grounded traversal controller. Exposing slopes to ordinary
        // gravity would let wandering pets land halfway up a connector that they never reserved.
        Platforms = Array.AsReadOnly(floors.Select(f => f.Platform).ToArray());
    }

    public BodyBounds WorkArea { get; }
    public double SceneScale { get; }
    /// <summary>Shared floor-contact precision for planning and furniture departure at every scene scale.</summary>
    public double FloorContactTolerance => Math.Max(.01, .5 * SceneScale);
    public int FloorCount => Floors.Count;
    /// <summary>Index zero is the ground floor. Indices increase upward.</summary>
    public ReadOnlyCollection<HouseFloor> Floors { get; }
    public ReadOnlyCollection<HouseStair> Stairs { get; }
    /// <summary>Base floor support used by ordinary gravity; stairs expose their own explicit Support.</summary>
    public ReadOnlyCollection<RoomPlatform> Platforms { get; }

    public static HouseLayout Create(int floorCount, BodyBounds workArea)
    {
        if (floorCount < 1 || floorCount > MaximumFloorCount)
            throw new ArgumentOutOfRangeException(nameof(floorCount), "目前可使用一層、兩層或三層小屋。");
        if (new[] { workArea.Left, workArea.Top, workArea.Width, workArea.Height }.Any(v => !double.IsFinite(v) || Math.Abs(v) > 100000) ||
            workArea.Width < 1 || workArea.Height < 1 ||
            !double.IsFinite(workArea.Left + workArea.Width) || !double.IsFinite(workArea.Top + workArea.Height))
            throw new ArgumentOutOfRangeException(nameof(workArea));

        // One common scale preserves character/furniture proportions on narrow portrait displays as well as short work areas.
        var designHeight = BottomInset + GirlDesignHeight + FurnitureSupportClearance + TopClearance + FloorSpacing * (floorCount - 1);
        var scale = Math.Min(1, Math.Min(workArea.Width / MinimumDesignWidth, workArea.Height / designHeight));
        var left = workArea.Left + FloorInset * scale;
        var right = workArea.Left + workArea.Width - FloorInset * scale;
        var groundY = workArea.Top + workArea.Height - BottomInset * scale;
        var floors = Enumerable.Range(0, floorCount)
            .Select(i => new HouseFloor(i, left, right, groundY - i * FloorSpacing * scale)).ToArray();
        var stairs = new HouseStair[floorCount - 1];
        for (var i = 0; i < stairs.Length; i++)
        {
            // Alternating entrances provide a continuous zigzag route through the house.
            var onRight = i % 2 == 0;
            var upperX = onRight ? workArea.Left + workArea.Width - StairLandingInset * scale : workArea.Left + StairLandingInset * scale;
            var lowerX = upperX + (onRight ? -StairRun : StairRun) * scale;
            var lower = new RoomPoint(lowerX, floors[i].Y);
            var upper = new RoomPoint(upperX, floors[i + 1].Y);
            const int stepCount = 16;
            var steps = Enumerable.Range(1, stepCount).Select(step =>
            {
                var t = (double)step / stepCount;
                var previousX = lower.X + (upper.X - lower.X) * (step - 1) / stepCount;
                var nextX = lower.X + (upper.X - lower.X) * t;
                return new HouseStairStep(Math.Min(previousX, nextX), Math.Abs(nextX - previousX),
                    lower.Y + (upper.Y - lower.Y) * t, (lower.Y - upper.Y) / stepCount);
            }).ToArray();
            stairs[i] = new HouseStair($"house-stair-{i}-{i + 1}", i, i + 1, lower, upper, Array.AsReadOnly(steps));
        }
        return new HouseLayout(workArea, scale, floors, stairs);
    }

    public int? FindFloor(double footX, double footY, double tolerance = 2)
    {
        if (!double.IsFinite(footX) || !double.IsFinite(footY) || !double.IsFinite(tolerance) || tolerance < 0)
            return null;
        foreach (var floor in Floors)
            if (footX >= floor.Left - tolerance && footX <= floor.Right + tolerance && Math.Abs(footY - floor.Y) <= tolerance)
                return floor.Index;
        return null;
    }

    public double SafeFootX(int floorIndex, double requestedX, double halfWidth = 0)
    {
        if (floorIndex < 0 || floorIndex >= FloorCount) throw new ArgumentOutOfRangeException(nameof(floorIndex));
        if (!double.IsFinite(requestedX) || !double.IsFinite(halfWidth) || halfWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(requestedX));
        var floor = Floors[floorIndex];
        var clearance = Math.Min(halfWidth, floor.Width / 2);
        return Math.Clamp(requestedX, floor.Left + clearance, floor.Right - clearance);
    }
}

public sealed record HouseFloor(int Index, double Left, double Right, double Y)
{
    public double Width => Right - Left;
    public RoomPlatform Platform => new(Left, Width, Y, Y);
}

public sealed record HouseStair(string Id, int LowerFloor, int UpperFloor, RoomPoint LowerLanding,
    RoomPoint UpperLanding, IReadOnlyList<HouseStairStep> Steps)
{
    /// <summary>The visual treads share this continuous contact surface during stair traversal.</summary>
    public RoomPlatform Support => LowerLanding.X < UpperLanding.X
        ? new(LowerLanding.X, UpperLanding.X - LowerLanding.X, LowerLanding.Y, UpperLanding.Y)
        : new(UpperLanding.X, LowerLanding.X - UpperLanding.X, UpperLanding.Y, LowerLanding.Y);

    /// <summary>A shared key for both directions. Only one character may occupy this connector at a time.</summary>
    public string ReservationKey => Id;
}

public readonly record struct HouseStairStep(double X, double Width, double Y, double Rise);
