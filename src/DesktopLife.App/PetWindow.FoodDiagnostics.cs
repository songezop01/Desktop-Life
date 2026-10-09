using DesktopLife.Core;
using System.Diagnostics;

namespace DesktopLife.App;

public partial class PetWindow
{
    internal bool HasFoodReservationDiagnostic => foodReservation is not null;
    internal void StartFoodDiagnostic() => StartRoomActivity(RoomActivity.Feed);
    internal void FinishFoodDiagnostic(int portions, bool expectContact)
    {
        var initial = FoodContactCount;
        var started = Stopwatch.StartNew();
        for (var frame = 0; frame < 15000; frame++)
        {
            if (started.Elapsed > TimeSpan.FromSeconds(6)) throw new Exception("Food native component exceeded its bounded test time.");
            houseControlledThisFrame = false; navigationStepped = false; poseAction = null;
            TickSequence(.016); ObserveNavigationProgress(.016); StepPetGravity(.016); ApplyPosition();
            ApplyPose(clock.Elapsed.TotalSeconds + (frame + 1) * .016); UpdateLayout();
            if (FoodContactCount - initial >= portions && expectContact) return;
            if (sequence is null)
            {
                if (expectContact) throw new Exception($"Food sequence ended before contact: {appearance}, contacts={FoodContactCount - initial}, nav={MovementPhase}.");
                if (FoodContactCount != initial) throw new Exception("Cancelled food produced a contact.");
                return;
            }
        }
        throw new Exception($"Food sequence did not settle: {appearance}, {CurrentPhase}, {MovementPhase}.");
    }
}
