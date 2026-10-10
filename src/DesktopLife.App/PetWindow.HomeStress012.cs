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
    internal Guid? HomeStressFoodReservationToken=>foodReservation?.Token;
    internal bool HomeStressDogUsesFurniturePlatform=>appearance==PetAppearance.BorderCollie&&
        HomeStressTeaserActive&&teaserTarget is {} target&&target.Platforms.Any(p=>body.X+HalfWidth>=p.X&&body.X+HalfWidth<=p.X+p.Width&&
            Math.Abs(body.Y+BodyHeight-p.HeightAt(body.X+HalfWidth))<=sceneScale);
}
