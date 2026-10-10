using System.IO;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    private void SmokeGirlMealLifePose(PetWindow girl,string root)
    {
        // Feed is no longer a free room pose. Reach a real supplied table through
        // the same inventory and contact transaction as an autonomous meal.
        var temporary=false;var table=Pet.Furniture.FirstOrDefault(f=>f.Item.Kind==FurnitureKind.DiningTable);
        if(table is null)
        {
            var floor=Pet.House?.Floors[0];var bounds=DisplayWorkspace.Bounds;var scale=Pet.House?.SceneScale??1;
            var item=new RoomItem(Guid.NewGuid(),FurnitureKind.DiningTable,(floor?.Left??bounds.Left)+300*scale,(floor?.Y??bounds.Top+bounds.Height)-160*scale);
            Pet.AddRoomItem(item);table=Pet.Furniture.Single(f=>f.Item.Id==item.Id);temporary=true;
        }
        try
        {
            table.ChooseFoodDiagnostic(FoodKind.Ramen);girl.PrepareCareMenuDiagnostic(0);
            LifeFor(PetAppearance.Girl).ApplyCare(new(){Hunger=70});girl.StartFoodDiagnostic();
            if(!girl.HasFoodReservationDiagnostic)throw new Exception("Girl meal life pose did not reserve actual table stock.");
            var before=food.Find(table.Item.Id)!.RemainingPortions;
            girl.FinishFoodDiagnostic(1,true);girl.RenderDirectCareDiagnostic(Path.Combine(root,"girl-life-Feed.png"));
            if(food.Find(table.Item.Id)!.RemainingPortions!=before-1)throw new Exception("Girl meal life pose did not consume its actual meal.");
            girl.ResetPosition();
        }
        finally{if(temporary){Pet.Furniture.Remove(table);table.Close();PruneFoodToRoom();}}
    }
}
