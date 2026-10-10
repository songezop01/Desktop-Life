using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    private void SmokeHouseFloorWaitSupport(PetWindow[] residents, List<object> evidence)
    {
        var bounds = Bounds(); RoomWindow? box = null;
        var attractions = residents.Select(actor => (Actor: actor, Enabled: actor.AllowCursorAttraction)).ToArray();
        try
        {
            box = new(new(Guid.NewGuid(), FurnitureKind.Box, bounds.Left, bounds.Top));
            Furniture.Add(box);
            foreach (var compact in new[] { false, true })
            {
                ConfigureHouse(3, false);
                if (compact)
                {
                    ownedHouse = HouseLayout.Create(3, bounds with { Width = Math.Min(bounds.Width, HouseLayout.MinimumDesignWidth * .4) });
                    houseSurface?.Configure(ownedHouse);
                    foreach (var resident in residents) resident.ApplyHouseGeometry();
                }
                var layout = House!; box.SetSceneScale(layout.SceneScale);
                foreach (var sourceIndex in new[] { 0, 2 })
                {
                    var source = layout.Floors[sourceIndex]; var destination = layout.Floors[2 - sourceIndex];
                    box.SetFloor(sourceIndex); box.Relocate(source.Left + (source.Width - box.Width) * .43, source.Y - box.Height);
                    platformCacheKey = int.MinValue;
                    var boxSupport = box.Platforms.Single(); var center = boxSupport.X + boxSupport.Width * .5;
                    var gap = source.Y - boxSupport.HeightAt(center);
                    if (gap <= layout.FloorContactTolerance || gap >= GravityBody.PlatformContactTolerance)
                        throw new Exception("The floor-wait fixture must exercise the real near-floor Box support.");
                    foreach (var actor in residents)
                    {
                        actor.CancelRoute(); actor.Occupancy.Release(actor.appearance); actor.sequence = null;
                        actor.homeTarget = null; actor.SetPaused(false); actor.AllowCursorAttraction = false;
                        actor.body.Place(center - actor.HalfWidth, source.Y - actor.BodyHeight, bounds);
                        actor.petGravity.PlaceSupported(actor.body.X, actor.body.Y, actor.BodyWidth, actor.BodyHeight, source.Platform);
                        actor.body.Action = BodyAction.Walk; actor.poseAction = null; actor.houseControlledThisFrame = false;
                        var failures = actor.NavigationFailures; var houseFailures = actor.HouseRouteFailures;
                        var trips = actor.StairTrips;
                        actor.Navigate(center - actor.HalfWidth, destination.Y - actor.BodyHeight, .016, bounds, 120);
                        if (!actor.houseRoute.TryPeek(out var step) || step.Kind != HouseTravelKind.FloorWalk)
                            throw new Exception("The floor-wait regression did not begin a real house floor approach.");
                        var before = actor.Position; var pending = actor.houseRoute.Count;
                        actor.SetPaused(true);
                        for (var tick = 0; tick < 12; tick++)
                        {
                            actor.previous = actor.clock.Elapsed.TotalSeconds - .016;
                            actor.Tick(null, EventArgs.Empty);
                            CheckWait("paused");
                        }
                        actor.SetPaused(false);
                        var poseTime = actor.clock.Elapsed.TotalSeconds;
                        actor.sprite.Pose(BodyAction.Sit, poseTime, actor.facing);
                        actor.sprite.Pose(BodyAction.Walk, poseTime, actor.facing);
                        if (!actor.sprite.IsStandingUp) throw new Exception("The floor-wait regression did not enter the actual stand-up animation.");
                        actor.previous = actor.clock.Elapsed.TotalSeconds - .016;
                        actor.Tick(null, EventArgs.Empty);
                        CheckWait("standing-up");
                        var reached = false; var frames = 0;
                        for (; frames < 12000; frames++)
                        {
                            actor.houseControlledThisFrame = false; actor.navigationStepped = false;
                            actor.Navigate(center - actor.HalfWidth, destination.Y - actor.BodyHeight, .016, bounds, 120);
                            actor.ObserveNavigationProgress(.016); actor.StepPetGravity(.016);
                            if (actor.NavigationFailures != failures || actor.HouseRouteFailures != houseFailures)
                                throw new Exception($"{actor.appearance} failed to resume its planted floor route beside the Box.");
                            if (actor.petGravity.Grounded && Math.Abs(actor.body.X + actor.HalfWidth - center) < .05 &&
                                Math.Abs(actor.body.Y + actor.BodyHeight - destination.Y) < .01 && !actor.FinishingMotion)
                            { reached = true; break; }
                        }
                        if (!reached || actor.StairTrips - trips != 2)
                            throw new Exception($"{actor.appearance} did not finish both actual stairs after floor waiting.");
                        evidence.Add(new { Lifecycle = "real-floor-wait-beside-low-box", Character = actor.appearance.ToString(), Compact = compact,
                            From = sourceIndex, To = destination.Index, Scale = layout.SceneScale, BoxFloorGap = gap,
                            FloorFeet = source.Y, BoxFeet = boxSupport.HeightAt(center), WaitingFeet = before.Y + actor.BodyHeight,
                            PausedTicks = 12, ActualStandUpTick = true, FloorPositionUnchanged = true, Ticks = frames + 1,
                            StairTrips = actor.StairTrips - trips, NavigationFailures = actor.NavigationFailures - failures,
                            HouseRouteFailures = actor.HouseRouteFailures - houseFailures });

                        void CheckWait(string branch)
                        {
                            if (Math.Abs(actor.body.X - before.X) > .000001 || Math.Abs(actor.body.Y - before.Y) > .000001 ||
                                Math.Abs(actor.body.Y + actor.BodyHeight - source.Y) > .000001 || !actor.petGravity.Grounded ||
                                actor.houseRoute.Count != pending || actor.NavigationFailures != failures || actor.HouseRouteFailures != houseFailures)
                                throw new Exception($"{actor.appearance} left its actual floor during {branch} beside a Box: feet={actor.body.Y + actor.BodyHeight}, floor={source.Y}, gap={gap}.");
                        }
                    }
                }
            }
        }
        finally
        {
            foreach (var actor in residents) { actor.SetPaused(false); actor.CancelRoute(); actor.Occupancy.Release(actor.appearance); }
            foreach (var attraction in attractions) attraction.Actor.AllowCursorAttraction = attraction.Enabled;
            if (box is not null) { Furniture.Remove(box); box.Close(); }
            ConfigureHouse(3, false); platformCacheKey = int.MinValue;
        }
    }
}
