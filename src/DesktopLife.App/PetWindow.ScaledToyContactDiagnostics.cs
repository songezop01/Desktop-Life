using System.IO;
using System.Reflection;
using System.Text.Json;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    /// <summary>Real play approaches and contact impulses with a fixed-size toy on small house layouts.</summary>
    public void SmokeScaledToyContacts(string root)
    {
        if(worldOwner is not null)throw new InvalidOperationException("Scaled toy smoke requires the world owner.");
        Directory.CreateDirectory(root);
        var actors=new[]{this}.Concat(sharedCharacters).ToArray();
        var oldHouse=ownedHouse;var furniture=Furniture.ToArray();
        var visible=furniture.Select(f=>(Window:f,Visible:f.IsVisible)).ToArray();
        var restores=actors.Select(CaptureHouseSequenceActor).ToArray();
        var playState=actors.Select(a=>(Actor:a,Factory:a.ParameterFactory,Target:a.playTarget,Teaser:a.teaserTarget,Requested:a.requestedToy,Contact:a.phaseContact)).ToArray();
        var toys=AllToys.Select(t=>(Toy:t,X:t.Model.X,Y:t.Model.Y,VX:t.Model.VelocityX,VY:t.Model.VelocityY,Held:t.Model.Held,Bounds:t.RoomBounds,Platforms:t.Platforms)).ToArray();
        var owners=(Dictionary<string,PetAppearance>)typeof(HouseholdOccupancy).GetField("owners",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(Occupancy)!;
        var reservations=new Dictionary<string,PetAppearance>(owners);
        var evidence=new List<object>();
        try
        {
            foreach(var actor in actors){actor.SetSimulationEnabled(false);actor.SetPaused(false);actor.SetRoomEditing(false);actor.sequence=null;actor.CancelRoute();}
            foreach(var item in furniture)item.Hide();Furniture.Clear();platformCacheKey=int.MinValue;owners.Clear();
            foreach(var spec in new[]{(PetAppearance.BorderCollie,569d/1076,"spacedesk-three-floor"),(PetAppearance.Cat,.4,"compact-three-floor")})
                foreach(var fromRight in new[]{false,true})
                {
                    var actor=actors.Single(a=>a.appearance==spec.Item1);var bounds=Bounds();
                    ownedHouse=HouseLayout.Create(3,bounds with{Width=Math.Min(1371,bounds.Width),Height=1076*spec.Item2});
                    houseSurface?.Configure(ownedHouse);foreach(var resident in actors)resident.ApplyHouseGeometry();
                    var layout=ownedHouse;var floor=layout.Floors[0];var toyBounds=bounds with{Height=floor.Y-bounds.Top};
                    foreach(var toy in AllToys){toy.Model.Held=false;toy.Model.Place(bounds.Left+bounds.Width-60,floor.Y-40,toyBounds);toy.Model.Kick(0,0);}
                    var toyX=layout.WorkArea.Left+layout.WorkArea.Width*.5;
                    Ball.Model.Place(toyX,floor.Y-40,toyBounds);Ball.Model.Step(.016,toyBounds,layout.Platforms);
                    actor.sequence=null;actor.CancelRoute();actor.Occupancy.Release(actor.appearance);
                    actor.body.Place(toyX+(fromRight?130:-130),floor.Y-actor.BodyHeight,bounds);
                    actor.petGravity.PlaceSupported(actor.body.X,actor.body.Y,actor.BodyWidth,actor.BodyHeight,floor.Platform);
                    actor.ParameterFactory=null;actor.Parameters=BehaviorParameters.Default with{Movement=BehaviorParameters.Default.Movement with{Speed=50,PauseFrequency=0,DirectionChange=0,PathCurvature=0}};
                    actor.requestedToy=Ball;actor.playTarget=Ball;actor.teaserTarget=null;actor.phaseContact=false;
                    actor.SetAction(BodyAction.PlayToy);
                    var active=actor.sequence??throw new Exception("The scaled play sequence did not start.");
                    var initial=actor.Position;var failures=actor.NavigationFailures;var timeouts=actor.ApproachTimeouts;var generic=actor.GenericPlayContactCount;
                    var touched=false;var ticks=0;RoomPoint? contact=null;
                    for(;ticks<5000;ticks++)
                    {
                        Ball.Model.Step(.016,toyBounds,layout.Platforms);
                        actor.houseControlledThisFrame=false;actor.navigationStepped=false;
                        actor.TickSequence(.016);actor.RecordApproachTimeout(active);actor.ObserveNavigationProgress(.016);actor.StepPetGravity(.016);
                        actor.ApplyPose((ticks+1)*.016);
                        if(Math.Abs(Ball.Model.VelocityX)+Math.Abs(Ball.Model.VelocityY)>0)
                        {
                            if(active.Phase!=BehaviorPhase.Contact||!actor.phaseContact||!actor.petGravity.Grounded)
                                throw new Exception($"{actor.appearance} kicked a scaled toy before its grounded contact phase.");
                            contact=ToyContactPhysics.ReachableContactPoint(Ball.Model.X,Ball.Model.Y,actor.body.X,actor.body.Y,actor.BodyHeight,actor.appearance==PetAppearance.Cat?72:96);
                            if(contact is null)throw new Exception("The real toy impulse had no reachable circumference point.");
                            touched=true;break;
                        }
                        if(active.ApproachTimedOut||active.InterruptedBy is not null||actor.sequence is null)break;
                    }
                    if(!touched||Distance(initial,actor.Position)<20||actor.NavigationFailures!=failures||actor.ApproachTimeouts!=timeouts||
                        actor.appearance==PetAppearance.BorderCollie&&actor.GenericPlayContactCount-generic!=1)
                        throw new Exception($"{actor.appearance} did not physically reach and play with the scaled toy: scale={layout.SceneScale}, phase={active.Phase}, timeout={active.ApproachTimedOut}.");
                    var centreLocalY=(Ball.Model.Y+20-actor.body.Y)/(actor.BodyHeight/144);
                    evidence.Add(new{Character=actor.appearance.ToString(),Layout=spec.Item3,FromRight=fromRight,Scale=layout.SceneScale,
                        Ticks=ticks+1,Initial=initial,Position=actor.Position,Feet=actor.body.Y+actor.BodyHeight,Contact=contact,CentreLocalY=centreLocalY,
                        BallVelocity=new RoomPoint(Ball.Model.VelocityX,Ball.Model.VelocityY),GenericPlayContacts=actor.GenericPlayContactCount-generic,
                        ApproachTimeouts=actor.ApproachTimeouts-timeouts,NavigationFailures=actor.NavigationFailures-failures});
                    actor.sequence=null;actor.CancelRoute();actor.Occupancy.Release(actor.appearance);
                }
            Write(true,null);
        }
        catch(Exception ex){Write(false,ex.ToString());throw;}
        finally
        {
            foreach(var actor in actors){actor.sequence=null;actor.CancelRoute();}
            Furniture.Clear();Furniture.AddRange(furniture);ownedHouse=oldHouse;if(oldHouse is not null)houseSurface?.Configure(oldHouse);platformCacheKey=int.MinValue;
            foreach(var state in visible)if(state.Visible)state.Window.Show();else state.Window.Hide();
            foreach(var restore in restores)restore();
            foreach(var state in playState){state.Actor.ParameterFactory=state.Factory;state.Actor.playTarget=state.Target;state.Actor.teaserTarget=state.Teaser;state.Actor.requestedToy=state.Requested;state.Actor.phaseContact=state.Contact;}
            foreach(var state in toys){state.Toy.RoomBounds=state.Bounds;state.Toy.Platforms=state.Platforms;state.Toy.Model.Held=false;state.Toy.Model.Place(state.X,state.Y,Bounds());state.Toy.Model.Kick(state.VX,state.VY);state.Toy.Model.Held=state.Held;}
            owners.Clear();foreach(var state in reservations)owners.Add(state.Key,state.Value);
        }
        void Write(bool passed,string? error)=>File.WriteAllText(Path.Combine(root,"scaled-toy-contact-check.json"),JsonSerializer.Serialize(new{Passed=passed,Error=error,Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
    }
}
