using System.Windows;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    public event Action<CompletedBehavior>? BehaviorCompleted;
    public bool AutonomousIntent {get;set;}=true;
    private bool sequenceAutonomous;
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
    public int GenericPlayContactCount {get;private set;}
    public int PlayTargetRevisionCount {get;private set;}
    public Dictionary<PlayApproachStoppedReason,int> PlayApproachStops {get;}=[];
    public int ApproachTimeouts {get;private set;}
    public List<object> ApproachTimeoutDetails {get;}=[];
    private BehaviorSequence? lastTimedOutSequence;
    private void RecordApproachTimeout(BehaviorSequence s,BehaviorPhase? phaseBeforeStep=null,double? phaseAgeBeforeStep=null)
    {
        if(!s.ApproachTimedOut||ReferenceEquals(s,lastTimedOutSequence))return;
        lastTimedOutSequence=s;ApproachTimeouts++;
        if(ApproachTimeoutDetails.Count==32)ApproachTimeoutDetails.RemoveAt(0);
        var toy=s.Kind==SequenceKind.Play?playTarget??Ball:null;
        ObjectApproach? approach=s.Kind!=SequenceKind.Play?null:teaserTarget is {} teaser?new ObjectApproach(teaser.Item.X+25,teaser.Platforms[1].Y-BodyHeight,-1,0):ToyApproach(toy!);
        ApproachTimeoutDetails.Add(new{RecordedUtc=DateTimeOffset.UtcNow,Character=appearance.ToString(),Kind=s.Kind.ToString(),Activity=s.Activity?.ToString(),s.TargetId,s.Age,s.PhaseAge,s.ApproachBudgetSeconds,PhaseBeforeStep=phaseBeforeStep?.ToString(),PhaseAgeBeforeStep=phaseAgeBeforeStep,BeforeNavigationCleanup=phaseBeforeStep is not null,Position=Position,Feet=body.Y+BodyHeight,Grounded=petGravity.Grounded,Navigation=MovementPhase.ToString(),BodyWidth,BodyHeight,SceneScale=sceneScale,MovementSpeed=Parameters.Movement.Speed,TargetApproach=approach,ToyState=toy is null?null:new{Id=ToyId(toy),toy.Model.X,toy.Model.Y,toy.Model.VelocityX,toy.Model.VelocityY,toy.Model.Held,toy.Model.IsResting},TeaserPosition=teaserTarget?.TeaserPosition,RoomGoal=routeGoal is {} roomGoal?new RoomPoint(roomGoal.X,roomGoal.Y):null,RoomStep=route.TryPeek(out var roomStep)?roomStep:(RoomWaypoint?)null,HomeTarget=homeTarget,HomeSpot=new RoomPoint(homeX,homeFeet),RestSpot=restSpot,HouseGoal=houseGoal is {} goal?new RoomPoint(goal.X,goal.Feet):null,HouseStep=houseRoute.TryPeek(out var step)?step:null,RemainingHouseSteps=houseRoute.ToArray()});
    }
    private bool CanPlayToy(ToyWindow toy)=>toy.RoomItem is not {} item||FurnitureCompatibility.CanUse(appearance,item.Kind,FurnitureUse.Play);
    private bool CanPlayTeaser(RoomWindow? target)=>appearance==PetAppearance.Cat&&target?.Teaser is not null&&FurnitureCompatibility.CanUse(appearance,target.Item.Kind,FurnitureUse.Play);
    private BodyAction PlayContactPose=>appearance==PetAppearance.Cat?BodyAction.BatToy:BodyAction.PlayToy;
    public void BeginCare(CareKind kind,BodyAction action)
    {
        requestedCare=kind;
        if(FinishingMotion){sequence?.Interrupt(BehaviorInterruptReason.Care);queuedAction=action;return;}
        SetAction(action);
    }
    private bool ShowHand=>activeCare==CareKind.Pet&&sequence?.Phase is BehaviorPhase.Accept or BehaviorPhase.React;
    private bool ShowComb=>activeCare==CareKind.Groom&&sequence?.Phase is BehaviorPhase.Relax or BehaviorPhase.Lean;
    public BodyAction PhysiologicalAction=>CompanionActivity.Resolve(body.Action,sequence,IsVisible,paused,Interacting,EditingRoom,petGravity.Grounded,PhysiologicalPlayContact());
    private bool PhysiologicalPlayContact()
    {
        if(sequence is not null)return phaseContact;
        if(body.Action!=BodyAction.PlayToy||!petGravity.Grounded)return false;
        var toy=playTarget??Ball;
        return CanPlayToy(toy)&&!toy.Model.Held&&TouchingToy(toy);
    }
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
        if(sequence is {Finished:false})BehaviorCompleted?.Invoke(new(LifeBehavior.Observe,false,false,0,false));
        homeTarget=null;sequence=null;attentionPoint=null;phaseContact=false;activeCare=requestedCare;requestedCare=null;
        sequenceAutonomous=AutonomousIntent&&activeCare is null;
        if(TryBeginRoomActivity(action))return;
        if(action==BodyAction.ObserveCursor&&TryBeginHome(SequenceKind.Observe,FurnitureUse.Observe))return;
        if(action==BodyAction.Stretch&&EmotionalState.Fatigue<85&&TryBeginHome(SequenceKind.Scratch,FurnitureUse.Scratch))return;
        if(action==BodyAction.Hide&&TryBeginHome(SequenceKind.Box,FurnitureUse.Hide))return;
        if(action is BodyAction.Groom or BodyAction.Nuzzle)
        {sequence=new(action==BodyAction.Nuzzle?SequenceKind.Stroke:activeCare==CareKind.Groom?SequenceKind.Brush:SequenceKind.SelfGroom,BehaviorPersonality,Bond,character:appearance);return;}
        if(action==BodyAction.Sleep)
        {
            var bounds=Bounds();var platforms=RoomPlatforms();
            var spots=Furniture.Where(f=>!EditingRoom&&Occupancy.Available(FurnitureCompatibility.Reservation(f.Item,appearance),appearance)).Where(f=>FurnitureCompatibility.CanUse(appearance,f.Item.Kind,FurnitureUse.Rest)||FurnitureCompatibility.CanUse(appearance,f.Item.Kind,FurnitureUse.Sleep)).SelectMany(f=>f.Platforms.Where(p=>Math.Abs(p.Y-p.EndY)<1).Select((p,i)=>new RestSpot(f.Item.Id+":"+i,f.Item.Kind,p.X+p.Width*(f.Item.Kind is FurnitureKind.HumanBed or FurnitureKind.Sofa?(appearance==PetAppearance.Girl?.23:appearance==PetAppearance.BorderCollie?.5:.77):.5),p.Y))).ToList();
            spots.Add(new("quiet-left",null,bounds.Left+Math.Max(HalfWidth,70*sceneScale),LocalFloor));
            spots.Add(new("quiet-right",null,bounds.Left+bounds.Width-Math.Max(HalfWidth,70*sceneScale),LocalFloor));
            var reachable=spots.Where(s=>CanReach(s.X,s.Feet)).ToArray();
            if((appearance is PetAppearance.Girl or PetAppearance.BorderCollie)&&reachable.Length>0)
            {
                int Rank(RestSpot s)=>s.Kind is {} kind?FurnitureCompatibility.Priority(appearance,kind,FurnitureUse.Rest):10;
                var rank=reachable.Min(Rank);reachable=reachable.Where(s=>Rank(s)==rank).ToArray();
            }
            restSpot=restPreference.ChooseHome(reachable,BehaviorPersonality,Bond,body.X+HalfWidth,cursor?.X,DateTimeOffset.UtcNow,EmotionalState.Fatigue,movementRandom);
            StartSleepAt(restSpot);return;
        }
        if(action is not (BodyAction.PlayToy or BodyAction.PseudoPushIcon))return;
        if(requestedToy is {} wanted){if(CanPlayToy(wanted))playTarget=wanted;teaserTarget=null;requestedToy=null;}
        if(!CanPlayTeaser(teaserTarget))teaserTarget=null;
        if(playTarget is {} selected&&!CanPlayToy(selected))playTarget=null;
        playTarget??=AllToys.Where(CanPlayToy).OrderBy(toy=>Math.Abs(toy.Model.X-body.X)).FirstOrDefault();
        if(action==BodyAction.PseudoPushIcon){playTarget=Square;teaserTarget=null;}
        var id=teaserTarget?.Item.Id.ToString()??ToyId(playTarget??Ball);
        if(toyInterest.Attraction(id,0)<=0)
        {
            teaserTarget=null;
            playTarget=AllToys.Where(t=>CanPlayToy(t)&&toyInterest.Attraction(ToyId(t),0)>0).OrderBy(t=>Math.Abs(t.Model.X-body.X)).FirstOrDefault();
            if(playTarget is null){body.Action=BodyAction.Sit;return;}id=ToyId(playTarget);
        }
        if(!Occupancy.TryAcquire(id,appearance)){body.Action=BodyAction.ObserveCursor;teaserTarget=null;playTarget=null;return;}
        var approach=teaserTarget is {} teaser?new ObjectApproach(teaser.Item.X+25,teaser.Platforms[1].Y-BodyHeight,-1,0):ToyApproach(playTarget??Ball);
        sequence=new(SequenceKind.Play,BehaviorPersonality,Bond,id,character:appearance,approachBudgetSeconds:EstimateApproachBudget(approach.X+HalfWidth,approach.Y+BodyHeight,Parameters.Movement.Speed*SequenceStyle.From(BehaviorPersonality,Bond).ApproachSpeed));
    }
    private void EndSequence()
    {
        Occupancy.Release(appearance);
        if(sequence is {} completed)
        {
            RecordApproachTimeout(completed);
            PlayTargetRevisionCount+=completed.PlayTargetRevisions;
            if(completed.PlayApproachStopped is {} stopped)PlayApproachStops[stopped]=PlayApproachStops.GetValueOrDefault(stopped)+1;
            Routine.Finished(completed.Kind);
            var behavior=completed.Kind switch{SequenceKind.Activity=>completed.Activity switch{RoomActivity.Feed=>LifeBehavior.Eat,RoomActivity.HairCare=>LifeBehavior.Groom,RoomActivity.Rest=>LifeBehavior.Rest,_=>LifeBehavior.Play},SequenceKind.Sleep=>LifeBehavior.Wake,SequenceKind.Play=>LifeBehavior.Play,SequenceKind.Scratch=>LifeBehavior.Scratch,SequenceKind.Box=>LifeBehavior.Explore,SequenceKind.SelfGroom or SequenceKind.Brush=>LifeBehavior.Groom,SequenceKind.Stroke=>LifeBehavior.Social,_=>LifeBehavior.Observe};
            var success=completed.Finished&&completed.InterruptedBy is null&&(completed.Kind!=SequenceKind.Play||completed.Contacts>0&&completed.PlayApproachStopped is null);
            BehaviorCompleted?.Invoke(new(behavior,success,sequenceAutonomous,completed.Age,cursor is {} c&&Math.Abs(c.X-body.X-HalfWidth)<180&&Math.Abs(c.Y-body.Y-80)<220));
        }
        if(sequence is {Kind:SequenceKind.Play,TargetId:{} id})toyInterest.Finish(id);
        sequence=null;attentionPoint=null;teaserPawTarget=null;body.Action=BodyAction.Idle;poseAction=BodyAction.Idle;CancelRoute(preserveRecovery:true);
        sequencePoseAge=null;activeCare=null;homeTarget=null;feline.Clip=null;
    }
    private void TickToyAttention(double dt)
    {
        toyInterest.Step(dt);stimulusElapsed+=dt;
        if(!AllowCursorAttraction||paused||Interacting||stimulusElapsed<6||(SequenceCommitted&&!(sequence?.Kind==SequenceKind.SelfGroom&&sequence.Age>=3))||body.Action==BodyAction.Sleep)return;
        var fast=AllToys.Where(t=>CanPlayToy(t)&&!t.Model.Held&&Math.Abs(t.Model.VelocityX)+Math.Abs(t.Model.VelocityY)>180&&Math.Abs(t.Model.X-body.X)<320&&toyInterest.Attraction(ToyId(t),300)>0).OrderBy(t=>Math.Abs(t.Model.X-body.X)).FirstOrDefault();
        if(fast is null)return;stimulusElapsed=0;requestedToy=fast;
        InteractionRequested?.Invoke(BodyAction.PlayToy);
    }
    private bool TickSequence(double dt)
    {
        if(sequence is not { } s)return false;
        if(s.Finished){EndSequence();return true;}
        if(s.Kind==SequenceKind.Activity)return TickRoomActivity(s,dt);
        if(s.Kind is SequenceKind.Box or SequenceKind.Scratch or SequenceKind.Observe)return TickHomeSequence(s,dt);
        if(s.Kind==SequenceKind.Sleep)return TickSleepSequence(s,dt);
        if(s.Kind is SequenceKind.Stroke or SequenceKind.Brush or SequenceKind.SelfGroom)return TickCareSequence(s,dt);
        if(s.Kind!=SequenceKind.Play)return false;
        var toy=playTarget??Ball;var teaser=teaserTarget;
        var exists=teaser is not null?CanPlayTeaser(teaser)&&Furniture.Contains(teaser)&&s.TargetId==teaser.Item.Id.ToString():CanPlayToy(toy)&&!toy.Model.Held&&AllToys.Contains(toy)&&s.TargetId==ToyId(toy);
        var point=teaser?.TeaserPosition??new Point(toy.Model.X+20,toy.Model.Y+20);
        attentionPoint=point;
        ObjectApproach approach;var available=true;
        if(teaser is not null)approach=new(teaser.Item.X+25,teaser.Platforms[1].Y-BodyHeight,-1,0);
        else available=TryToyApproach(toy,out approach);
        if(exists&&teaser is null&&s.Phase==BehaviorPhase.Approach)
        {
            if(!available&&toy.Model.IsResting)s.WatchUnavailableToy();
            else if(available)
            {
                var target=new RoomPoint(toy.Model.X,toy.Model.Y);var feet=approach.Y+BodyHeight;var floor=BaseFloor(feet);
                if(s.NeedsPlayTargetRevision(target,floor,feet))
                    s.RevisePlayApproach(target,floor,feet,EstimateApproachBudget(approach.X+HalfWidth,feet,Parameters.Movement.Speed*s.Style.ApproachSpeed));
            }
        }
        if(s.Phase==BehaviorPhase.WatchBall&&s.PlayApproachStopped is not null)
        {
            phaseContact=false;
            if(StopHousePursuit(dt,Parameters.Movement.Speed*s.Style.ApproachSpeed))return true;
            CancelRoute();
        }
        if(s.Phase==BehaviorPhase.Recover&&StopHousePursuit(dt,Parameters.Movement.Speed*s.Style.ApproachSpeed))return true;
        var reached=available&&(teaser is not null||TouchingToy(toy))&&Math.Abs(body.X-approach.X)<Math.Min(1.5,BodyHeight/144*.75)&&Math.Abs(body.Y-approach.Y)<4;
        var before=s.Phase;var beforeAge=s.PhaseAge;
        s.Step(dt,new(TargetExists:exists,Reached:reached,Contact:phaseContact,NavigationFailed:MovementPhase==NavigationPhase.Unreachable,Grounded:petGravity.Grounded,TargetSpeed:Math.Abs(toy.Model.VelocityX)+Math.Abs(toy.Model.VelocityY)));
        RecordApproachTimeout(s,before,beforeAge);
        if(s.Phase!=before)
        {
            phaseContact=false;
            if(s.Phase==BehaviorPhase.Recover)
            {
                if(StopHousePursuit(dt,Parameters.Movement.Speed*s.Style.ApproachSpeed))return true;
                CancelRoute();
            }
        }
        poseAction=s.Phase==BehaviorPhase.Approach?BodyAction.Walk:s.Phase==BehaviorPhase.WatchBall?BodyAction.Sit:BodyAction.ObserveCursor;
        if(s.Phase is BehaviorPhase.Notice or BehaviorPhase.Orient or BehaviorPhase.Watch or BehaviorPhase.Prepare or BehaviorPhase.Crouch or BehaviorPhase.Paw or BehaviorPhase.Contact)
            facing=point.X<body.X+HalfWidth?-1:1;
        if(s.Phase==BehaviorPhase.Approach)MovePetToward(approach.X,approach.Y,dt,Bounds(),Parameters.Movement.Speed*s.Style.ApproachSpeed);
        if(s.Phase is BehaviorPhase.Prepare or BehaviorPhase.Crouch)poseAction=BodyAction.Sit;
        if(s.Phase is BehaviorPhase.Paw or BehaviorPhase.Contact)
        {
            var bodyScale=BodyHeight/CharacterGeometry.CanonicalHeight;
            var contact=teaser is not null?new RoomPoint(point.X,point.Y):ToyContactPhysics.ReachableContactPoint(toy.Model.X,toy.Model.Y,body.X,body.Y,BodyHeight,appearance==PetAppearance.Cat?72:96);
            var local=new Point(((contact?.X??point.X)-body.X)/bodyScale,((contact?.Y??point.Y)-body.Y)/bodyScale);
            poseAction=PlayContactPose;
            if(appearance==PetAppearance.Cat)
            {
                var reach=s.Phase==BehaviorPhase.Paw?Math.Clamp(s.PhaseAge/.25,0,1):1;
                var ready=new Point(facing>0?82:34,112);
                teaserPawTarget=contact is not null?ready+(local-ready)*reach:ready;
                // Cat contact retains its painted, reachable paw target and wind-up.
                if(s.Phase==BehaviorPhase.Contact&&!phaseContact&&contact is not null&&!feline.IsTurning&&petGravity.Grounded&&
                    (teaser is null||local.X is >=6 and <=110&&local.Y is >=72 and <=140))
                {
                    if(teaser?.Teaser is {} hanging){hanging.Bat(-125);TeaserContactCount++;phaseContact=true;}
                    else if(!toy.Model.Held&&TouchingToy(toy)){var kick=ToyContactPhysics.Push(toy.Model.X,Bounds(),approach.PushX,Parameters.Play.Force);toy.Model.Kick(kick.X,kick.Y);phaseContact=true;lastToyKick=clock.Elapsed.TotalSeconds;}
                }
            }
            else
            {
                // The dog uses a forward play pose and grounded body contact.
                // Its illustrated silhouette has no procedural feline paw rig.
                teaserPawTarget=null;
                if(teaser is null&&s.Phase==BehaviorPhase.Contact&&!phaseContact&&contact is not null&&petGravity.Grounded&&
                    CanPlayToy(toy)&&!toy.Model.Held&&TouchingToy(toy))
                {
                    var kick=ToyContactPhysics.Push(toy.Model.X,Bounds(),approach.PushX,Parameters.Play.Force);
                    toy.Model.Kick(kick.X,kick.Y);phaseContact=true;GenericPlayContactCount++;lastToyKick=clock.Elapsed.TotalSeconds;
                }
            }
        }
        if(s.Phase==BehaviorPhase.Complete)EndSequence();
        return true;
    }
    private bool TickSleepSequence(BehaviorSequence s,double dt)
    {
        var exists=restSpot is null||restSpot.Kind is null||Furniture.Any(f=>f.Item==homeTarget)&&!EditingRoom;
        var reached=restSpot is null||Math.Abs(body.X+HalfWidth-restSpot.X)<3&&Math.Abs(body.Y+BodyHeight-restSpot.Feet)<4;
        var before=s.Phase;
        s.Step(dt,new(TargetExists:exists,Reached:reached,NavigationFailed:MovementPhase==NavigationPhase.Unreachable,Grounded:petGravity.Grounded,Rested:EmotionalState.Energy>=30&&EmotionalState.Fatigue<=75));
        if(before!=s.Phase&&s.Phase==BehaviorPhase.Sleep&&restSpot is {} used){restPreference.Used(used.Id);LearnHome(used.Kind is {} kind&&FurnitureCompatibility.CanUse(appearance,kind,FurnitureUse.Sleep)?FurnitureUse.Sleep:FurnitureUse.Rest);}
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
        if(s.Phase==BehaviorPhase.Approach&&restSpot is {} at)MovePetToward(at.X-HalfWidth,at.Feet-BodyHeight,dt,Bounds(),Parameters.Movement.Speed*.8);
        if(s.Phase==BehaviorPhase.Complete)EndSequence();return true;
    }
    private bool TickCareSequence(BehaviorSequence s,double dt)
    {
        var before=s.Phase;s.Step(dt,new(Grounded:petGravity.Grounded));
        if(s.Kind!=SequenceKind.SelfGroom&&cursor is {} at)attentionPoint=new(at.X,at.Y);
        if(before!=s.Phase&&s.Phase is BehaviorPhase.React or BehaviorPhase.Lean&&CharacterCapability.Allows(appearance,CharacterExpression.Purr)&&movementRandom.NextDouble()<s.Style.PurrChance)
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
            var sign=pointer.X>body.X+HalfWidth?1:-1;
            if(s.Phase==BehaviorPhase.Hesitate)body.MoveToward(body.X-sign*s.Style.Distance,body.Y,dt,Bounds(),s.Style.Distance);
            if(s.Phase==BehaviorPhase.React&&Bond>=65)body.MoveToward(body.X+sign*4,body.Y,dt,Bounds(),10);
        }
        if(s.Phase==BehaviorPhase.Complete)EndSequence();return true;
    }
}
