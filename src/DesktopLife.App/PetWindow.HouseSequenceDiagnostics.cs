using System.IO;
using System.Reflection;
using System.Text.Json;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    /// <summary>Runs real long approaches through the house into real furniture without manufacturing Reached or moving to the target.</summary>
    public void SmokeHouseSequences(string root)
    {
        if(worldOwner is not null)throw new InvalidOperationException("House sequence smoke requires the shared world owner.");
        Directory.CreateDirectory(root);
        var actors=new[]{this}.Concat(sharedCharacters).ToArray();
        if(!new[]{PetAppearance.Girl,PetAppearance.Cat,PetAppearance.BorderCollie}.All(kind=>actors.Any(a=>a.appearance==kind)))
            throw new InvalidOperationException("House sequence smoke requires all three residents.");
        var oldHouse=ownedHouse;
        var furniture=Furniture.Select(f=>(Window:f,Item:f.Item,Scale:f.SceneScale,Visible:f.IsVisible)).ToArray();
        var reservationField=typeof(HouseholdOccupancy).GetField("owners",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var reservations=(Dictionary<string,PetAppearance>)reservationField.GetValue(Occupancy)!;
        var oldReservations=new Dictionary<string,PetAppearance>(reservations);
        var restoreActors=actors.Select(CaptureHouseSequenceActor).ToArray();
        var evidence=new List<object>();
        RoomWindow? bed=null;
        try
        {
            foreach(var actor in actors)
            {
                actor.SetSimulationEnabled(false);actor.sequence=null;actor.CancelRoute();actor.SetPaused(false);actor.SetRoomEditing(false);
                actor.movementRandom=new Random(771+(int)actor.appearance);actor.AllowCursorAttraction=false;
                actor.Parameters=BehaviorParameters.Default with{Movement=BehaviorParameters.Default.Movement with{Speed=50,PauseFrequency=0,DirectionChange=0,PathCurvature=0}};
                actor.Parameters.Validate();actor.restPreference.Attach([]);actor.FurnitureUseCompleted=null;
            }
            reservations.Clear();foreach(var item in furniture)item.Window.Hide();Furniture.Clear();platformCacheKey=int.MinValue;
            ConfigureHouse(3,false);var house=House!;var top=house.Floors[2];
            bed=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.HumanBed,top.Left,top.Y,2));
            bed.SetSceneScale(house.SceneScale);bed.Relocate(top.Left+(top.Width-bed.Width)*.62,top.Y-bed.Height);
            Furniture.Add(bed);platformCacheKey=int.MinValue;
            if(bed.Platforms.Count==0)throw new Exception("The real bed has no physical support surface.");
            foreach(var actor in actors)
            {
                Run(actor,false);
                if(actor.appearance==PetAppearance.Girl)Run(actor,true);
            }
            File.WriteAllText(Path.Combine(root,"house-sequence-check.json"),JsonSerializer.Serialize(new{Passed=true,Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));

            void Run(PetWindow actor,bool activity)
            {
                actor.CancelRoute();actor.sequence=null;actor.Occupancy.Release(actor.appearance);actor.homeTarget=null;
                var start=house.SafeFootX(0,house.Floors[0].Left+house.Floors[0].Width*.22,actor.HalfWidth);
                actor.body.Place(start-actor.HalfWidth,house.Floors[0].Y-actor.BodyHeight,Bounds());
                actor.petGravity.PlaceSupported(actor.body.X,actor.body.Y,actor.BodyWidth,actor.BodyHeight,house.Floors[0].Platform);
                actor.body.Action=activity?BodyAction.Sit:BodyAction.Sleep;actor.poseAction=null;actor.houseControlledThisFrame=false;
                var failures=actor.NavigationFailures;var unreachable=actor.UnreachableTargets;var timeouts=actor.ApproachTimeouts;
                var houseFailures=actor.HouseRouteFailures;var trips=actor.StairTrips;var invalid=actor.InvalidFurnitureInteractions;
                var surface=bed.Platforms[0];
                if(activity)
                {
                    actor.requestedActivity=RoomActivity.Rest;
                    if(!actor.TryBeginRoomActivity(BodyAction.Sit))throw new Exception("The girl's real resting activity did not start.");
                }
                else actor.StartSleepAt(new(bed.Item.Id+":0",FurnitureKind.HumanBed,surface.X+surface.Width*.5,surface.Y));
                var active=actor.sequence??throw new Exception("Sleep/rest sequence was not created.");
                if(actor.homeTarget?.Id!=bed.Item.Id)throw new Exception("The sequence did not reserve the real top-floor bed.");
                var maxApproach=0d;var reached=false;var frames=0;var connectors=new HashSet<string>();
                for(;frames<40000;frames++)
                {
                    var previous=actor.Position;actor.houseControlledThisFrame=false;actor.navigationStepped=false;
                    actor.TickSequence(.016);actor.RecordApproachTimeout(active);
                    actor.ObserveNavigationProgress(.016);actor.StepPetGravity(.016);
                    maxApproach=Math.Max(maxApproach,active.Phase==BehaviorPhase.Approach?active.PhaseAge:0);
                    if(actor.houseConnector is {} connector)connectors.Add(connector);
                    if(!double.IsFinite(actor.body.X)||!double.IsFinite(actor.body.Y)||Distance(previous,actor.Position)>16)
                        throw new Exception($"{actor.appearance} teleported during a real house sequence.");
                    if(active.ApproachTimedOut||actor.ApproachTimeouts!=timeouts||actor.NavigationFailures!=failures||actor.UnreachableTargets!=unreachable||actor.HouseRouteFailures!=houseFailures||actor.InvalidFurnitureInteractions!=invalid)
                        throw new Exception($"{actor.appearance} failed its real {active.Kind} approach: timeout={active.ApproachTimedOut}, phase={active.Phase}, navigation={actor.MovementPhase}, position={actor.Position}.");
                    if(active.InterruptedBy is not null||active.TargetId is null||actor.sequence is null)
                        throw new Exception($"{actor.appearance} abandoned the requested top-floor bed.");
                    if(active.Phase==(activity?BehaviorPhase.Work:BehaviorPhase.Sleep)){reached=true;break;}
                }
                var feet=new RoomPoint(actor.body.X+actor.HalfWidth,actor.body.Y+actor.BodyHeight);
                if(!reached||maxApproach<=12||!actor.petGravity.Grounded||actor.StairTrips-trips!=2||connectors.Count!=2||
                    !bed.Platforms.Any(p=>feet.X>=p.X&&feet.X<=p.X+p.Width&&Math.Abs(p.HeightAt(feet.X)-feet.Y)<4))
                    throw new Exception($"{actor.appearance} did not complete a physical long approach: reached={reached}, approach={maxApproach:F2}s, stairs={actor.StairTrips-trips}, feet={feet}, phase={active.Phase}.");
                evidence.Add(new{Character=actor.appearance.ToString(),Kind=active.Kind.ToString(),FinalPhase=active.Phase.ToString(),
                    SimulatedSeconds=(frames+1)*.016,ApproachSeconds=maxApproach,active.ApproachBudgetSeconds,
                    StairTrips=actor.StairTrips-trips,Connectors=connectors,Feet=feet,Support=bed.Platforms.ToArray(),
                    ApproachTimeouts=actor.ApproachTimeouts-timeouts,UnreachableTargets=actor.UnreachableTargets-unreachable,
                    NavigationFailures=actor.NavigationFailures-failures,HouseRouteFailures=actor.HouseRouteFailures-houseFailures});
                actor.CancelRoute();actor.sequence=null;actor.Occupancy.Release(actor.appearance);
            }
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(root,"house-sequence-check.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
            throw;
        }
        finally
        {
            foreach(var actor in actors){actor.sequence=null;actor.CancelRoute();}
            Furniture.Clear();bed?.Close();Furniture.AddRange(furniture.Select(f=>f.Window));
            ownedHouse=oldHouse;
            if(oldHouse is not null)houseSurface?.Configure(oldHouse);
            foreach(var item in furniture)
            {item.Window.SetSceneScale(item.Scale);item.Window.SetFloor(item.Item.FloorIndex);item.Window.Relocate(item.Item.X,item.Item.Y);if(item.Visible)item.Window.Show();else item.Window.Hide();}
            platformCacheKey=int.MinValue;
            foreach(var restore in restoreActors)restore();
            reservations.Clear();foreach(var reservation in oldReservations)reservations.Add(reservation.Key,reservation.Value);
        }
    }

    private static Action CaptureHouseSequenceActor(PetWindow actor)
    {
        var position=actor.Position;var scale=actor.sceneScale;var action=actor.body.Action;var pose=actor.poseAction;
        var paused=actor.paused;var editing=actor.EditingRoom;var enabled=actor.simulationEnabled;var ticking=actor.timer.IsEnabled;var interval=actor.timer.Interval;
        var parameters=actor.Parameters;var attraction=actor.AllowCursorAttraction;var random=actor.movementRandom;var sequence=actor.sequence;
        var home=actor.homeTarget;var homeX=actor.homeX;var homeFeet=actor.homeFeet;var used=actor.homeUseRecorded;var rest=actor.restSpot;
        var activity=actor.roomActivity;var requestedActivity=actor.requestedActivity;var chair=actor.activityChair;var produced=actor.activityProduced;var template=actor.activityTemplate;
        var houseRoute=actor.houseRoute.ToArray();var houseGoal=actor.houseGoal;var connector=actor.houseConnector;var controlled=actor.houseControlledThisFrame;
        var route=actor.route.ToArray();var routeGoal=actor.routeGoal;var phase=actor.MovementPhase;var geometry=actor.roomGeometry;
        var elapsed=actor.navigationElapsed;var landing=actor.landingUntil;var launch=actor.navigationLaunchPoint;
        var recoveryX=actor.recoveryX;var recoveryAge=actor.recoveryAge;var recoveryReason=actor.recoveryReason;var stepped=actor.navigationStepped;
        var progressFields=typeof(NavigationProgress).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Select(f=>(Field:f,Value:f.GetValue(actor.navigationProgress))).ToArray();
        var target=actor.target;var queued=actor.queuedAction;var attention=actor.attentionPoint;var sequenceAge=actor.sequencePoseAge;
        var lastTarget=actor.lastTarget;var lastJump=actor.lastJump;
        var velocityX=actor.petGravity.VX;var velocityY=actor.petGravity.VY;var grounded=actor.petGravity.Grounded;
        var habits=actor.restPreference.Habits;var furnitureUsed=actor.FurnitureUseCompleted;
        var recentField=typeof(RestSpotPreference).GetField("recent",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var recent=(Dictionary<string,double>)recentField.GetValue(actor.restPreference)!;var oldRecent=new Dictionary<string,double>(recent);
        return () =>
        {
            actor.sceneScale=scale;actor.ApplyCharacterGeometry();actor.body.Place(position.X,position.Y,Bounds());
            actor.body.Action=action;actor.poseAction=pose;actor.Parameters=parameters;actor.AllowCursorAttraction=attraction;actor.movementRandom=random;
            actor.sequence=sequence;actor.homeTarget=home;actor.homeX=homeX;actor.homeFeet=homeFeet;actor.homeUseRecorded=used;actor.restSpot=rest;
            actor.roomActivity=activity;actor.requestedActivity=requestedActivity;actor.activityChair=chair;actor.activityProduced=produced;actor.activityTemplate=template;
            actor.houseRoute.Clear();foreach(var step in houseRoute)actor.houseRoute.Enqueue(step);actor.houseGoal=houseGoal;actor.houseConnector=connector;actor.houseControlledThisFrame=controlled;
            actor.route.Clear();foreach(var step in route)actor.route.Enqueue(step);actor.routeGoal=routeGoal;actor.MovementPhase=phase;actor.roomGeometry=geometry;
            actor.navigationElapsed=elapsed;actor.landingUntil=landing;actor.navigationLaunchPoint=launch;
            actor.recoveryX=recoveryX;actor.recoveryAge=recoveryAge;actor.recoveryReason=recoveryReason;actor.navigationStepped=stepped;
            foreach(var field in progressFields)field.Field.SetValue(actor.navigationProgress,field.Value);
            actor.target=target;actor.queuedAction=queued;actor.attentionPoint=attention;actor.sequencePoseAge=sequenceAge;
            actor.lastTarget=lastTarget;actor.lastJump=lastJump;
            actor.petGravity.X=position.X;actor.petGravity.Y=position.Y;actor.petGravity.VX=velocityX;actor.petGravity.VY=velocityY;
            actor.petGravity.Step(0,Bounds(),actor.BodyWidth,actor.BodyHeight,0,actor.RoomPlatforms());
            if(grounded)
            {
                var feet=position.Y+actor.BodyHeight;var x=position.X+actor.HalfWidth;
                var support=actor.RoomPlatforms().Concat(actor.House?.Stairs.Select(s=>s.Support)??[]).FirstOrDefault(p=>x>=p.X&&x<=p.X+p.Width&&Math.Abs(p.HeightAt(x)-feet)<1);
                if(support.Width>0)actor.petGravity.PlaceSupported(position.X,position.Y,actor.BodyWidth,actor.BodyHeight,support);
            }
            actor.petGravity.VX=velocityX;actor.petGravity.VY=velocityY;
            actor.restPreference.Attach(habits);recent.Clear();foreach(var pair in oldRecent)recent.Add(pair.Key,pair.Value);actor.FurnitureUseCompleted=furnitureUsed;
            actor.EditingRoom=editing;foreach(var item in actor.Furniture)item.SetEditing(editing);
            actor.SetPaused(paused);actor.SetSimulationEnabled(enabled);actor.timer.Interval=interval;if(!ticking)actor.timer.Stop();
            actor.ApplyPosition();actor.ApplyPose();
        };
    }
}
