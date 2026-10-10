using System.Windows;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    private bool homeStressHoverInput;
    private Point? homeStressHoverPointer;

    // The isolated workload supplies routed input, never a reward. TickDirectCare
    // still uses its real timer interval and the production silhouette/exposure gates.
    internal bool TryGetHomeStressHoverPointer(out Point? pointer)
    { pointer=homeStressHoverPointer;return homeStressHoverInput; }
    internal void SetHomeStressHoverPointer(Point? pointer)
    { homeStressHoverInput=true;homeStressHoverPointer=pointer; }
    internal void EndHomeStressHoverInput()
    { homeStressHoverInput=false;homeStressHoverPointer=null;CancelDirectCare(); }
    internal bool HomeStressInputReady=>DirectCareBlocks()==DirectCareBlock.None&&!HouseMotionActive&&
        !HasFoodReservationDiagnostic;
    internal void RequestHomeStressIdle()
    {
        // An ordinary stimulus may wait for a safe sequence boundary. It must
        // not transplant a resident off stairs or a furniture approach.
        if(IsVisible&&!paused&&!Interacting&&!EditingRoom&&!FinishingMotion&&!HouseMotionActive&&
            petGravity.Grounded&&!HasFoodReservationDiagnostic)
            RequestAction(BodyAction.Idle,BehaviorInterruptReason.Stimulus);
    }
    internal bool HomeStressTeaserActive=>teaserTarget is not null&&sequence is {Kind:SequenceKind.Play,Finished:false};
    internal object HomeStressTeaserDiagnostic
    {
        get
        {
            var target = teaserTarget;
            return new
            {
                State = CaptureTeaserDiagnosticState(target),
                StartRejections = new Dictionary<string, int>(teaserStartRejections),
                ContactRejections = new Dictionary<string, int>(teaserContactRejections),
                LastRejectedContact = new Dictionary<string, object>(teaserLastRejectedContact)
            };
        }
    }
    private object CaptureTeaserDiagnosticState(RoomWindow? target)
    {
        HangingToyStance? stance = null;
        if (target is not null && TryTeaserStance(target, out var planned)) stance = planned;
        return new
        {
            Active = HomeStressTeaserActive, TargetId = target?.Item.Id, TargetFloor = target?.Item.FloorIndex,
            Sequence = sequence?.Kind.ToString(), Phase = sequence?.Phase.ToString(), PhaseAge = sequence?.PhaseAge,
            Age = sequence?.Age, InterruptedBy = sequence?.InterruptedBy?.ToString(), Position,
            Feet = body.Y + BodyHeight, petGravity.Grounded, FinishingMotion, RemainingHouseSteps = houseRoute.Count,
            BodyWidth, BodyHeight, SceneScale = sceneScale, Stance = stance,
            Ball = target?.TeaserPosition, Contact = target is null ? null : TeaserContact(target),
            RopeLength = target?.Teaser?.RopeLength, TargetLength = target?.Teaser?.TargetLength,
            SpriteFrame = sprite.FrameIndex, PresentedFrame = sprite.PresentedFrameIndex
        };
    }
    internal Guid? HomeStressFoodReservationToken=>foodReservation?.Token;
    internal bool HomeStressDogUsesFurniturePlatform=>appearance==PetAppearance.BorderCollie&&
        HomeStressTeaserActive&&teaserTarget is {} target&&target.Platforms.Any(p=>body.X+HalfWidth>=p.X&&body.X+HalfWidth<=p.X+p.Width&&
            Math.Abs(body.Y+BodyHeight-p.HeightAt(body.X+HalfWidth))<=sceneScale);
}
