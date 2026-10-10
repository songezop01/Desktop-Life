namespace DesktopLife.Core;

public enum HouseTravelKind { FloorWalk, Stair }

public sealed record HouseTravelWaypoint(HouseTravelKind Kind, RoomPoint From, RoomPoint To,
    int SourceFloor, int TargetFloor, string? ConnectorId = null)
{
    public double Length => Math.Sqrt(Math.Pow(To.X - From.X, 2) + Math.Pow(To.Y - From.Y, 2));
}

public readonly record struct HouseTravelAdvance(RoomPoint Feet, bool Reached, bool Supported);

/// <summary>Grounded travel along fixed floor and stair surfaces. Furniture approaches remain RoomNavigation's responsibility.</summary>
public static class HouseTraversal
{
    public const double MaximumElapsedSeconds = .1;

    /// <summary>Preserves an active route's real contact during a non-moving tick; never creates a landing.</summary>
    public static RoomPlatform? ContactSupport(HouseLayout layout, HouseTravelWaypoint? step, RoomPoint currentFeet)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(currentFeet);
        if (step is null || !Finite(currentFeet) || !Finite(step.From) || !Finite(step.To)) return null;
        RoomPlatform support;
        double tolerance;
        if (step.Kind == HouseTravelKind.FloorWalk)
        {
            if (step.SourceFloor < 0 || step.SourceFloor >= layout.FloorCount || step.TargetFloor != step.SourceFloor)
                return null;
            var floor = layout.Floors[step.SourceFloor];
            tolerance = layout.FloorContactTolerance;
            // A stale route must not attach to the replacement house merely because
            // its source index still exists; both planned endpoints belong to this floor.
            if (Math.Abs(step.From.Y - floor.Y) > tolerance || Math.Abs(step.To.Y - floor.Y) > tolerance ||
                step.From.X < floor.Left || step.From.X > floor.Right || step.To.X < floor.Left || step.To.X > floor.Right)
                return null;
            support = floor.Platform;
        }
        else if (step.Kind == HouseTravelKind.Stair)
        {
            var stair = layout.Stairs.SingleOrDefault(s => s.ReservationKey == step.ConnectorId);
            if (stair is null) return null;
            var upward = step.SourceFloor == stair.LowerFloor && step.TargetFloor == stair.UpperFloor;
            var downward = step.SourceFloor == stair.UpperFloor && step.TargetFloor == stair.LowerFloor;
            if (!upward && !downward || Distance(step.From, upward ? stair.LowerLanding : stair.UpperLanding) > .75 ||
                Distance(step.To, upward ? stair.UpperLanding : stair.LowerLanding) > .75) return null;
            support = stair.Support;
            tolerance = .75;
        }
        else return null;
        if (currentFeet.X < support.X || currentFeet.X > support.X + support.Width ||
            Math.Abs(support.HeightAt(currentFeet.X) - currentFeet.Y) > tolerance ||
            !Advance(step, currentFeet, 0, 1).Supported) return null;
        return support;
    }

    public static IReadOnlyList<HouseTravelWaypoint>? Plan(HouseLayout layout, double footX, double footY,
        int goalFloor, double goalX, double halfWidth = 0)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (goalFloor < 0 || goalFloor >= layout.FloorCount) throw new ArgumentOutOfRangeException(nameof(goalFloor));
        if (!double.IsFinite(goalX) || !double.IsFinite(halfWidth) || halfWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(goalX));
        // A character in midair or on furniture must first reach a floor; planning never manufactures a landing.
        var source = layout.FindFloor(footX, footY, layout.FloorContactTolerance);
        if (source is null || halfWidth * 2 > layout.Floors[source.Value].Width) return null;
        var route = new List<HouseTravelWaypoint>();
        var point = new RoomPoint(footX, footY);
        var floor = source.Value;
        while (floor != goalFloor)
        {
            var upward = goalFloor > floor;
            var nextFloor = floor + (upward ? 1 : -1);
            var stair = layout.Stairs.Single(s => s.LowerFloor == Math.Min(floor, nextFloor) && s.UpperFloor == Math.Max(floor, nextFloor));
            var entrance = upward ? stair.LowerLanding : stair.UpperLanding;
            var exit = upward ? stair.UpperLanding : stair.LowerLanding;
            // A wide body must fit at the stair landing as well as on the floor.
            var sourceFloor = layout.Floors[floor];
            var destinationFloor = layout.Floors[nextFloor];
            if (entrance.X < sourceFloor.Left + halfWidth || entrance.X > sourceFloor.Right - halfWidth ||
                exit.X < destinationFloor.Left + halfWidth || exit.X > destinationFloor.Right - halfWidth)
                return null;
            AddWalk(entrance, floor);
            route.Add(new(HouseTravelKind.Stair, entrance, exit, floor, nextFloor, stair.ReservationKey));
            point = exit;
            floor = nextFloor;
        }
        AddWalk(new(layout.SafeFootX(goalFloor, goalX, halfWidth), layout.Floors[goalFloor].Y), goalFloor);
        return route.AsReadOnly();

        void AddWalk(RoomPoint target, int level)
        {
            if (Math.Abs(point.X - target.X) > .000001 || Math.Abs(point.Y - target.Y) > .000001)
                route.Add(new(HouseTravelKind.FloorWalk, point, target, level, level));
            point = target;
        }
    }

    public static HouseTravelAdvance Advance(HouseTravelWaypoint step, RoomPoint currentFeet, double elapsed,
        double pixelsPerSecond, double supportTolerance = .75)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(currentFeet);
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!double.IsFinite(pixelsPerSecond) || pixelsPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(pixelsPerSecond));
        if (!double.IsFinite(supportTolerance) || supportTolerance < 0) throw new ArgumentOutOfRangeException(nameof(supportTolerance));
        if (!Finite(step.From) || !Finite(step.To) || !Finite(currentFeet))
            return new(currentFeet, false, false);

        var dx = step.To.X - step.From.X;
        var dy = step.To.Y - step.From.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= .000000000001)
        {
            var atTarget = Distance(currentFeet, step.To) <= supportTolerance;
            // A zero-duration tick never changes a character's position, including at a route endpoint.
            return new(currentFeet, atTarget, atTarget);
        }
        var fraction = ((currentFeet.X - step.From.X) * dx + (currentFeet.Y - step.From.Y) * dy) / lengthSquared;
        var projected = new RoomPoint(step.From.X + fraction * dx, step.From.Y + fraction * dy);
        var length = Math.Sqrt(lengthSquared);
        if (fraction < -supportTolerance / length || fraction > 1 + supportTolerance / length ||
            Distance(projected, currentFeet) > supportTolerance)
            return new(currentFeet, false, false);

        var remaining = Distance(currentFeet, step.To);
        if (remaining == 0) return new(currentFeet, true, true);
        var distance = pixelsPerSecond * Math.Min(elapsed, MaximumElapsedSeconds);
        if (distance == 0) return new(currentFeet, false, true);
        var movementFraction = Math.Min(1, distance / remaining);
        // Interpolate from the actual feet. A tiny accumulated error is corrected over the remaining segment,
        // never by snapping to the entrance or by raising the existing jump limit.
        var next = movementFraction >= 1 ? step.To : new RoomPoint(currentFeet.X + (step.To.X - currentFeet.X) * movementFraction,
            currentFeet.Y + (step.To.Y - currentFeet.Y) * movementFraction);
        return new(next, movementFraction >= 1, true);
    }

    private static bool Finite(RoomPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static double Distance(RoomPoint a, RoomPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
