using System.IO;
using System.Windows;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    private static void ValidateHouseFoodDiagnosticProfile(string root)
    {
        var full=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DesktopLifeSmoke"));
        if(!string.Equals(Path.GetDirectoryName(full),allowed,StringComparison.OrdinalIgnoreCase)||
            !Guid.TryParseExact(Path.GetFileName(full),"N",out _))
            throw new InvalidOperationException("House food fixtures require a fresh isolated TEMP/GUID profile.");
    }

    // The fixture owns both immutable transaction results. It never delegates a
    // synthetic bite to MainWindow's saved organism or grants a free Feed reward.
    private sealed class HouseFoodDiagnosticFixture:IDisposable
    {
        private readonly PetWindow actor;
        private readonly RoomWindow surface;
        private readonly Func<Guid,FoodServing?>? oldFood;
        private readonly Func<Guid,PetAppearance,FoodReservation?>? oldReserve;
        private readonly Action<FoodReservation>? oldRelease;
        private readonly Func<FoodContact,bool>? oldContact;
        private readonly PetState oldNeeds;
        private readonly int oldContacts;
        private readonly FoodReservations reservations=new();
        internal FoodInventoryState Inventory {get;private set;}=new();
        internal PetState Needs {get;private set;}
        internal int AcceptedBites {get;private set;}
        internal int ActiveReservations=>reservations.ActiveCount;

        internal HouseFoodDiagnosticFixture(PetWindow actor,RoomWindow surface)
        {
            this.actor=actor;this.surface=surface;
            oldFood=actor.FoodForFurniture;oldReserve=actor.ReserveFood;oldRelease=actor.ReleaseFood;oldContact=actor.FoodContactRequested;
            oldNeeds=actor.EmotionalState;oldContacts=actor.FoodContactCount;
            actor.ReleaseFoodReservation();
            Needs=oldNeeds with{Hunger=70};actor.EmotionalState=Needs;
            var added=FoodInventory.Add(Inventory,[surface.Item],surface.Item.Id,FoodKind.CatKibble,2);
            if(!added.Accepted)throw new InvalidOperationException("House food fixture could not create its finite serving.");
            Inventory=added.Inventory;surface.SetFood(Inventory.Find(surface.Item.Id));
            actor.FoodForFurniture=id=>Inventory.Find(id);
            actor.ReserveFood=(id,kind)=>Inventory.Find(id) is {} serving?
                reservations.Reserve(Inventory,[surface.Item],[actor.appearance],id,serving.ServingId,kind).Reservation:null;
            actor.ReleaseFood=claim=>reservations.Release(claim);
            actor.FoodContactRequested=contact=>
            {
                if(contact.Resident!=actor.appearance||!contact.IsActualContact||!actor.petGravity.Grounded)
                    throw new InvalidOperationException("House food fixture received an unsupported bite.");
                var before=Inventory.Find(surface.Item.Id)!;
                var result=reservations.Consume(Inventory,Needs,[surface.Item],[actor.appearance],contact);
                if(!result.Accepted)return false;
                if(result.ConsumedPortions!=1||result.Inventory.Find(surface.Item.Id) is not {} after||
                    after.ServingId!=before.ServingId||after.Revision!=before.Revision+1||after.RemainingPortions!=before.RemainingPortions-1||
                    Math.Abs(result.Pet.Hunger-(Needs.Hunger-FoodCatalog.HungerPerPortion(before.Kind)))>1e-8)
                    throw new InvalidOperationException("House food fixture failed its atomic one-portion transaction.");
                Inventory=result.Inventory;Needs=result.Pet;actor.EmotionalState=Needs;AcceptedBites++;
                surface.SetFood(Inventory.Find(surface.Item.Id));return true;
            };
        }

        public void Dispose()
        {
            actor.ReleaseFoodReservation();reservations.Clear();
            actor.FoodForFurniture=oldFood;actor.ReserveFood=oldReserve;actor.ReleaseFood=oldRelease;actor.FoodContactRequested=oldContact;
            actor.EmotionalState=oldNeeds;actor.FoodContactCount=oldContacts;surface.SetFood(null);
        }
    }

    private void SmokeHouseShiftedFoodSupport(PetWindow actor,RoomWindow surface,int from,bool compact,double offset,
        List<object> evidence,bool? requiredReachable=null)
    {
        using var food=new HouseFoodDiagnosticFixture(actor,surface);
        var floor=House!.Floors[surface.Item.FloorIndex];var bounds=Bounds();
        var spot=actor.ActivitySpot(surface,RoomActivity.Feed,Furniture.Select(f=>f.Item).ToArray());
        if(Math.Abs(spot.Feet-floor.Y)>.001)throw new InvalidOperationException("Food approach used a shifted image edge as floor support.");
        // An independent geometric oracle for the current illustrated cat mouth.
        // Stock existence alone cannot make a suspended/misaligned bowl reachable.
        var mouth=new Point(spot.X,spot.Feet-32*actor.BodyHeight/144);
        var mouthGap=(mouth-surface.FoodContactPoint).Length;
        var reachable=mouthGap<=18*actor.sceneScale;
        if(requiredReachable is {} required&&reachable!=required)
            throw new InvalidOperationException("Canonical house food fixture did not exercise its required reachability.");
        var failures=actor.NavigationFailures;var unreachable=actor.UnreachableTargets;var houseFailures=actor.HouseRouteFailures;
        var timeouts=actor.ApproachTimeouts;var trips=actor.StairTrips;var contacts=actor.FoodContactCount;
        var serving=food.Inventory.Find(surface.Item.Id)!;var connectors=new HashSet<string>();var frames=0;
        actor.StartRoomActivity(RoomActivity.Feed);
        var active=actor.sequence;
        if(reachable)
        {
            if(active is null||!actor.HasFoodReservationDiagnostic||food.ActiveReservations!=1||actor.homeTarget?.Id!=surface.Item.Id||
                Math.Abs(actor.homeFeet-floor.Y)>.001)
                throw new InvalidOperationException("Reachable finite house food did not start its real reserved activity.");
        }
        else if(active is not null||actor.HasFoodReservationDiagnostic||food.ActiveReservations!=0||actor.CurrentRoomActivity is not null)
            throw new InvalidOperationException("Misaligned house bowl started a remote eating sequence.");
        if(food.AcceptedBites!=0||food.Needs.Hunger!=70||food.Inventory.Find(surface.Item.Id)!=serving)
            throw new InvalidOperationException("House food approach granted a portion before physical contact.");

        var reached=false;
        for(;frames<30000;frames++)
        {
            var previous=actor.Position;actor.houseControlledThisFrame=false;actor.navigationStepped=false;
            if(reachable){actor.TickSequence(.016);actor.RecordApproachTimeout(active!);}
            else
            {
                // Keep the original shifted-fixture regression's cross-floor
                // travel even when the finite food contact itself is rejected.
                actor.body.Action=BodyAction.Walk;
                actor.Navigate(spot.X-actor.HalfWidth,floor.Y-actor.BodyHeight,.016,bounds,actor.Parameters.Movement.Speed*.8);
            }
            actor.ObserveNavigationProgress(.016);actor.StepPetGravity(.016);
            actor.ApplyPosition();actor.ApplyPose(actor.clock.Elapsed.TotalSeconds+(frames+1)*.016);actor.UpdateLayout();
            if(actor.houseConnector is {} connector)connectors.Add(connector);
            if(!double.IsFinite(actor.body.X)||!double.IsFinite(actor.body.Y)||Distance(previous,actor.Position)>16||
                actor.ApproachTimeouts!=timeouts||actor.NavigationFailures!=failures||actor.UnreachableTargets!=unreachable||
                actor.HouseRouteFailures!=houseFailures||active?.ApproachTimedOut==true)
                throw new InvalidOperationException($"Shifted finite house food failed physical navigation: offset={offset}, scale={sceneScale}.");
            if(reachable)
            {
                if(food.AcceptedBites>0){reached=true;break;}
                if(actor.sequence is null||active!.InterruptedBy is not null)break;
            }
            else if(actor.petGravity.Grounded&&Math.Abs(actor.body.X+actor.HalfWidth-spot.X)<.05&&
                Math.Abs(actor.body.Y+actor.BodyHeight-floor.Y)<.01&&!actor.FinishingMotion){reached=true;break;}
        }
        var expectedTrips=Math.Abs(from-floor.Index);
        if(!reached||!actor.petGravity.Grounded||Math.Abs(actor.body.Y+actor.BodyHeight-floor.Y)>4||
            actor.StairTrips-trips!=expectedTrips||connectors.Count!=expectedTrips)
            throw new InvalidOperationException("Shifted finite house food lost actual floor/stair support: "+
                System.Text.Json.JsonSerializer.Serialize(new{compact,offset,from,TargetFloor=floor.Index,reachable,reached,frames,
                    Position=actor.Position,Feet=actor.body.Y+actor.BodyHeight,TargetFeet=floor.Y,TargetX=spot.X,
                    actor.petGravity.Grounded,ActualTrips=actor.StairTrips-trips,expectedTrips,Connectors=connectors,
                    actor.FinishingMotion,Phase=active?.Phase.ToString(),food.AcceptedBites}));
        if(!reachable)actor.StartRoomActivity(RoomActivity.Feed);
        var after=food.Inventory.Find(surface.Item.Id)!;
        if(reachable)
        {
            if(food.AcceptedBites!=1||actor.FoodContactCount-contacts!=1||after.ServingId!=serving.ServingId||
                after.RemainingPortions!=1||after.Revision!=1||food.Needs.Hunger!=68)
                throw new InvalidOperationException("Supported house eating failed its actual one-portion transaction.");
        }
        else if(food.AcceptedBites!=0||actor.FoodContactCount!=contacts||after!=serving||food.Needs.Hunger!=70||
            actor.HasFoodReservationDiagnostic||food.ActiveReservations!=0||actor.sequence is not null)
            throw new InvalidOperationException("Reaching a floor beside suspended food incorrectly granted a bite.");
        evidence.Add(new{Lifecycle=requiredReachable is null?"shifted-furniture-physical-activity":reachable?"same-floor-finite-food":"suspended-finite-food-rejected",
            Character=actor.appearance.ToString(),Furniture=surface.Item.Kind.ToString(),Activity="Feed",Compact=compact,From=from,To=floor.Index,
            Offset=offset,Scale=House.SceneScale,TargetFeet=spot.Feet,Feet=actor.body.Y+actor.BodyHeight,ImageBottom=surface.Item.Y+surface.Height,
            MouthGap=mouthGap,FoodReachable=reachable,FoodStarted=reachable,InitialPortions=2,RemainingPortions=after.RemainingPortions,
            food.AcceptedBites,Hunger=food.Needs.Hunger,FoodContacts=actor.FoodContactCount-contacts,FreshServing=serving.ServingId,
            Ticks=frames+1,StairTrips=actor.StairTrips-trips,Connectors=connectors,
            ApproachTimeouts=actor.ApproachTimeouts-timeouts,NavigationFailures=actor.NavigationFailures-failures,
            UnreachableTargets=actor.UnreachableTargets-unreachable,HouseRouteFailures=actor.HouseRouteFailures-houseFailures,Passed=true});
    }
}
