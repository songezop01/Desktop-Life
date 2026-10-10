using System.Diagnostics;
using System.Windows;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    private async Task<object> SmokeDisplayDirectInputs(string root)
    {
        var house=Pet.House!;var oldPause=aiPaused;var added=new List<RoomWindow>();var rows=new List<object>();
        try
        {
            aiPaused=false;
            // All furniture and care data belongs to this candidate's synthetic
            // profile. Every temporary tool is removed before the next preset.
            foreach(var (kind,index) in careKinds.Select((k,i)=>(k,i)))
            {
                var actor=CharacterWindow(kind);var location=actor.Position;
                actor.PrepareCareMenuDiagnostic(index);actor.PlaceDirectCareDiagnostic(location);actor.Topmost=true;
                var point=actor.DirectHeadDiagnosticPoint();var count=CompanionFor(kind).CareCount;
                CompanionFor(kind).LastCare.Clear();
                for(var i=0;i<12;i++)actor.SampleDirectCareDiagnostic(point,.033);
                if(!actor.HoverHandVisible||CompanionFor(kind).CareCount!=count)throw new Exception("DPI hover failed or granted stationary care: "+kind+"; "+
                    System.Text.Json.JsonSerializer.Serialize(new{Display=DisplayWorkspace.Active.Id,house.FloorCount,house.SceneScale,Before=count,
                        After=CompanionFor(kind).CareCount,State=actor.HoverContactDiagnostic(point)}));
                actor.SampleDirectCareDiagnostic(null,.033);
                for(var i=0;i<12;i++)actor.SampleDirectCareDiagnostic(new(point.X+Math.Min(8,i),point.Y),.033);
                if(CompanionFor(kind).CareCount!=count+1)throw new Exception("DPI head stroke missed the actual resident: "+kind);
                actor.SampleDirectCareDiagnostic(null,.033);rows.Add(new{Resident=kind.ToString(),HoverHand=true,ActualStroke=true});
            }
            var floor=house.Floors[0];var scale=house.SceneScale;
            RoomWindow Add(FurnitureKind kind,double x,double height)
            {
                var item=new RoomItem(Guid.NewGuid(),kind,x,floor.Y-height*scale);
                Pet.AddRoomItem(item);var surface=Pet.Furniture.Single(f=>f.Item.Id==item.Id);added.Add(surface);surface.Topmost=true;surface.SetEditing(false);surface.Show();return surface;
            }
            var bowl=Add(FurnitureKind.CatBowl,floor.Left+floor.Width*.08,28);
            var table=Add(FurnitureKind.DiningTable,floor.Left+floor.Width*.30,160);
            var comb=Add(FurnitureKind.Comb,floor.Left+floor.Width*.72,40);
            await Task.Delay(60);
            bowl.OpenFoodMenuDiagnostic();bowl.ChooseFoodDiagnostic(FoodKind.SharedKibble);bowl.CloseFoodMenuDiagnostic();
            table.OpenFoodMenuDiagnostic();table.ChooseFoodDiagnostic(FoodKind.HotPot);table.CloseFoodMenuDiagnostic();
            if(food.Find(bowl.Item.Id)?.RemainingPortions!=60||food.Find(table.Item.Id)?.RemainingPortions!=16)
                throw new Exception("DPI food hit strip did not supply its actual furniture.");
            var cat=CharacterWindow(PetAppearance.Cat);var target=cat.DirectGroomDiagnosticPoint();var before=CompanionFor(PetAppearance.Cat).CareCount;
            var savedStand=comb.Item;var nativeComb=DisplayWorkspace.NativeWindowBounds(comb);
            if(Math.Abs(nativeComb.Dpi/96d-DisplayWorkspace.Active.Scale)>.01)
                throw new Exception("Fresh comb capture surface has the wrong native DPI.");
            comb.BeginCombDiagnostic();
            var stroke=Stopwatch.StartNew();var previousSample=stroke.Elapsed.TotalSeconds;var maximumGap=0d;
            var contactSamples=new List<object>();
            for(var i=0;i<16;i++)
            {
                var requested=new Point(target.X+Math.Min(12,i),target.Y);comb.MoveCombDiagnostic(requested);
                if((comb.CombTip-requested).Length>.5)
                    throw new Exception("Scaled comb tip was clamped away from the actual grooming strip.");
                await Task.Delay(20);
                var now=stroke.Elapsed.TotalSeconds;var elapsed=now-previousSample;previousSample=now;
                maximumGap=Math.Max(maximumGap,elapsed);cat.TickHeldCombDiagnostic(elapsed);
                contactSamples.Add(new{Sample=i,Elapsed=elapsed,State=cat.CombContactDiagnostic(comb),CareCount=CompanionFor(PetAppearance.Cat).CareCount});
            }
            comb.ReleaseCombDiagnostic();
            var returned=DisplayWorkspace.RoomPosition(comb);
            if(comb.CombHeld||comb.Item!=savedStand||Math.Abs(returned.X-savedStand.X)>1||Math.Abs(returned.Y-savedStand.Y)>1)
                throw new Exception("DPI comb release did not restore its unchanged saved stand.");
            System.IO.File.WriteAllText(System.IO.Path.Combine(root,"display-comb-contact-samples.json"),System.Text.Json.JsonSerializer.Serialize(
                new{Display=DisplayWorkspace.Active.Id,DisplayWorkspace.Active.Scale,Floors=house.FloorCount,house.SceneScale,Before=before,After=CompanionFor(PetAppearance.Cat).CareCount,Samples=contactSamples},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            if(CompanionFor(PetAppearance.Cat).CareCount!=before+1)throw new Exception("DPI comb capture or real fur contact failed: "+DisplayWorkspace.Active.Id+", "+house.FloorCount+" floors; see display-comb-contact-samples.json.");
            return new{Succeeded=true,Residents=rows,SharedBowlLeftClick=true,MealTableLeftClick=true,CombDrag=true,
                CombReturnedToStand=true,CombNativeDpi=nativeComb.Dpi,CombStrokeDistanceDip=12,CombElapsedSeconds=stroke.Elapsed.TotalSeconds,
                CombMaximumSampleGapSeconds=maximumGap,ActualNativeScale=DisplayWorkspace.Active.Scale,house.SceneScale};
        }
        finally
        {
            aiPaused=oldPause;
            foreach(var f in added){f.CancelCombDrag();f.CloseFoodMenuDiagnostic();Pet.Furniture.Remove(f);f.Close();}
            foreach(var actor in characterWindows){actor.CancelDirectCare();actor.SetPaused(true);}PruneFoodToRoom();
        }
    }
}
