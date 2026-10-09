using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    internal Func<Guid, FoodServing?>? FoodForFurniture { get; set; }
    internal Func<Guid, PetAppearance, FoodReservation?>? ReserveFood { get; set; }
    internal Action<FoodReservation>? ReleaseFood { get; set; }
    internal Func<FoodContact, bool>? FoodContactRequested { get; set; }
    private FoodReservation? foodReservation;
    private double foodContactAge;
    public int FoodContactCount { get; private set; }

    internal bool TryStartFoodActivity()
    {
        if (FoodForFurniture is null || !IsVisible || paused || Interacting || EditingRoom || FinishingMotion || SequenceCommitted || body.Action == BodyAction.Sleep) return false;
        StartRoomActivity(RoomActivity.Feed);
        return foodReservation is not null && CurrentRoomActivity == RoomActivity.Feed;
    }

    private bool TryBeginFoodActivity()
    {
        var room = Furniture.Select(f => f.Item).ToArray();
        var candidates = Furniture.Where(f => !EditingRoom && FoodForFurniture!(f.Item.Id) is { RemainingPortions: > 0 } food &&
                FoodCatalog.CanServe(f.Item.Kind, food.Kind) && FoodCatalog.CanEat(appearance, food.Kind) &&
                Occupancy.Available(FurnitureCompatibility.Reservation(f.Item, appearance), appearance))
            .Select(f => (Window: f, Spot: ActivitySpot(f, RoomActivity.Feed, room)))
            .Where(c => CanReach(c.Spot.X, c.Spot.Feet) && CanTouchFoodAt(c.Window, c.Spot.X, c.Spot.Feet,
                appearance == PetAppearance.Girl ? RoomActivityPolicy.NearbyChair(c.Window.Item, room) : null))
            .OrderBy(c => Math.Abs(c.Spot.X - body.X - HalfWidth) + Math.Abs(c.Spot.Feet - body.Y - BodyHeight))
            .ToArray();
        foreach (var candidate in candidates)
        {
            var item = candidate.Window.Item;
            var reservation = ReserveFood?.Invoke(item.Id, appearance);
            if (reservation is null) continue;
            if (!Occupancy.TryAcquire(FurnitureCompatibility.Reservation(item, appearance), appearance))
            { ReleaseFood?.Invoke(reservation); continue; }
            var chair = appearance == PetAppearance.Girl ? RoomActivityPolicy.NearbyChair(item, room) : null;
            if (chair is not null && !Occupancy.TryAcquire(FurnitureCompatibility.Reservation(chair, appearance), appearance))
            { ReleaseFood?.Invoke(reservation); Occupancy.Release(appearance); continue; }
            foodReservation = reservation; foodContactAge = 0;
            homeTarget = item; homeX = candidate.Spot.X; homeFeet = candidate.Spot.Feet; activityChair = chair;
            roomActivity = RoomActivity.Feed; activityProduced = false; homeUseRecorded = false;
            sequence = new(SequenceKind.Activity, BehaviorPersonality, Bond, item.Id.ToString(), item.Kind,
                character: appearance, activity: RoomActivity.Feed, approachBudgetSeconds: EstimateApproachBudget(homeX, homeFeet, Parameters.Movement.Speed * .8));
            return true;
        }
        // Empty or unreachable furniture must not create an eating sequence on
        // the floor, and must not remain an Eat pose without an actual target.
        body.Action = BodyAction.Idle; poseAction = BodyAction.Idle; roomActivity = null;
        return true;
    }

    private bool FoodTargetAvailable() => foodReservation is {} reservation &&
        FoodForFurniture?.Invoke(reservation.FurnitureId) is { RemainingPortions: > 0 } serving && serving.ServingId == reservation.ServingId;

    private bool CanTouchFoodAt(RoomWindow surface, double center, double feet, RoomItem? chair)
    {
        // Initial eating-pose reach points, expressed in canonical character
        // coordinates. This is a bounded surface reach, not an inferred floor
        // contact: suspended bowls and distant seats cannot grant a bite.
        var x = appearance == PetAppearance.Girl ? 78 : 58;
        var y = appearance == PetAppearance.Girl ? chair is null ? 65 : 106 : 112;
        var reach = new System.Windows.Point(center + (x - 58) * BodyWidth / 116, feet + (y - 144) * BodyHeight / 144);
        return (reach - surface.FoodContactPoint).Length <= (appearance == PetAppearance.Girl ? 36 : 18) * sceneScale;
    }

    private void TickFoodContact(BehaviorSequence activity, double dt, bool reached)
    {
        var surface = Furniture.FirstOrDefault(f => f.Item.Id == foodReservation?.FurnitureId);
        var touching = activity.Phase == BehaviorPhase.Work && activity.InterruptedBy is null && reached && petGravity.Grounded &&
            IsVisible && !paused && !Interacting && !EditingRoom && !FinishingMotion && foodReservation is not null &&
            sprite.Visibility == System.Windows.Visibility.Visible && sprite.FrameIndex == 5 && surface is not null &&
            CanTouchFoodAt(surface, body.X + HalfWidth, body.Y + BodyHeight, activityChair);
        if (!touching) { foodContactAge = 0; return; }
        foodContactAge += Math.Clamp(dt, 0, .1);
        if (foodContactAge < 1) return;
        foodContactAge -= 1;
        var reservation = foodReservation!;
        if (FoodForFurniture?.Invoke(reservation.FurnitureId) is not {} serving ||
            FoodContactRequested?.Invoke(new(reservation.Token, reservation.FurnitureId, reservation.ServingId, appearance, serving.Revision, true)) != true)
        { activity.Interrupt(BehaviorInterruptReason.Safety); ReleaseFoodReservation(); return; }
        FoodContactCount++;
    }

    private void ReleaseFoodReservation()
    {
        if (foodReservation is {} reservation) ReleaseFood?.Invoke(reservation);
        foodReservation = null; foodContactAge = 0;
    }
}
