using System.IO;
using System.Reflection;
using System.Text.Json;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    /// <summary>Moves a real toy during production house pursuit, retaining real stair support and contact.</summary>
    public void SmokeHouseMovingGoals(string root)
    {
        if(worldOwner is not null)throw new InvalidOperationException("Moving-goal smoke requires the shared world owner.");
        Directory.CreateDirectory(root);
        var actors=new[]{this}.Concat(sharedCharacters).ToArray();
        if(!Enum.GetValues<PetAppearance>().All(kind=>actors.Any(actor=>actor.appearance==kind)))
            throw new InvalidOperationException("Moving-goal smoke requires all three residents.");
        var originalDisplay=DisplayWorkspace.Active;var originalHouse=ownedHouse;
        var furniture=Furniture.ToArray();var restoreActors=actors.Select(CaptureHouseSequenceActor).ToArray();
        var playStates=actors.Select(actor=>(Actor:actor,Target:actor.playTarget,Requested:actor.requestedToy,Teaser:actor.teaserTarget,Contact:actor.phaseContact)).ToArray();
        var toyStates=AllToys.Select(toy=>(Toy:toy,X:toy.Model.X,Y:toy.Model.Y,VX:toy.Model.VelocityX,VY:toy.Model.VelocityY,
            Held:toy.Model.Held,Platforms:toy.Platforms,RoomBounds:toy.RoomBounds)).ToArray();
        var reservationField=typeof(HouseholdOccupancy).GetField("owners",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var reservations=(Dictionary<string,PetAppearance>)reservationField.GetValue(Occupancy)!;
        var originalReservations=new Dictionary<string,PetAppearance>(reservations);var evidence=new List<object>();
        var completionHandlers=new List<(PetWindow Actor,Action<CompletedBehavior> Handler)>();
        try
        {
            foreach(var actor in actors)
            {
                actor.SetSimulationEnabled(false);actor.SetPaused(false);actor.SetRoomEditing(false);
                actor.sequence=null;actor.CancelRoute();actor.AllowCursorAttraction=false;
                actor.Parameters=BehaviorParameters.Default with{Seed=70,Movement=BehaviorParameters.Default.Movement with{Speed=50}};
            }
            reservations.Clear();Furniture.Clear();platformCacheKey=int.MinValue;
            foreach(var toy in AllToys)toy.Model.Held=toy!=Ball;
            foreach(var display in DisplayWorkspace.Enumerate())
            {
                DisplayWorkspace.Select(display.Id);ConfigureHouse(3,false);
                // The girl's real PlayToy intent starts a room activity. Only the
                // cat and dog run physical toy-pursuit sequences in the product.
                foreach(var actor in actors.Where(candidate=>candidate.appearance!=PetAppearance.Girl))
                {
                    foreach(var changeAt in new[]{HouseTravelKind.FloorWalk,HouseTravelKind.Stair})
                        Run(actor,display,changeAt,0);
                    RunStopping(actor,display,false);RunStopping(actor,display,true);
                }
            }
            File.WriteAllText(Path.Combine(root,"house-moving-goal-check.json"),JsonSerializer.Serialize(new{Passed=true,Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(root,"house-moving-goal-check.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
            throw;
        }
        finally
        {
            foreach(var item in completionHandlers)item.Actor.BehaviorCompleted-=item.Handler;
            foreach(var actor in actors){actor.sequence=null;actor.CancelRoute();actor.Occupancy.Release(actor.appearance);}
            DisplayWorkspace.Select(originalDisplay.Id);ownedHouse=originalHouse;
            if(originalHouse is not null)houseSurface?.Configure(originalHouse);
            Furniture.Clear();Furniture.AddRange(furniture);platformCacheKey=int.MinValue;
            foreach(var restore in restoreActors)restore();
            foreach(var state in playStates){state.Actor.playTarget=state.Target;state.Actor.requestedToy=state.Requested;state.Actor.teaserTarget=state.Teaser;state.Actor.phaseContact=state.Contact;}
            foreach(var state in toyStates)
            {
                state.Toy.Model.Held=false;state.Toy.Model.Place(state.X,state.Y,Bounds());state.Toy.Model.Kick(state.VX,state.VY);
                state.Toy.Model.Held=state.Held;state.Toy.Platforms=state.Platforms;state.Toy.RoomBounds=state.RoomBounds;
            }
            reservations.Clear();foreach(var item in originalReservations)reservations.Add(item.Key,item.Value);
        }

        void Run(PetWindow actor,RoomDisplay display,HouseTravelKind changeAt,int destinationFloor)
        {
            var layout=House!;var ground=layout.Floors[0];var top=layout.Floors[2];
            actor.sequence=null;actor.CancelRoute();actor.Occupancy.Release(actor.appearance);actor.homeTarget=null;
            actor.toyInterest.Step(30);
            actor.body.Place(ground.Left+ground.Width*.22,ground.Y-actor.BodyHeight,Bounds());
            actor.petGravity.PlaceSupported(actor.body.X,actor.body.Y,actor.BodyWidth,actor.BodyHeight,ground.Platform);
            actor.body.Action=BodyAction.PlayToy;actor.poseAction=null;actor.houseControlledThisFrame=false;
            Ball.Model.Held=false;Ball.Model.Place(top.Left+top.Width*.3,top.Y-InteractiveToy.Size,Bounds());
            Ball.Platforms=layout.Platforms;Ball.Step(0);actor.requestedToy=Ball;actor.BeginSequence(BodyAction.PlayToy);
            var active=actor.sequence??throw new Exception("The real moving-toy sequence did not begin.");
            var failures=actor.NavigationFailures;var houseFailures=actor.HouseRouteFailures;
            var unreachable=actor.UnreachableTargets;var timeouts=actor.ApproachTimeouts;var trips=actor.StairTrips;
            var changed=false;var changeTime=0d;var maxStep=0d;var highestFloorAfterChange=0;var contacted=false;var frames=0;
            for(;frames<40000;frames++)
            {
                if(!changed&&actor.houseRoute.TryPeek(out var step)&&step.Kind==changeAt&&
                    (changeAt!=HouseTravelKind.Stair||Math.Abs(actor.body.Y+actor.BodyHeight-step.From.Y)>40*layout.SceneScale))
                {
                    // Moving only the toy reproduces a user moving it between floors.
                    // The character keeps its actual position, support and phase.
                    var target=layout.Floors[destinationFloor];
                    Ball.Model.Place(target.Left+target.Width*.46,target.Y-InteractiveToy.Size,Bounds());Ball.Step(0);
                    changed=true;changeTime=frames*.016;trips=actor.StairTrips;
                }
                var before=actor.Position;actor.houseControlledThisFrame=false;actor.navigationStepped=false;
                Ball.Platforms=layout.Platforms;Ball.Step(.016);
                actor.TickSequence(.016);actor.RecordApproachTimeout(active);
                actor.ObserveNavigationProgress(.016);actor.StepPetGravity(.016);
                actor.ApplyPose((frames+1)*.016);
                maxStep=Math.Max(maxStep,Distance(before,actor.Position));
                var feet=new RoomPoint(actor.body.X+actor.HalfWidth,actor.body.Y+actor.BodyHeight);
                if(changed&&layout.FindFloor(feet.X,feet.Y,layout.FloorContactTolerance) is {} reachedFloor)
                    highestFloorAfterChange=Math.Max(highestFloorAfterChange,reachedFloor);
                if(!actor.petGravity.Grounded||!layout.Platforms.Concat(layout.Stairs.Select(stair=>stair.Support)).Any(
                    platform=>feet.X>=platform.X-.01&&feet.X<=platform.X+platform.Width+.01&&Math.Abs(platform.HeightAt(feet.X)-feet.Y)<.02))
                    throw new Exception($"{actor.appearance} lost real support while redirecting to a moved toy on {display.Label}.");
                if(maxStep>.801||active.ApproachTimedOut||actor.ApproachTimeouts!=timeouts||actor.NavigationFailures!=failures||
                    actor.HouseRouteFailures!=houseFailures||actor.UnreachableTargets!=unreachable)
                    throw new Exception($"{actor.appearance} failed a moving-toy approach on {display.Label}: timeout={active.ApproachTimedOut}, step={maxStep}, phase={active.Phase}.");
                if(active.Contacts>0){contacted=true;break;}
                if(active.Finished||actor.sequence is null)break;
            }
            var expectedTrips=changeAt==HouseTravelKind.Stair?2:0;
            if(!changed||!contacted||highestFloorAfterChange>(changeAt==HouseTravelKind.Stair?1:0)||actor.StairTrips-trips!=expectedTrips)
                throw new Exception($"{actor.appearance} retained an obsolete destination or failed real toy contact: changed={changed}, contact={contacted}, highestFloor={highestFloorAfterChange}, stairs={actor.StairTrips-trips}.");
            var other=actors.First(candidate=>candidate!=actor).appearance;
            if(actor.houseConnector is not null||layout.Stairs.Any(stair=>!Occupancy.Available(stair.ReservationKey,other)))
                throw new Exception("A completed moving-toy route retained a stair reservation.");
            evidence.Add(new{Display=display.Label,layout.SceneScale,Character=actor.appearance.ToString(),ChangedDuring=changeAt.ToString(),
                NewToyFloor=destinationFloor,ChangeSeconds=changeTime,SimulatedSeconds=(frames+1)*.016,Contact=contacted,
                StairTripsAfterChange=actor.StairTrips-trips,HighestFloorAfterChange=highestFloorAfterChange,MaxFrameTranslation=maxStep,
                active.ApproachBudgetSeconds,active.PlayTargetRevisions,active.PlayApproachStopped,
                ApproachTimeouts=actor.ApproachTimeouts-timeouts,NavigationFailures=actor.NavigationFailures-failures,
                HouseRouteFailures=actor.HouseRouteFailures-houseFailures,UnreachableTargets=actor.UnreachableTargets-unreachable,ReleasedStairReservations=true});
            actor.sequence=null;actor.CancelRoute();actor.Occupancy.Release(actor.appearance);
        }

        void RunStopping(PetWindow actor,RoomDisplay display,bool heldByUser)
        {
            var layout=House!;var ground=layout.Floors[0];var top=layout.Floors[2];
            actor.sequence=null;actor.CancelRoute();actor.Occupancy.Release(actor.appearance);actor.homeTarget=null;
            actor.toyInterest.Step(30);
            actor.body.Place(ground.Left+ground.Width*.22,ground.Y-actor.BodyHeight,Bounds());
            actor.petGravity.PlaceSupported(actor.body.X,actor.body.Y,actor.BodyWidth,actor.BodyHeight,ground.Platform);
            actor.body.Action=BodyAction.PlayToy;actor.poseAction=null;actor.houseControlledThisFrame=false;
            Ball.Model.Held=false;Ball.Model.Place(top.Left+top.Width*.3,top.Y-InteractiveToy.Size,Bounds());
            Ball.Platforms=layout.Platforms;Ball.Step(0);actor.requestedToy=Ball;actor.BeginSequence(BodyAction.PlayToy);
            var active=actor.sequence??throw new Exception("The real stopping-toy sequence did not begin.");
            CompletedBehavior? completed=null;Action<CompletedBehavior> handler=value=>completed=value;
            actor.BehaviorCompleted+=handler;completionHandlers.Add((actor,handler));
            var failures=actor.NavigationFailures;var houseFailures=actor.HouseRouteFailures;
            var unreachable=actor.UnreachableTargets;var timeouts=actor.ApproachTimeouts;var trips=actor.StairTrips;
            var moved=0;var firstChange=-1d;var lastChange=-1d;var stoppedOnStair=false;
            var highestFloor=0;var maxStep=0d;var frames=0;
            for(;frames<40000;frames++)
            {
                var now=frames*.016;
                var entered=actor.houseRoute.TryPeek(out var current)&&current.Kind==HouseTravelKind.Stair&&
                    Math.Abs(actor.body.Y+actor.BodyHeight-current.From.Y)>40*layout.SceneScale;
                if((moved==0&&entered)||(!heldByUser&&moved is >0 and <5&&now-lastChange>=1.1))
                {
                    if(firstChange<0)firstChange=now;
                    lastChange=now;moved++;
                    if(heldByUser)Ball.Model.Held=true;
                    else{Ball.Model.Place(top.Left+top.Width*(moved%2==1?.55:.3),top.Y-InteractiveToy.Size,Bounds());Ball.Step(0);}
                }
                var before=actor.Position;actor.houseControlledThisFrame=false;actor.navigationStepped=false;
                Ball.Platforms=layout.Platforms;Ball.Step(.016);
                actor.TickSequence(.016);actor.RecordApproachTimeout(active);
                actor.ObserveNavigationProgress(.016);actor.StepPetGravity(.016);
                actor.ApplyPose((frames+1)*.016);
                maxStep=Math.Max(maxStep,Distance(before,actor.Position));
                var feet=new RoomPoint(actor.body.X+actor.HalfWidth,actor.body.Y+actor.BodyHeight);
                if(layout.FindFloor(feet.X,feet.Y,layout.FloorContactTolerance) is {} floor)highestFloor=Math.Max(highestFloor,floor);
                if(active.Phase is BehaviorPhase.WatchBall or BehaviorPhase.Recover&&
                    actor.houseRoute.TryPeek(out var finishing)&&actor.EnteredHouseStair(finishing))
                {
                    stoppedOnStair=true;
                    if(active.PhaseAge>.000001)throw new Exception("Stopped-pursuit choreography advanced before the real stair endpoint.");
                }
                if(!actor.petGravity.Grounded||!layout.Platforms.Concat(layout.Stairs.Select(stair=>stair.Support)).Any(
                    platform=>feet.X>=platform.X-.01&&feet.X<=platform.X+platform.Width+.01&&Math.Abs(platform.HeightAt(feet.X)-feet.Y)<.02))
                    throw new Exception($"{actor.appearance} lost support when a toy pursuit stopped on stairs.");
                if(maxStep>.801||active.ApproachTimedOut||actor.ApproachTimeouts!=timeouts||actor.NavigationFailures!=failures||
                    actor.HouseRouteFailures!=houseFailures||actor.UnreachableTargets!=unreachable||active.Contacts!=0)
                    throw new Exception($"{actor.appearance} failed safe toy-pursuit stopping: held={heldByUser}, timeout={active.ApproachTimedOut}, contacts={active.Contacts}.");
                if(active.Finished&&actor.sequence is null)break;
            }
            if(firstChange<0||!stoppedOnStair||!active.Finished||completed is not {Successful:false}||highestFloor!=1||actor.StairTrips-trips!=1||
                actor.houseRoute.Count!=0||actor.houseConnector is not null||layout.FindFloor(actor.body.X+actor.HalfWidth,actor.body.Y+actor.BodyHeight,layout.FloorContactTolerance)!=1||
                (!heldByUser&&active.PlayApproachStopped!=PlayApproachStoppedReason.EscapingTarget))
                throw new Exception($"{actor.appearance} did not stop an unsuccessful pursuit on the real next-floor endpoint: held={heldByUser}, stoppedOnStair={stoppedOnStair}, completed={completed}, floor={highestFloor}.");
            var other=actors.First(candidate=>candidate!=actor).appearance;
            if(layout.Stairs.Any(stair=>!Occupancy.Available(stair.ReservationKey,other)))
                throw new Exception("A stopped toy pursuit retained a stair reservation.");
            evidence.Add(new{Display=display.Label,layout.SceneScale,Character=actor.appearance.ToString(),
                Scenario=heldByUser?"toy-held-mid-stair":"toy-repeatedly-moved-mid-stair",Changes=moved,FirstChangeSeconds=firstChange,
                SimulatedSeconds=(frames+1)*.016,StoppedOnStair=stoppedOnStair,EndedOnFloor=1,Contacts=active.Contacts,
                Successful=completed.Successful,StairTripsAfterStop=actor.StairTrips-trips,MaxFrameTranslation=maxStep,
                active.PlayTargetRevisions,active.PlayApproachStopped,InterruptedBy=active.InterruptedBy?.ToString(),
                ApproachTimeouts=actor.ApproachTimeouts-timeouts,ReleasedStairReservations=true});
            actor.BehaviorCompleted-=handler;completionHandlers.Remove((actor,handler));
            Ball.Model.Held=false;actor.sequence=null;actor.CancelRoute();actor.Occupancy.Release(actor.appearance);
        }
    }
}
