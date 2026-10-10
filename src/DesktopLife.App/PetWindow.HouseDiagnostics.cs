using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    public void SmokeHouseToyFloor(string root)
    {
        var floors=House?.FloorCount??1;var bounds=Bounds();
        var saved=AllToys.Select(t=>(Toy:t,Position:new RoomPoint(t.Model.X,t.Model.Y),Velocity:new RoomPoint(t.Model.VelocityX,t.Model.VelocityY),Held:t.Model.Held)).ToArray();
        var evidence=new List<object>();
        try
        {
            foreach(var count in Enumerable.Range(1,3))
            {
                ConfigureHouse(count,false);
                foreach(var toy in AllToys)
                {
                    // A pre-house save or a drop below the new floor must use the real toy step and bounds.
                    toy.Model.Held=false;toy.Model.Place(bounds.Left+bounds.Width*.5,bounds.Top+bounds.Height-40,bounds);toy.Model.Kick(0,0);
                    toy.Step(.016);
                    if(Math.Abs(toy.Model.Y+40-NavigationFloor)>.001)
                        throw new Exception($"Legacy toy stayed below the {count}-floor house: {toy.Model.Y+40} vs {NavigationFloor}.");
                    evidence.Add(new{Floors=count,Toy=toy.Title,Feet=toy.Model.Y+40,Floor=NavigationFloor});
                }
            }
            File.WriteAllText(Path.Combine(root,"house-toy-floor-check.json"),JsonSerializer.Serialize(new{Passed=true,Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally
        {
            ConfigureHouse(floors,false);
            foreach(var state in saved){state.Toy.Model.Held=false;state.Toy.Model.Place(state.Position.X,state.Position.Y,ToyBounds);state.Toy.Model.Kick(state.Velocity.X,state.Velocity.Y);state.Toy.Model.Held=state.Held;}
        }
    }
    /// <summary>Exercises the production controller and gravity in an isolated smoke-test world.</summary>
    public void SmokeHouseTravel(string root)
    {
        ValidateHouseFoodDiagnosticProfile(root);
        if(worldOwner is not null)throw new InvalidOperationException("House smoke must run on the shared world owner.");
        Directory.CreateDirectory(root);
        var residents=new[]{this}.Concat(sharedCharacters).ToArray();
        if(!new[]{PetAppearance.Girl,PetAppearance.Cat,PetAppearance.BorderCollie}.All(kind=>residents.Any(p=>p.appearance==kind)))
            throw new InvalidOperationException("House smoke requires all three character kinds in the shared world.");
        var originalFloors=House?.FloorCount??1;
        var furniture=Furniture.Select(f=>(Window:f,Item:f.Item,Visible:f.IsVisible)).ToArray();
        var states=residents.Select(p=>(Pet:p,Position:p.Position,Paused:p.paused,Enabled:p.simulationEnabled,
            Timer:p.timer.IsEnabled,Visible:p.IsVisible,Action:p.body.Action,Pose:p.poseAction)).ToArray();
        var toyStates=AllToys.Select(t=>(Toy:t,X:t.Model.X,Y:t.Model.Y)).ToArray();
        var evidence=new List<object>();
        try
        {
            foreach(var actor in residents){actor.SetSimulationEnabled(false);actor.SetPaused(false);actor.SetRoomEditing(false);actor.ShowActivated=false;actor.Show();}
            foreach(var item in furniture)item.Window.Hide();
            Furniture.Clear();platformCacheKey=int.MinValue;
            for(var floors=1;floors<=HouseLayout.MaximumFloorCount;floors++)
            {
                ConfigureHouse(floors,false);
                var layout=House!;
                foreach(var actor in residents)
                    foreach(var source in layout.Floors)
                        foreach(var destination in layout.Floors)
                        {
                            var startX=layout.SafeFootX(source.Index,source.Left+source.Width*.4,actor.HalfWidth);
                            var goalX=layout.SafeFootX(destination.Index,destination.Left+destination.Width*.6,actor.HalfWidth);
                            Place(actor,source.Index,startX);
                            var trips=actor.StairTrips;var failures=actor.HouseRouteFailures;
                            var ticks=Travel(actor,destination.Index,goalX);
                            if(actor.HouseRouteFailures!=failures)throw new Exception($"{actor.appearance} reported a house route failure in {floors} floors: {source.Index}->{destination.Index}.");
                            if(actor.StairTrips-trips!=Math.Abs(source.Index-destination.Index))
                                throw new Exception($"{actor.appearance} skipped a connector: {source.Index}->{destination.Index}.");
                            evidence.Add(new{Floors=floors,Character=actor.appearance.ToString(),From=source.Index,To=destination.Index,
                                Ticks=ticks,Trips=actor.StairTrips-trips,Feet=new RoomPoint(actor.body.X+actor.HalfWidth,actor.body.Y+actor.BodyHeight),Scale=layout.SceneScale});
                        }
                RenderHouseCharacterComparison(Path.Combine(root,$"house-{floors}-character-proportions.png"),residents);
            }

            ConfigureHouse(3,false);
            foreach(var actor in residents)
            {
                var layout=House!;Place(actor,0,layout.Stairs[0].LowerLanding.X);
                var goal=layout.SafeFootX(2,layout.Floors[2].Left+layout.Floors[2].Width*.55,actor.HalfWidth);
                for(var tick=0;tick<8000&&!actor.IsOnStair;tick++)Frame(actor,2,goal);
                // Advance beyond the entrance so this verifies support halfway through a real connector.
                for(var tick=0;tick<30;tick++)Frame(actor,2,goal);
                if(!actor.IsOnStair)throw new Exception($"{actor.appearance} did not enter a stair for lifecycle verification.");
                var before=actor.Position;var pending=actor.houseRoute.Count;var connector=actor.houseConnector;
                actor.SetPaused(true);
                for(var tick=0;tick<12;tick++){actor.previous=actor.clock.Elapsed.TotalSeconds-.016;actor.Tick(null,EventArgs.Empty);}
                if(Distance(before,actor.Position)>.001||actor.houseRoute.Count!=pending||!actor.petGravity.Grounded)
                    throw new Exception($"{actor.appearance} moved or lost stair support while paused.");
                actor.SetPaused(false);actor.SuspendPresence();
                for(var tick=0;tick<12;tick++){actor.previous=actor.clock.Elapsed.TotalSeconds-.016;actor.Tick(null,EventArgs.Empty);}
                if(Distance(before,actor.Position)>.001||actor.houseRoute.Count!=pending)
                    throw new Exception($"{actor.appearance} discarded or advanced its hidden stair route.");
                var another=residents.First(p=>p!=actor);
                if(connector is not null&&!Occupancy.Available(connector,another.appearance))
                    throw new Exception("A hidden character retained a stair reservation.");
                actor.Show();Travel(actor,2,goal);
                evidence.Add(new{Character=actor.appearance.ToString(),Lifecycle="paused-hidden-resumed",Passed=true});

                // Match the production drag movement callback: cancel the route, relocate, then let gravity settle.
                Place(actor,0,layout.Stairs[0].LowerLanding.X);
                for(var tick=0;tick<35;tick++)Frame(actor,2,goal);
                actor.CancelRoute();actor.ClearHouseRoute();
                var dropX=layout.SafeFootX(0,layout.Floors[0].Left+layout.Floors[0].Width*.5,actor.HalfWidth);
                actor.body.Place(dropX-actor.HalfWidth,layout.Floors[0].Y-actor.BodyHeight-30,Bounds());
                actor.petGravity.VX=actor.petGravity.VY=0;
                for(var tick=0;tick<300&&!actor.petGravity.Grounded;tick++){actor.houseControlledThisFrame=false;actor.StepPetGravity(.016);}
                // Place changed the body graph but Grounded can still reflect the previous contact until the first real step.
                actor.houseControlledThisFrame=false;actor.StepPetGravity(.016);
                for(var tick=0;tick<300&&!actor.petGravity.Grounded;tick++)actor.StepPetGravity(.016);
                if(!actor.petGravity.Grounded||Math.Abs(actor.body.Y+actor.BodyHeight-layout.Floors[0].Y)>.01||actor.houseRoute.Count!=0)
                    throw new Exception($"{actor.appearance} failed to settle after drag cancelled a stair route.");
                evidence.Add(new{Character=actor.appearance.ToString(),Lifecycle="drag-cancel-ground-recovery",Passed=true});
            }
            SmokeHouseLowBoxExit(residents,evidence);
            SmokeHouseActivitySupports(residents,evidence);
            File.WriteAllText(Path.Combine(root,"house-travel-check.json"),JsonSerializer.Serialize(new{Passed=true,Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(root,"house-travel-check.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
            throw;
        }
        finally
        {
            foreach(var actor in residents){actor.CancelRoute();actor.Occupancy.Release(actor.appearance);}
            Furniture.AddRange(furniture.Select(f=>f.Window));
            ConfigureHouse(originalFloors,false);
            foreach(var item in furniture){item.Window.SetFloor(item.Item.FloorIndex);item.Window.Relocate(item.Item.X,item.Item.Y);if(item.Visible)item.Window.Show();else item.Window.Hide();}
            platformCacheKey=int.MinValue;
            foreach(var state in toyStates)state.Toy.Model.Place(state.X,state.Y,Bounds());
            foreach(var state in states)
            {
                state.Pet.body.Place(state.Position.X,state.Position.Y,Bounds());state.Pet.body.Action=state.Action;state.Pet.poseAction=state.Pose;
                state.Pet.SetPaused(state.Paused);if(state.Visible)state.Pet.Show();else state.Pet.Hide();
                state.Pet.SetSimulationEnabled(state.Enabled);if(!state.Timer)state.Pet.timer.Stop();state.Pet.ApplyPosition();
            }
        }

        void Place(PetWindow actor,int floorIndex,double footX)
        {
            actor.CancelRoute();actor.Occupancy.Release(actor.appearance);actor.sequence=null;
            actor.body.Place(footX-actor.HalfWidth,House!.Floors[floorIndex].Y-actor.BodyHeight,Bounds());
            actor.petGravity.PlaceSupported(actor.body.X,actor.body.Y,actor.BodyWidth,actor.BodyHeight,House.Floors[floorIndex].Platform);
            actor.body.Action=BodyAction.Walk;actor.poseAction=null;actor.houseControlledThisFrame=false;
        }
        void Frame(PetWindow actor,int floorIndex,double goalX)
        {
            var previousPoint=actor.Position;actor.houseControlledThisFrame=false;actor.navigationStepped=false;
            actor.Navigate(goalX-actor.HalfWidth,House!.Floors[floorIndex].Y-actor.BodyHeight,.016,Bounds(),120);
            actor.StepPetGravity(.016);
            if(!double.IsFinite(actor.body.X)||!double.IsFinite(actor.body.Y)||Distance(previousPoint,actor.Position)>1.921)
                throw new Exception($"{actor.appearance} made an unbounded movement during house travel.");
            var feet=new RoomPoint(actor.body.X+actor.HalfWidth,actor.body.Y+actor.BodyHeight);
            var supported=House.Platforms.Concat(House.Stairs.Select(s=>s.Support)).Any(p=>feet.X>=p.X-.01&&feet.X<=p.X+p.Width+.01&&Math.Abs(p.HeightAt(feet.X)-feet.Y)<.02);
            if(!actor.petGravity.Grounded||!supported)throw new Exception($"{actor.appearance} lost actual floor/stair contact: {feet}, phase={actor.MovementPhase}.");
        }
        int Travel(PetWindow actor,int floorIndex,double goalX)
        {
            for(var tick=0;tick<8000;tick++)
            {
                Frame(actor,floorIndex,goalX);
                if(Math.Abs(actor.body.X+actor.HalfWidth-goalX)<.05&&Math.Abs(actor.body.Y+actor.BodyHeight-House!.Floors[floorIndex].Y)<.01&&!actor.FinishingMotion)return tick+1;
            }
            throw new Exception($"{actor.appearance} did not reach floor {floorIndex}: position={actor.Position}, phase={actor.MovementPhase}, houseSteps={actor.houseRoute.Count}.");
        }
    }

    private void SmokeHouseLowBoxExit(PetWindow[] residents,List<object> evidence)
    {
        var bounds=Bounds();RoomWindow? box=null;
        try
        {
            box=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.Box,bounds.Left,bounds.Top));
            Furniture.Add(box);
            foreach(var compact in new[]{false,true})
            {
                ConfigureHouse(3,false);
                if(compact)
                {
                    // A narrow phone-sized logical viewport keeps the real WPF furniture and controller,
                    // while making the Box-to-floor separation smaller than one DIP.
                    ownedHouse=HouseLayout.Create(3,bounds with{Width=Math.Min(bounds.Width,HouseLayout.MinimumDesignWidth*.4)});
                    houseSurface?.Configure(ownedHouse);
                    foreach(var resident in residents)resident.ApplyHouseGeometry();
                }
                var layout=House!;box.SetSceneScale(layout.SceneScale);
                foreach(var sourceIndex in new[]{0,2})
                {
                    var source=layout.Floors[sourceIndex];var destination=layout.Floors[2-sourceIndex];
                    box.SetFloor(sourceIndex);box.Relocate(source.Left+(source.Width-box.Width)*.43,source.Y-box.Height);
                    platformCacheKey=int.MinValue;
                    var support=box.Platforms.Single();var center=support.X+support.Width*.5;
                    var gap=source.Y-support.HeightAt(center);
                    if(gap<=layout.FloorContactTolerance||gap>=4||compact&&gap>=1)
                        throw new Exception($"The real Box did not exercise the near-floor regression: gap={gap}, scale={layout.SceneScale}.");
                    foreach(var actor in residents)
                    {
                        actor.CancelRoute();actor.Occupancy.Release(actor.appearance);actor.sequence=null;
                        actor.body.Place(center-actor.HalfWidth,support.HeightAt(center)-actor.BodyHeight,bounds);
                        actor.petGravity.PlaceSupported(actor.body.X,actor.body.Y,actor.BodyWidth,actor.BodyHeight,support);
                        actor.body.Action=BodyAction.Walk;actor.poseAction=null;actor.houseControlledThisFrame=false;
                        var failures=actor.NavigationFailures;var unreachable=actor.UnreachableTargets;var houseFailures=actor.HouseRouteFailures;
                        var trips=actor.StairTrips;var connectors=new HashSet<string>();var leftBox=false;var reached=false;var frames=0;
                        for(;frames<12000;frames++)
                        {
                            var previous=actor.Position;actor.houseControlledThisFrame=false;actor.navigationStepped=false;
                            // The final X deliberately remains inside the starting Box. Only a physical drop
                            // followed by both stairs can reach this vertically aligned destination.
                            actor.Navigate(center-actor.HalfWidth,destination.Y-actor.BodyHeight,.016,bounds,120);
                            actor.ObserveNavigationProgress(.016);actor.StepPetGravity(.016);
                            if(actor.houseConnector is {} connector)connectors.Add(connector);
                            leftBox|=actor.petGravity.Grounded&&Math.Abs(actor.body.Y+actor.BodyHeight-source.Y)<.01;
                            if(!double.IsFinite(actor.body.X)||!double.IsFinite(actor.body.Y)||Distance(previous,actor.Position)>4)
                                throw new Exception($"{actor.appearance} made an unbounded movement leaving the real Box.");
                            if(actor.NavigationFailures!=failures||actor.UnreachableTargets!=unreachable||actor.HouseRouteFailures!=houseFailures)
                                throw new Exception($"{actor.appearance} failed the real Box-to-house route: scale={layout.SceneScale}, gap={gap}, phase={actor.MovementPhase}.");
                            if(actor.petGravity.Grounded&&Math.Abs(actor.body.X+actor.HalfWidth-center)<.05&&
                                Math.Abs(actor.body.Y+actor.BodyHeight-destination.Y)<.01&&!actor.FinishingMotion)
                            {reached=true;break;}
                        }
                        if(!reached||!leftBox||actor.StairTrips-trips!=2||connectors.Count!=2)
                            throw new Exception($"{actor.appearance} did not leave its real Box for the aligned other floor: scale={layout.SceneScale}, gap={gap}, feet={actor.body.Y+actor.BodyHeight}, phase={actor.MovementPhase}.");
                        evidence.Add(new{Lifecycle="real-low-box-to-aligned-floor",Character=actor.appearance.ToString(),Compact=compact,
                            From=sourceIndex,To=destination.Index,Scale=layout.SceneScale,BoxFloorGap=gap,Ticks=frames+1,LeftBox=leftBox,
                            StairTrips=actor.StairTrips-trips,Connectors=connectors,NavigationFailures=actor.NavigationFailures-failures,
                            UnreachableTargets=actor.UnreachableTargets-unreachable,HouseRouteFailures=actor.HouseRouteFailures-houseFailures});
                    }
                }
            }
        }
        finally
        {
            foreach(var actor in residents){actor.CancelRoute();actor.Occupancy.Release(actor.appearance);}
            if(box is not null){Furniture.Remove(box);box.Close();}
            ConfigureHouse(3,false);platformCacheKey=int.MinValue;
        }
    }

    private void SmokeHouseActivitySupports(PetWindow[] residents,List<object> evidence)
    {
        var restores=residents.Select(CaptureHouseSequenceActor).ToArray();
        var factories=residents.Select(actor=>(Actor:actor,Factory:actor.ParameterFactory)).ToArray();
        var bounds=Bounds();RoomWindow? fixture=null;
        try
        {
            foreach(var actor in residents)
            {
                actor.ParameterFactory=_=>BehaviorParameters.Default with{Movement=BehaviorParameters.Default.Movement with{Speed=50,PauseFrequency=0,DirectionChange=0,PathCurvature=0}};
                actor.restPreference.Attach([]);
            }
            foreach(var compact in new[]{false,true})
            {
                ConfigureHouse(3,false);
                if(compact)
                {
                    ownedHouse=HouseLayout.Create(3,bounds with{Width=Math.Min(bounds.Width,HouseLayout.MinimumDesignWidth*.4)});
                    houseSurface?.Configure(ownedHouse);
                    foreach(var resident in residents)resident.ApplyHouseGeometry();
                }
                var layout=House!;var floor=layout.Floors[1];
                foreach(var spec in new[]{(PetAppearance.Cat,FurnitureKind.CatBowl,RoomActivity.Feed),
                    (PetAppearance.Girl,FurnitureKind.Bookshelf,RoomActivity.Read),(PetAppearance.BorderCollie,FurnitureKind.PetBed,RoomActivity.Rest)})
                    foreach(var offset in new[]{-8d,8d})
                    {
                        var actor=residents.Single(a=>a.appearance==spec.Item1);
                        fixture=new RoomWindow(new(Guid.NewGuid(),spec.Item2,floor.Left,floor.Y,1));fixture.SetSceneScale(layout.SceneScale);
                        fixture.Relocate(floor.Left+(floor.Width-fixture.Width)*.45,floor.Y-fixture.Height+offset);
                        Furniture.Add(fixture);platformCacheKey=int.MinValue;
                        try
                        {
                            actor.CancelRoute();actor.sequence=null;actor.homeTarget=null;actor.Occupancy.Release(actor.appearance);
                            var from=offset<0?0:2;var startFloor=layout.Floors[from];
                            var start=layout.SafeFootX(from,startFloor.Left+startFloor.Width*.25,actor.HalfWidth);
                            actor.body.Place(start-actor.HalfWidth,startFloor.Y-actor.BodyHeight,bounds);
                            actor.petGravity.PlaceSupported(actor.body.X,actor.body.Y,actor.BodyWidth,actor.BodyHeight,startFloor.Platform);
                            var failures=actor.NavigationFailures;var unreachable=actor.UnreachableTargets;var houseFailures=actor.HouseRouteFailures;
                            var timeouts=actor.ApproachTimeouts;var trips=actor.StairTrips;
                            if(spec.Item3==RoomActivity.Feed)
                            {
                                // This case predates finite inventory. Keep every shifted
                                // cross-floor navigation/support assertion, and test a bite
                                // only when the real mouth can reach the actual bowl.
                                SmokeHouseShiftedFoodSupport(actor,fixture,from,compact,offset,evidence);
                                continue;
                            }
                            actor.StartRoomActivity(spec.Item3);
                            var active=actor.sequence??throw new Exception("The shifted-furniture activity did not start.");
                            var supportFeet=spec.Item3==RoomActivity.Rest?fixture.Platforms[0].Y:floor.Y;
                            if(actor.homeTarget?.Id!=fixture.Item.Id||Math.Abs(actor.homeFeet-supportFeet)>.001)
                                throw new Exception($"{actor.appearance} targeted an image edge instead of physical activity support.");
                            var reached=false;var frames=0;var connectors=new HashSet<string>();
                            for(;frames<30000;frames++)
                            {
                                var previous=actor.Position;actor.houseControlledThisFrame=false;actor.navigationStepped=false;
                                actor.TickSequence(.016);actor.RecordApproachTimeout(active);actor.ObserveNavigationProgress(.016);actor.StepPetGravity(.016);
                                if(actor.houseConnector is {} connector)connectors.Add(connector);
                                if(!double.IsFinite(actor.body.X)||!double.IsFinite(actor.body.Y)||Distance(previous,actor.Position)>16)
                                    throw new Exception("Shifted-furniture activity produced an unbounded movement.");
                                if(active.ApproachTimedOut||actor.ApproachTimeouts!=timeouts||actor.NavigationFailures!=failures||actor.UnreachableTargets!=unreachable||actor.HouseRouteFailures!=houseFailures)
                                    throw new Exception($"{actor.appearance} failed shifted {spec.Item2}: offset={offset}, scale={layout.SceneScale}, feet={actor.body.Y+actor.BodyHeight}, target={actor.homeFeet}.");
                                if(active.Phase==BehaviorPhase.Work){reached=true;break;}
                                if(active.InterruptedBy is not null||actor.sequence is null)break;
                            }
                            if(!reached||!actor.petGravity.Grounded||Math.Abs(actor.body.Y+actor.BodyHeight-supportFeet)>4||actor.StairTrips-trips!=1||connectors.Count!=1)
                                throw new Exception($"{actor.appearance} did not use the real support beside/on shifted {spec.Item2}.");
                            evidence.Add(new{Lifecycle="shifted-furniture-physical-activity",Character=actor.appearance.ToString(),Furniture=spec.Item2.ToString(),
                                Activity=spec.Item3.ToString(),Compact=compact,From=from,To=1,Offset=offset,Scale=layout.SceneScale,
                                TargetFeet=actor.homeFeet,Feet=actor.body.Y+actor.BodyHeight,ImageBottom=fixture.Item.Y+fixture.Height,
                                active.ApproachBudgetSeconds,Ticks=frames+1,StairTrips=actor.StairTrips-trips,
                                ApproachTimeouts=actor.ApproachTimeouts-timeouts,NavigationFailures=actor.NavigationFailures-failures,
                                UnreachableTargets=actor.UnreachableTargets-unreachable,HouseRouteFailures=actor.HouseRouteFailures-houseFailures});
                        }
                        finally{actor.CancelRoute();actor.sequence=null;actor.Occupancy.Release(actor.appearance);Furniture.Remove(fixture);fixture.Close();fixture=null;platformCacheKey=int.MinValue;}
                    }
                // Independently require a reachable, finite same-floor meal and
                // reject a suspended full bowl at both normal and phone scale.
                var cat=residents.Single(a=>a.appearance==PetAppearance.Cat);
                foreach(var suspended in new[]{false,true})
                {
                    fixture=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.CatBowl,floor.Left,floor.Y,1));
                    fixture.SetSceneScale(layout.SceneScale);
                    fixture.Relocate(floor.Left+(floor.Width-fixture.Width)*.45,floor.Y-fixture.Height-(suspended?120*layout.SceneScale:0));
                    Furniture.Add(fixture);platformCacheKey=int.MinValue;
                    try
                    {
                        cat.CancelRoute();cat.sequence=null;cat.homeTarget=null;cat.Occupancy.Release(cat.appearance);
                        var start=layout.SafeFootX(1,floor.Left+floor.Width*.25,cat.HalfWidth);
                        cat.body.Place(start-cat.HalfWidth,floor.Y-cat.BodyHeight,bounds);
                        cat.petGravity.PlaceSupported(cat.body.X,cat.body.Y,cat.BodyWidth,cat.BodyHeight,floor.Platform);
                        SmokeHouseShiftedFoodSupport(cat,fixture,1,compact,suspended?-120*layout.SceneScale:0,evidence,!suspended);
                    }
                    finally{cat.CancelRoute();cat.sequence=null;cat.Occupancy.Release(cat.appearance);Furniture.Remove(fixture);fixture.Close();fixture=null;platformCacheKey=int.MinValue;}
                }
            }
        }
        finally
        {
            if(fixture is not null){Furniture.Remove(fixture);fixture.Close();}
            ConfigureHouse(3,false);platformCacheKey=int.MinValue;
            foreach(var restore in restores)restore();
            foreach(var saved in factories)saved.Actor.ParameterFactory=saved.Factory;
        }
    }

    private static double Distance(RoomPoint first,RoomPoint second)=>Math.Sqrt(Math.Pow(first.X-second.X,2)+Math.Pow(first.Y-second.Y,2));

    private static void RenderHouseCharacterComparison(string path,PetWindow[] actors)
    {
        const int width=720,height=330;const double feet=290;
        var drawing=new DrawingVisual();
        using(var dc=drawing.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(253,247,240)),null,new Rect(0,0,width,height));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(180,144,120)),2),new Point(20,feet),new Point(width-20,feet));
            for(var i=0;i<actors.Length;i++)
            {
                var actor=actors[i];actor.body.Action=BodyAction.Idle;actor.poseAction=null;actor.ApplyPose(.7);actor.ApplyPosition();actor.UpdateLayout();
                var content=(FrameworkElement)actor.Content;
                content.Measure(new Size(actor.BodyWidth,actor.BodyHeight));content.Arrange(new Rect(0,0,actor.BodyWidth,actor.BodyHeight));content.UpdateLayout();
                var frame=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(actor.BodyWidth)),Math.Max(1,(int)Math.Ceiling(actor.BodyHeight)),96,96,PixelFormats.Pbgra32);
                frame.Render(content);
                var visualHeight=actor.BodyHeight;var visualWidth=actor.BodyWidth;
                dc.DrawImage(frame,new Rect(25+i*230,feet-visualHeight,visualWidth,visualHeight));
                dc.DrawText(new FormattedText($"{actor.appearance} {visualHeight:F1} DIP",System.Globalization.CultureInfo.GetCultureInfo("zh-TW"),FlowDirection.LeftToRight,new Typeface("Microsoft JhengHei"),13,Brushes.SaddleBrown,1),new Point(25+i*230,feet+12));
            }
        }
        var sheet=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);sheet.Render(drawing);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(sheet));using var file=File.Create(path);encoder.Save(file);
    }
}
