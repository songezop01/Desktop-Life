using System.Windows;
using System.Windows.Media;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    private readonly RenderPositionSampler presentationPosition = new();
    private readonly record struct PresentationGeometry(HouseLayout? House, double Width, double Height, string Display, double Dpi);
    private PresentationGeometry? presentationGeometry;
    private long presentationGeometryToken;
    private bool presentationTick, presentationHooked, presentationInitialized, houseAdvancePresented, stairPresentationReached;
    private double presentationTickTime;
    private HouseTravelWaypoint? presentationTravel;
    private StairStepMotion? presentationStair;
    private HouseLayout? presentationStairHouse;
    private bool presentationDescending;
    private Point? presentedOrigin;
    private Point? presentedBaseOrigin;
    private Point? floorTransferSample;
    private Point floorTransferOffset;
    private double floorTransferDistance, floorTransferSpan;
    private bool floorTransferActive;
    private (int X, int Y, string Display, double Dpi)? presentedPixels;
    // Compressed raster fixtures must not flood WPF with thousands of native
    // moves in one dispatcher turn. Real-time HWND probes leave this false.
    private bool diagnosticRasterPresentation;

    // The host supplies a real, transformed alpha-foot point in the 116 x 144
    // canonical sprite. Absence of calibration uses root motion and is reported.
    internal Func<double, StairFootSide, Point?>? StairRasterFootSampler { get; set; }
    internal Action<double?>? PresentedWalkCycleRequested { get; set; }
    internal Point PresentedOrigin => presentedOrigin ?? new(body.X, body.Y);
    internal StairStepPose? PresentedStairPose { get; private set; }
    internal bool StairRasterAlignmentAvailable { get; private set; }
    internal long PresentationRenderFrames { get; private set; }
    internal long PresentationWindowWrites { get; private set; }

    private void InitializeHouseMotion012()
    {
        if (presentationInitialized) return; presentationInitialized = true;
        Loaded += (_, _) => { SnapPresentation(); UpdatePresentationRendering(); };
        IsVisibleChanged += (_, _) => { SnapPresentation(); UpdatePresentationRendering(); };
        Closed += (_, _) => StopPresentationRendering();
    }
    private void BeginPresentationTick(double timeSeconds)
    {
        presentationTick = true; presentationTickTime = timeSeconds;
        houseAdvancePresented = stairPresentationReached = false; presentationTravel = null;
    }
    private void EndPresentationTick() => presentationTick = false;

    // Call once for an actual HouseTraversal advance, before its waypoint is dequeued.
    private void RememberHousePresentation(HouseTravelWaypoint travel, bool reached)
    { houseAdvancePresented = true; presentationTravel = travel; stairPresentationReached = reached; }

    private bool CanPresentContinuousWalk()
    {
        var walking = (poseAction ?? body.Action) is BodyAction.Walk or BodyAction.Wander or BodyAction.Explore or BodyAction.ChaseCursor or BodyAction.AvoidCursor;
        return walking && IsLoaded && IsVisible && simulationEnabled && !paused && !EditingRoom && !Interacting && petGravity.Grounded
            && MovementPhase is not (NavigationPhase.Orienting or NavigationPhase.Crouching or NavigationPhase.Airborne or NavigationPhase.Landing or NavigationPhase.Recovering);
    }
    private void SelectPresentationStair(HouseTravelWaypoint? travel)
    {
        var house = House;
        var stair = travel?.Kind == HouseTravelKind.Stair && house is not null
            ? house.Stairs.FirstOrDefault(value => value.ReservationKey == travel.ConnectorId) : null;
        var descending = travel is not null && travel.TargetFloor < travel.SourceFloor;
        if (stair is null)
        {
            if (presentationStair is not null)
            {
                presentationGeometryToken++;
                if (presentationTick && CanPresentContinuousWalk() && presentedOrigin is {} painted && presentedBaseOrigin is {} root)
                {
                    floorTransferOffset = new(painted.X - root.X, painted.Y - root.Y);
                    floorTransferSpan = Math.Max(64 * (House?.SceneScale ?? 1), Math.Sqrt(Math.Pow(floorTransferOffset.X, 2) + Math.Pow(floorTransferOffset.Y, 2)) * 4);
                    floorTransferDistance = 0; floorTransferSample = null; floorTransferActive = true;
                }
                else ClearFloorPresentationTransfer();
                sprite.SetPresentedFacing(null);
            }
            presentationStair = null; presentationStairHouse = null; return;
        }
        if (presentationStair is not null && ReferenceEquals(presentationStairHouse, house)
            && presentationStair.ConnectorId == stair.ReservationKey && presentationDescending == descending)
        {
            // A geometry/reset snap may have retired the transient turn override
            // while preserving this cached connector. Restore the real stance
            // direction before sampling its next actual painted foot.
            sprite.SetPresentedFacing(Math.Sign(travel!.To.X-travel.From.X),hold:true);
            return;
        }
        presentationStair = new(stair, descending); presentationStairHouse = house; presentationDescending = descending;
        ClearFloorPresentationTransfer();
        // The approach prepares this turn before the first planted stance.
        // Once on stairs, a turn must not drag that stance foot across its tread.
        sprite.SetPresentedFacing(Math.Sign(travel!.To.X-travel.From.X),hold:true);
        presentationGeometryToken++;
    }
    private void ApplyPresentationPosition()
    {
        sprite.ConfigureRasterFootGeometry(BodyWidth,BodyHeight,DisplayWorkspace.Active.Scale);
        var geometry = new PresentationGeometry(House, BodyWidth, BodyHeight, DisplayWorkspace.Active.Id, DisplayWorkspace.Active.Scale);
        if (presentationGeometry != geometry)
        { presentationGeometry = geometry; presentationGeometryToken++; presentedPixels = null; ClearFloorPresentationTransfer(); }
        if (!presentationTick) { SnapPresentation(); return; }
        SelectPresentationStair(houseAdvancePresented ? presentationTravel : null);
        var continuous = CanPresentContinuousWalk() && !stairPresentationReached;
        presentationPosition.Record(new(body.X, body.Y), presentationTickTime, continuous,
            BodyHeight / DesktopBody.Height, presentationGeometryToken, allowVerticalMotion: presentationStair is not null);
        if (!continuous) { SnapPresentation(); return; }
        ApplyPresentedSample(presentationTickTime, staticStair: false);
        UpdatePresentationRendering();
    }
    private void SnapPresentation()
    {
        // An ordinary landing is a continuous hand-off, not a drag/reset. All
        // intentional interruptions retire the transfer and snap immediately.
        if (!presentationTick || !CanPresentContinuousWalk()) ClearFloorPresentationTransfer();
        // A frozen grounded stair gets an actual tread, never a stale interpolated
        // midpoint. Drags, jumps, relocation and hidden scenes keep logical position.
        houseRoute.TryPeek(out var travel);
        var staticStair = IsVisible && petGravity.Grounded && !Interacting && !EditingRoom
            && MovementPhase != NavigationPhase.Recovering && travel?.Kind == HouseTravelKind.Stair;
        SelectPresentationStair(staticStair ? travel : null);
        presentationPosition.Snap(new(body.X, body.Y), clock.Elapsed.TotalSeconds, presentationGeometryToken);
        PresentedWalkCycleRequested?.Invoke(null);
        ApplyPresentedSample(clock.Elapsed.TotalSeconds, staticStair);
        StopPresentationRendering();
    }
    private void ApplyPresentedSample(double timeSeconds, bool staticStair)
    {
        var origin = presentationPosition.Sample(timeSeconds); PresentedStairPose = null; StairRasterAlignmentAvailable = false;
        presentedBaseOrigin = new(origin.X, origin.Y);
        if (presentationStair is {} stair && stair.TrySample(new(origin.X + HalfWidth, origin.Y + BodyHeight), out var pose))
        {
            if (staticStair)
            {
                var progress = Math.Round(pose!.StepIndex + pose.StepProgress, MidpointRounding.AwayFromZero) / stair.StepCount;
                pose = stair.SampleProgress(Math.Clamp(progress, 0, 1));
            }
            PresentedStairPose = pose;
            if (!staticStair) PresentedWalkCycleRequested?.Invoke(pose!.WalkCycle);
            origin = new(pose!.RootFeet.X - HalfWidth, pose.RootFeet.Y - BodyHeight);
            presentedBaseOrigin = new(origin.X, origin.Y);
            var actual = StairRasterFootSampler?.Invoke(pose.WalkCycle, pose.PlantSide);
            if (!staticStair && sprite.PreviewRasterWalkFoot((pose.StepIndex * 4 - 1) / 8d,
                    pose.StepIndex % 2 == 0 ? StairFootSide.Right : StairFootSide.Left) is {} oldFoot
                && sprite.PreviewRasterWalkFoot((pose.StepIndex * 4 + 3) / 8d,
                    pose.StepIndex % 2 == 0 ? StairFootSide.Left : StairFootSide.Right) is {} nextFoot)
            {
                // The facing interpolation may briefly be edge-on. Keep the
                // projected raster-origin path continuous through that turn,
                // but only report a stance contact when pixels are visible.
                var oldPlant = stair.Anchors[pose.StepIndex]; var nextPlant = stair.Anchors[pose.StepIndex + 1];
                var from = new RoomPoint(oldPlant.X - oldFoot.X * BodyWidth / 116, oldPlant.Y - oldFoot.Y * BodyHeight / 144);
                var to = new RoomPoint(nextPlant.X - nextFoot.X * BodyWidth / 116, nextPlant.Y - nextFoot.Y * BodyHeight / 144);
                var transfer = StairStepMotion.TransferFraction(pose.StepProgress);
                origin = new(from.X + (to.X - from.X) * transfer, from.Y + (to.Y - from.Y) * transfer);
            }
            if (actual is {} foot && double.IsFinite(foot.X) && double.IsFinite(foot.Y) && foot.X >= 0 && foot.X <= 116 && foot.Y >= 0 && foot.Y <= 144)
            {
                if(staticStair)origin = new(pose.PlantPoint.X - foot.X * BodyWidth / 116, pose.PlantPoint.Y - foot.Y * BodyHeight / 144);
                StairRasterAlignmentAvailable = true;
            }
        }
        else
        {
            PresentedWalkCycleRequested?.Invoke(null);
            if (CanPresentContinuousWalk()) origin = ApplyFloorPresentationTransfer(origin);
        }
        var pixel = DisplayWorkspace.ToPixels(new(origin.X, origin.Y));
        var key = ((int)Math.Round(pixel.X), (int)Math.Round(pixel.Y), DisplayWorkspace.Active.Id, DisplayWorkspace.Active.Scale);
        presentedOrigin = DisplayWorkspace.FromPixels(new(key.Item1, key.Item2));
        if (presentedPixels == key) return;
        presentedPixels = key;
        if (diagnosticRasterPresentation) return;
        DisplayWorkspace.Position(this, presentedOrigin.Value.X, presentedOrigin.Value.Y); PresentationWindowWrites++;
    }
    private void ClearFloorPresentationTransfer()
    { floorTransferActive = false; floorTransferSample = null; floorTransferDistance = 0; floorTransferOffset = default; sprite.SetPresentedFacing(null); }
    private RoomPoint ApplyFloorPresentationTransfer(RoomPoint logical)
    {
        var offset = new Point();
        if (floorTransferActive)
        {
            if (floorTransferSample is {} old)
                floorTransferDistance += Math.Sqrt(Math.Pow(logical.X - old.X, 2) + Math.Pow(logical.Y - old.Y, 2));
            floorTransferSample = new(logical.X, logical.Y);
            var remaining = 1 - PresentationEase(Math.Clamp(floorTransferDistance / floorTransferSpan, 0, 1));
            offset = new(floorTransferOffset.X * remaining, floorTransferOffset.Y * remaining);
            if (remaining == 0) ClearFloorPresentationTransfer();
        }
        // Anticipate only the next reserved route's entrance on the current
        // real floor. No route, physics, contacts or needs advance here.
        var entrance = houseRoute.FirstOrDefault(value => value.Kind == HouseTravelKind.Stair);
        if (entrance is not null && House is {} house && house.FindFloor(logical.X + HalfWidth, logical.Y + BodyHeight, house.FloorContactTolerance) == entrance.SourceFloor
            && sprite.PreviewRasterWalkFoot(-.125, StairFootSide.Right,Math.Sign(entrance.To.X-entrance.From.X)) is {} futureFoot)
        {
            var desiredFuture = new Point(HalfWidth - futureFoot.X * BodyWidth / 116, BodyHeight - futureFoot.Y * BodyHeight / 144);
            var span = Math.Max(64 * house.SceneScale, Math.Sqrt(desiredFuture.X * desiredFuture.X + desiredFuture.Y * desiredFuture.Y) * 4);
            var distance = Math.Abs(logical.X + HalfWidth - entrance.From.X);
            var preparation = PresentationEase(Math.Clamp(1 - distance / span, 0, 1));
            sprite.SetPresentedFacing(distance<=span?Math.Sign(entrance.To.X-entrance.From.X):null);
            if(sprite.PreviewRasterWalkFoot(-.125,StairFootSide.Right) is {} foot)
            {
                var desired = new Point(HalfWidth - foot.X * BodyWidth / 116, BodyHeight - foot.Y * BodyHeight / 144);
                offset = new(offset.X + (desired.X - offset.X) * preparation, offset.Y + (desired.Y - offset.Y) * preparation);
            }
        }
        else sprite.SetPresentedFacing(null);
        return new(logical.X + offset.X, logical.Y + offset.Y);
    }
    private static double PresentationEase(double value) => value * value * (3 - 2 * value);
    private void UpdatePresentationRendering()
    {
        var needed = IsLoaded && IsVisible && simulationEnabled && !paused && !EditingRoom && !Interacting && presentationPosition.Pending(clock.Elapsed.TotalSeconds);
        if (needed && !presentationHooked) { CompositionTarget.Rendering += RenderPresentation; presentationHooked = true; }
        else if (!needed) StopPresentationRendering();
    }
    private void StopPresentationRendering()
    { if (presentationHooked) CompositionTarget.Rendering -= RenderPresentation; presentationHooked = false; }
    private void RenderPresentation(object? sender, EventArgs e)
    {
        if (!CanPresentContinuousWalk()) { SnapPresentation(); return; }
        var now = clock.Elapsed.TotalSeconds; PresentationRenderFrames++; ApplyPresentedSample(now, staticStair: false);
        if (!presentationPosition.Pending(now)) StopPresentationRendering();
    }
}
