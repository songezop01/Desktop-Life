using System.IO;
using System.Text.Json;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    public void SmokeLowToySupports(string root)
    {
        if(worldOwner is not null)throw new InvalidOperationException("Low-toy smoke requires the shared world owner.");
        Directory.CreateDirectory(root);
        var actors=new[]{this}.Concat(sharedCharacters).ToArray();
        var oldHouse=ownedHouse;var furniture=Furniture.ToArray();
        var restoreActors=actors.Select(CaptureHouseSequenceActor).ToArray();
        var factories=actors.Select(a=>(Actor:a,Factory:a.ParameterFactory)).ToArray();
        var toyStates=AllToys.Select(t=>(Toy:t,X:t.Model.X,Y:t.Model.Y,Vx:t.Model.VelocityX,Vy:t.Model.VelocityY,Held:t.Model.Held)).ToArray();
        var evidence=new List<object>();RoomWindow? fixture=null;
        try
        {
            foreach(var actor in actors){actor.SetSimulationEnabled(false);actor.sequence=null;actor.CancelRoute();actor.Occupancy.Release(actor.appearance);actor.SetPaused(false);}
            Furniture.Clear();platformCacheKey=int.MinValue;
            foreach(var compact in new[]{false,true})
            {
                ConfigureHouse(3,false);
                if(compact)
                {
                    var bounds=Bounds();ownedHouse=HouseLayout.Create(3,bounds with{Width=Math.Min(bounds.Width,560*.4)});
                    houseSurface?.Configure(ownedHouse);foreach(var actor in actors)actor.ApplyHouseGeometry();
                }
                var layout=House!;var floor=layout.Floors[0];
                fixture=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.Scratcher,floor.Left,floor.Y,0));
                fixture.SetSceneScale(layout.SceneScale);fixture.Relocate(floor.Left+(floor.Width-fixture.Width)*.6,floor.Y-fixture.Height);
                Furniture.Add(fixture);platformCacheKey=int.MinValue;
                var support=fixture.Platforms.Single();
                foreach(var kind in new[]{PetAppearance.Cat,PetAppearance.BorderCollie})
                {
                    var actor=actors.Single(a=>a.appearance==kind);actor.CancelRoute();actor.sequence=null;actor.Occupancy.Release(actor.appearance);
                    actor.ParameterFactory=_=>BehaviorParameters.Default with{Seed=70,Movement=BehaviorParameters.Default.Movement with{Speed=85},Play=new(110,.3)};
                    var ballCenter=support.X+support.Width*.5;
                    var start=layout.SafeFootX(0,ballCenter-100*layout.SceneScale,actor.HalfWidth);
                    actor.body.Place(start-actor.HalfWidth,floor.Y-actor.BodyHeight,Bounds());
                    actor.petGravity.PlaceSupported(actor.body.X,actor.body.Y,actor.BodyWidth,actor.BodyHeight,floor.Platform);
                    foreach(var toy in AllToys){toy.Model.Held=false;toy.Model.Place(floor.Left,floor.Y-40,ToyBounds);toy.Model.Kick(0,0);}
                    Ball.Model.Place(ballCenter-20,support.Y-40,ToyBounds);Ball.Platforms=RoomPlatforms();Ball.Step(.016);
                    actor.requestedToy=Ball;actor.SetAction(BodyAction.PlayToy);
                    var active=actor.sequence??throw new Exception("The low-platform play sequence did not start.");
                    var timeouts=actor.ApproachTimeouts;var failures=actor.NavigationFailures;var unreachable=actor.UnreachableTargets;
                    var touched=false;var frames=0;
                    for(;frames<2000;frames++)
                    {
                        Ball.Step(.016);actor.houseControlledThisFrame=false;actor.navigationStepped=false;
                        actor.TickSequence(.016);actor.RecordApproachTimeout(active);actor.ObserveNavigationProgress(.016);actor.StepPetGravity(.016);actor.ApplyPose(frames*.016);
                        if(active.ApproachTimedOut||actor.ApproachTimeouts!=timeouts||actor.NavigationFailures!=failures||actor.UnreachableTargets!=unreachable)
                            throw new Exception($"{kind} failed to approach a real low-platform ball at scale {layout.SceneScale}.");
                        if(actor.phaseContact)
                        {
                            if(active.Phase!=BehaviorPhase.Contact||Math.Abs(Ball.Model.VelocityX)<50||Ball.Model.VelocityY>=0||!actor.petGravity.Grounded)
                                throw new Exception("Low-platform contact did not produce its grounded physical kick.");
                            touched=true;break;
                        }
                        if(Math.Abs(Ball.Model.VelocityX)>.1)throw new Exception("The low-platform ball was kicked before the contact phase.");
                        if(actor.sequence is null||active.Finished)break;
                    }
                    if(!touched||Math.Abs(actor.body.Y+actor.BodyHeight-floor.Y)>.01)
                        throw new Exception($"{kind} did not contact the real Scratcher ball from its supported floor stance.");
                    evidence.Add(new{Character=kind.ToString(),Compact=compact,Scale=layout.SceneScale,SupportWidth=support.Width,
                        BallSupportFeet=support.Y,PetSupportFeet=actor.body.Y+actor.BodyHeight,FloorFeet=floor.Y,
                        Ticks=frames+1,ContactPhase=active.Phase.ToString(),BallVelocity=new RoomPoint(Ball.Model.VelocityX,Ball.Model.VelocityY),
                        ApproachTimeouts=actor.ApproachTimeouts-timeouts,NavigationFailures=actor.NavigationFailures-failures,
                        UnreachableTargets=actor.UnreachableTargets-unreachable});
                    actor.sequence=null;actor.CancelRoute();actor.Occupancy.Release(actor.appearance);
                }
                Furniture.Remove(fixture);fixture.Close();fixture=null;platformCacheKey=int.MinValue;
            }
            File.WriteAllText(Path.Combine(root,"low-toy-support-check.json"),JsonSerializer.Serialize(new{Passed=true,Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(root,"low-toy-support-check.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));throw;
        }
        finally
        {
            if(fixture is not null){Furniture.Remove(fixture);fixture.Close();}
            Furniture.Clear();Furniture.AddRange(furniture);ownedHouse=oldHouse;if(oldHouse is not null)houseSurface?.Configure(oldHouse);platformCacheKey=int.MinValue;
            foreach(var restore in restoreActors)restore();foreach(var saved in factories)saved.Actor.ParameterFactory=saved.Factory;
            foreach(var state in toyStates){state.Toy.Model.Held=false;state.Toy.Model.Place(state.X,state.Y,ToyBounds);state.Toy.Model.Kick(state.Vx,state.Vy);state.Toy.Model.Held=state.Held;}
        }
    }
}
