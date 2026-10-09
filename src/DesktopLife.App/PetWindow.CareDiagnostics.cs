using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    internal void PrepareCareMenuDiagnostic(int index)
    {
        SetSimulationEnabled(false);SetPaused(false);SetRoomEditing(false);ResetPosition();
        DismissCareFeedback();interactionUntil=0;cursor=null;previousCursor=null;AllowCursorAttraction=false;
        var bounds=Bounds();var x=bounds.Left+Math.Min(100+index*180,Math.Max(0,bounds.Width-BodyWidth-40));
        body.Place(x,NavigationFloor-BodyHeight,bounds);petGravity.VX=petGravity.VY=0;StepPetGravity(.016);
        var parameters=BehaviorParameters.Default with
        {
            Seed=70,Play=new(110,.3),
            Movement=BehaviorParameters.Default.Movement with{Speed=85,PauseFrequency=0,DirectionChange=0,PathCurvature=0}
        };
        ParameterFactory=_=>parameters;Parameters=parameters;ApplyPosition();ApplyPose();
    }

    internal void ClickCareMenuDiagnostic(CareKind kind)
    {
        var menu=Character.ContextMenu??throw new Exception("Character care menu missing.");
        var item=menu.Items.OfType<MenuItem>().Single(i=>i.Tag is CareKind value&&value==kind);
        // Dispatch the production menu's routed Click event and its real subscriber.
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent,item));
    }

    internal void PlaceCareToyDiagnostic(ToyWindow toy,double offset)
    {
        toy.Model.Held=false;toy.Platforms=RoomPlatforms();toy.RoomBounds=ToyBounds;
        toy.Model.Place(body.X+offset,NavigationFloor-40,ToyBounds);toy.Model.Kick(0,0);toy.Step(0);
    }

    internal void CoolCareToyDiagnostic(ToyWindow toy)=>toyInterest.Finish(ToyId(toy));
    internal string CareToyIdDiagnostic(ToyWindow toy)=>ToyId(toy);
    internal void RecentDragCareDiagnostic()=>interactionUntil=clock.Elapsed.TotalSeconds+2;
    internal void RecoveryCareDiagnostic()=>RecoverNavigation("care-menu-regression");
    internal void AssertBusyCareLeavesNoQueueDiagnostic()
    {
        if(!FinishingMotion)throw new Exception("Busy care regression requires active safety recovery.");
        BeginCare(CareKind.Groom,BodyAction.Groom);
        if(requestedCare is not null||activeCare is not null||queuedAction is not null||MovementPhase!=NavigationPhase.Recovering)
            throw new Exception("Direct busy care left a marker/queued action or cancelled safety recovery.");
    }
    internal void SeedStaleCareDiagnostic()
    {queuedAction=BodyAction.Sleep;requestedActivity=RoomActivity.Rest;requestedToy=Square;}

    internal object AdvanceCareMenuDiagnostic(CareKind kind)
    {
        if(sequence is null||activeCare!=kind||requestedCare is not null||queuedAction is not null)
            throw new Exception($"{appearance}/{kind} did not own a clean care sequence.");
        var active=sequence;var phases=new HashSet<BehaviorPhase>();var watch=Stopwatch.StartNew();
        for(var frame=0;frame<15000;frame++)
        {
            if(watch.Elapsed>TimeSpan.FromSeconds(6))throw new Exception($"{appearance}/{kind} care choreography exceeded its native test budget.");
            houseControlledThisFrame=false;navigationStepped=false;poseAction=null;teaserPawTarget=null;
            TickSequence(.016);ObserveNavigationProgress(.016);StepPetGravity(.016);ApplyPosition();
            ApplyPose(clock.Elapsed.TotalSeconds+(frame+1)*.016);UpdateLayout();
            phases.Add(active.Phase);
            var rendered=new RenderTargetBitmap(Math.Max(1,(int)Width),Math.Max(1,(int)Height),96,96,PixelFormats.Pbgra32);
            rendered.Render((Visual)Content);
            var visible=kind switch
            {
                CareKind.Feed=>active.Phase==BehaviorPhase.Work&&sprite.Visibility==Visibility.Visible&&sprite.FrameIndex==5&&
                    (appearance==PetAppearance.Girl||FoodBowl.Visibility==Visibility.Visible),
                CareKind.Pet=>PettingHand.Visibility==Visibility.Visible,
                CareKind.Groom when appearance==PetAppearance.Girl=>active.Phase==BehaviorPhase.Work&&CurrentRoomActivity==RoomActivity.HairCare&&sprite.FrameIndex==3,
                CareKind.Groom=>GroomComb.Visibility==Visibility.Visible,
                CareKind.Rest=>active.Phase==BehaviorPhase.Sleep&&SleepMark.Visibility==Visibility.Visible&&sprite.FrameIndex==4,
                CareKind.Play when appearance==PetAppearance.Girl=>active.Phase==BehaviorPhase.Work&&CurrentRoomActivity is not null,
                CareKind.Play=>phaseContact&&Math.Abs((playTarget??Ball).Model.VelocityX)>20,
                _=>false
            };
            if(visible)
            {
                if(!petGravity.Grounded)throw new Exception($"{appearance}/{kind} performed care without support.");
                if(kind==CareKind.Play&&appearance==PetAppearance.Cat&&
                    (teaserPawTarget is not {} target||feline.RenderedPawTip is not {} tip||(target-tip).Length>2))
                    throw new Exception("Manual cat play contact did not match the painted paw.");
                if(RequestAction(BodyAction.ChaseCursor,BehaviorInterruptReason.Stimulus)||!ReferenceEquals(sequence,active)||queuedAction is not null)
                    throw new Exception($"{appearance}/{kind} care was replaced by cursor stimulus.");
                return new{Character=appearance.ToString(),Care=kind.ToString(),SimulatedSeconds=(frame+1)*.016,
                    Phases=phases.Select(p=>p.ToString()).ToArray(),VisibleEffect=true,Grounded=true,SpriteFrame=sprite.FrameIndex};
            }
            foreach(var toy in AllToys)toy.Step(.016);
            if(sequence is null||active.InterruptedBy is not null)
                throw new Exception($"{appearance}/{kind} ended without its visible care effect: phase={active.Phase}, interrupted={active.InterruptedBy}.");
        }
        throw new Exception($"{appearance}/{kind} did not reach visible care: phase={active.Phase}, position={Position}, navigation={MovementPhase}.");
    }
}
