using System.Windows;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    private BehaviorSequence? sequence;
    private readonly ToyInterest toyInterest=new();
    public PersonalityProfile BehaviorPersonality {get;set;}=new();
    public double Bond {get;set;}=15;
    public bool SequenceCommitted=>sequence is {Committed:true};
    public BehaviorPhase? CurrentPhase=>sequence?.Phase;
    public string? AttentionTarget=>sequence?.TargetId;
    private Point? attentionPoint;
    private bool phaseContact;
    private double stimulusElapsed;
    private ToyWindow? requestedToy;
    private readonly RestSpotPreference restPreference=new();
    private RestSpot? restSpot;
    private double? sequencePoseAge;
    private CareKind? requestedCare;
    private CareKind? activeCare;
    public void BeginCare(CareKind kind,BodyAction action)
    {
        requestedCare=kind;
        if(FinishingMotion){sequence?.Interrupt(BehaviorInterruptReason.Care);queuedAction=action;return;}
        SetAction(action);
    }
    private bool ShowHand=>activeCare==CareKind.Pet&&sequence?.Phase is BehaviorPhase.Accept or BehaviorPhase.React;
    private bool ShowComb=>activeCare==CareKind.Groom&&sequence?.Phase is BehaviorPhase.Relax or BehaviorPhase.Lean;
    public BodyAction PhysiologicalAction=>sequence is {Kind:SequenceKind.Box,Phase:BehaviorPhase.Rest}?BodyAction.Sit:sequence?.Kind==SequenceKind.Sleep&&sequence.Phase!=BehaviorPhase.Sleep?BodyAction.Sit:body.Action;
    private string ToyId(ToyWindow toy)=>toy.RoomItem?.Id.ToString()??(toy==Ball?"ball":"square");
    public bool RequestAction(BodyAction action,BehaviorInterruptReason reason)
    {
        if(Interacting)return false;
        if(sequence is {Finished:false})
        {
            if(!sequence.Interrupt(reason))return false;
            queuedAction=action;return true;
        }
        SetAction(action);return true;
    }
    private void BeginSequence(BodyAction action)
    {
        homeTarget=null;sequence=null;attentionPoint=null;phaseContact=false;activeCare=requestedCare;requestedCare=null;
        if(action==BodyAction.ObserveCursor&&TryBeginHome(SequenceKind.Observe,FurnitureUse.Observe))return;
        if(action==BodyAction.Stretch&&EmotionalState.Fatigue<85&&TryBeginHome(SequenceKind.Scratch,FurnitureUse.Scratch))return;
        if(action==BodyAction.Hide&&TryBeginHome(SequenceKind.Box,FurnitureUse.Hide))return;
        if(action is BodyAction.Groom or BodyAction.Nuzzle)
        {sequence=new(action==BodyAction.Nuzzle?SequenceKind.Stroke:activeCare==CareKind.Groom?SequenceKind.Brush:SequenceKind.SelfGroom,BehaviorPersonality,Bond);return;}
        if(action==BodyAction.Sleep)
        {
            var bounds=Bounds();var platforms=RoomPlatforms();
            var spots=Furniture.Where(f=>!EditingRoom).Where(f=>f.Context.Offers(FurnitureUse.Rest)||f.Context.Offers(FurnitureUse.Sleep)).SelectMany(f=>f.Platforms.Where(p=>Math.Abs(p.Y-p.EndY)<1).Select((p,i)=>new RestSpot(f.Item.Id+":"+i,f.Item.Kind,p.X+p.Width/2,p.Y))).ToList();
            spots.Add(new("quiet-left",null,bounds.Left+70,bounds.Top+bounds.Height));
            spots.Add(new("quiet-right",null,bounds.Left+bounds.Width-70,bounds.Top+bounds.Height));
            restSpot=restPreference.ChooseHome(spots.Where(s=>RoomNavigation.Plan(platforms,bounds,body.X+58,body.Y+144,s.X,s.Feet) is not null),BehaviorPersonality,Bond,body.X+58,cursor?.X,DateTimeOffset.UtcNow,EmotionalState.Fatigue,movementRandom);
            StartSleepAt(restSpot);return;
        }
        if(action is not (BodyAction.PlayToy or BodyAction.PseudoPushIcon))return;
        if(requestedToy is {} wanted){playTarget=wanted;teaserTarget=null;requestedToy=null;}
        if(action==BodyAction.PseudoPushIcon){playTarget=Square;teaserTarget=null;}
        var id=teaserTarget?.Item.Id.ToString()??ToyId(playTarget??Ball);
        if(toyInterest.Attraction(id,0)<=0)
        {
            teaserTarget=null;
            playTarget=AllToys.Where(t=>toyInterest.Attraction(ToyId(t),0)>0).OrderBy(t=>Math.Abs(t.Model.X-body.X)).FirstOrDefault();
            if(playTarget is null){body.Action=BodyAction.Sit;return;}id=ToyId(playTarget);
        }
        sequence=new(SequenceKind.Play,BehaviorPersonality,Bond,id);
    }
    private void EndSequence()
    {
        if(sequence is {} completed)Routine.Finished(completed.Kind);
        if(sequence is {Kind:SequenceKind.Play,TargetId:{} id})toyInterest.Finish(id);
        sequence=null;attentionPoint=null;teaserPawTarget=null;body.Action=BodyAction.Idle;poseAction=BodyAction.Idle;CancelRoute();
        sequencePoseAge=null;activeCare=null;homeTarget=null;feline.Clip=null;
    }
    private void TickToyAttention(double dt)
    {
        toyInterest.Step(dt);stimulusElapsed+=dt;
        if(!AllowCursorAttraction||paused||Interacting||stimulusElapsed<6||(SequenceCommitted&&!(sequence?.Kind==SequenceKind.SelfGroom&&sequence.Age>=3))||body.Action==BodyAction.Sleep)return;
        var fast=AllToys.Where(t=>!t.Model.Held&&Math.Abs(t.Model.VelocityX)+Math.Abs(t.Model.VelocityY)>180&&Math.Abs(t.Model.X-body.X)<320&&toyInterest.Attraction(ToyId(t),300)>0).OrderBy(t=>Math.Abs(t.Model.X-body.X)).FirstOrDefault();
        if(fast is null)return;stimulusElapsed=0;requestedToy=fast;
        InteractionRequested?.Invoke(BodyAction.PlayToy);
    }
    private bool TickSequence(double dt)
    {
        if(sequence is not { } s)return false;
        if(s.Finished){EndSequence();return true;}
        if(s.Kind is SequenceKind.Box or SequenceKind.Scratch or SequenceKind.Observe)return TickHomeSequence(s,dt);
        if(s.Kind==SequenceKind.Sleep)return TickSleepSequence(s,dt);
        if(s.Kind is SequenceKind.Stroke or SequenceKind.Brush or SequenceKind.SelfGroom)return TickCareSequence(s,dt);
        if(s.Kind!=SequenceKind.Play)return false;
        var toy=playTarget??Ball;var teaser=teaserTarget;
        var exists=teaser is not null?Furniture.Contains(teaser)&&s.TargetId==teaser.Item.Id.ToString():AllToys.Contains(toy)&&s.TargetId==ToyId(toy);
        var point=teaser?.TeaserPosition??new Point(toy.Model.X+20,toy.Model.Y+20);
        attentionPoint=point;
        var approach=teaser is not null?new ObjectApproach(teaser.Left+25,teaser.Platforms[1].Y-144,-1,0):ToyApproach(toy);
        var reached=Math.Abs(body.X-approach.X)<1.5&&Math.Abs(body.Y-approach.Y)<4;
        var before=s.Phase;
        s.Step(dt,new(TargetExists:exists,Reached:reached,Contact:phaseContact,NavigationFailed:MovementPhase==NavigationPhase.Unreachable,Grounded:petGravity.Grounded,TargetSpeed:Math.Abs(toy.Model.VelocityX)+Math.Abs(toy.Model.VelocityY)));
        if(s.Phase!=before){phaseContact=false;if(s.Phase==BehaviorPhase.Recover)CancelRoute();}
        poseAction=s.Phase==BehaviorPhase.Approach?BodyAction.Walk:s.Phase==BehaviorPhase.WatchBall?BodyAction.Sit:BodyAction.ObserveCursor;
        if(s.Phase is BehaviorPhase.Notice or BehaviorPhase.Orient or BehaviorPhase.Watch or BehaviorPhase.Prepare or BehaviorPhase.Crouch or BehaviorPhase.Paw or BehaviorPhase.Contact)
            facing=point.X<body.X+58?-1:1;
        if(s.Phase==BehaviorPhase.Approach)MovePetToward(approach.X,approach.Y,dt,Bounds(),Parameters.Movement.Speed*s.Style.ApproachSpeed);
        if(s.Phase is BehaviorPhase.Prepare or BehaviorPhase.Crouch)poseAction=BodyAction.Sit;
        if(s.Phase is BehaviorPhase.Paw or BehaviorPhase.Contact)
        {
            var local=new Point(point.X-body.X,point.Y-body.Y);
            poseAction=BodyAction.BatToy;
            var reach=s.Phase==BehaviorPhase.Paw?Math.Clamp(s.PhaseAge/.25,0,1):1;
            var ready=new Point(facing>0?82:34,112);
            teaserPawTarget=ready+(local-ready)*reach;
            // A physical impulse requires a painted, reachable paw target, after the visible wind-up.
            if(s.Phase==BehaviorPhase.Contact&&!phaseContact&&!feline.IsTurning&&petGravity.Grounded&&local.X is >=6 and <=110&&local.Y is >=72 and <=140)
            {
                if(teaser?.Teaser is {} hanging){hanging.Bat(-125);TeaserContactCount++;phaseContact=true;}
                else if(!toy.Model.Held&&TouchingToy(toy)){var kick=ToyContactPhysics.Push(toy.Model.X,Bounds(),approach.PushX,Parameters.Play.Force);toy.Model.Kick(kick.X,kick.Y);phaseContact=true;lastToyKick=clock.Elapsed.TotalSeconds;}
            }
        }
        if(s.Phase==BehaviorPhase.Complete)EndSequence();
        return true;
    }
    private bool TickSleepSequence(BehaviorSequence s,double dt)
    {
        var exists=restSpot is null||restSpot.Kind is null||Furniture.Any(f=>f.Item==homeTarget)&&!EditingRoom;
        var reached=restSpot is null||Math.Abs(body.X+58-restSpot.X)<3&&Math.Abs(body.Y+144-restSpot.Feet)<4;
        var before=s.Phase;
        s.Step(dt,new(TargetExists:exists,Reached:reached,NavigationFailed:MovementPhase==NavigationPhase.Unreachable,Grounded:petGravity.Grounded,Rested:EmotionalState.Energy>=30&&EmotionalState.Fatigue<=75));
        if(before!=s.Phase&&s.Phase==BehaviorPhase.Sleep&&restSpot is {} used){restPreference.Used(used.Id);LearnHome(FurnitureUse.Sleep);}
        if(s.TargetId is null){restSpot=null;homeTarget=null;}
        attentionPoint=restSpot is {} spot?new(spot.X,spot.Feet):null;
        poseAction=s.Phase switch
        {
            BehaviorPhase.Approach=>BodyAction.Walk,
            BehaviorPhase.LieDown or BehaviorPhase.Curl or BehaviorPhase.Sleep=>BodyAction.Sleep,
            BehaviorPhase.Stretch=>BodyAction.Stretch,
            BehaviorPhase.LickPaw or BehaviorPhase.WashFace=>appearance==PetAppearance.Cat?BodyAction.Groom:BodyAction.Sit,
            BehaviorPhase.Watch=>BodyAction.ObserveCursor,
            BehaviorPhase.Settle or BehaviorPhase.Knead=>BodyAction.Sit,
            BehaviorPhase.Inspect or BehaviorPhase.Search=>BodyAction.ObserveCursor,
            _=>BodyAction.Sit
        };
        sequencePoseAge=s.Phase switch{BehaviorPhase.LickPaw=>.5+s.PhaseAge*.4,BehaviorPhase.WashFace=>1.5+s.PhaseAge*.7,BehaviorPhase.LieDown=>5,BehaviorPhase.Curl or BehaviorPhase.Sleep=>25+s.PhaseAge%15,BehaviorPhase.Stretch=>s.PhaseAge,_=>s.PhaseAge};
        if(s.Phase==BehaviorPhase.Approach&&restSpot is {} at)MovePetToward(at.X-58,at.Feet-144,dt,Bounds(),Parameters.Movement.Speed*.8);
        if(s.Phase==BehaviorPhase.Complete)EndSequence();return true;
    }
    private bool TickCareSequence(BehaviorSequence s,double dt)
    {
        var before=s.Phase;s.Step(dt,new(Grounded:petGravity.Grounded));
        if(s.Kind!=SequenceKind.SelfGroom&&cursor is {} at)attentionPoint=new(at.X,at.Y);
        if(before!=s.Phase&&s.Phase is BehaviorPhase.React or BehaviorPhase.Lean&&appearance==PetAppearance.Cat&&movementRandom.NextDouble()<s.Style.PurrChance)
            SoundRequested?.Invoke(PetSound.Purr,.55+Bond/250);
        poseAction=s.Phase switch
        {
            BehaviorPhase.React=>Bond<25?BodyAction.Sit:BodyAction.Nuzzle,
            BehaviorPhase.Relax=>BodyAction.Sit,
            BehaviorPhase.Lean=>BodyAction.Nuzzle,
            BehaviorPhase.LickPaw or BehaviorPhase.WashFace or BehaviorPhase.GroomBody=>BodyAction.Groom,
            BehaviorPhase.Accept or BehaviorPhase.Pause or BehaviorPhase.Recover=>BodyAction.Sit,
            _=>BodyAction.ObserveCursor
        };
        if(appearance!=PetAppearance.Cat&&poseAction==BodyAction.Groom)poseAction=BodyAction.Sit;
        sequencePoseAge=s.Phase switch{BehaviorPhase.LickPaw=>.5+s.PhaseAge*.4,BehaviorPhase.WashFace=>1.5+s.PhaseAge*.7,BehaviorPhase.GroomBody=>3.5,_=>s.PhaseAge};
        if(petGravity.Grounded&&cursor is {} pointer&&s.Kind==SequenceKind.Stroke)
        {
            var sign=pointer.X>body.X+58?1:-1;
            if(s.Phase==BehaviorPhase.Hesitate)body.MoveToward(body.X-sign*s.Style.Distance,body.Y,dt,Bounds(),s.Style.Distance);
            if(s.Phase==BehaviorPhase.React&&Bond>=65)body.MoveToward(body.X+sign*4,body.Y,dt,Bounds(),10);
        }
        if(s.Phase==BehaviorPhase.Complete)EndSequence();return true;
    }
}
