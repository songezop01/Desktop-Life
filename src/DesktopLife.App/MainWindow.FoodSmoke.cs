using DesktopLife.Core;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace DesktopLife.App;

public partial class MainWindow
{
    internal async Task SmokeFood(string root)
    {
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DesktopLifeSmoke"));
        var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetDirectoryName(full), allowed, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(Path.GetFileName(full), "N", out _))
            throw new Exception("Food smoke requires a synthetic profile.");
        FreezeRestartVerification();
        aiPaused = false;
        Priorities.SelectedItem=DisplayPriority.Highest;
        settings = settings with { Presence = PresenceMode.All }; UpdatePetVisibility();
        foreach (var actor in characterWindows) {actor.PrepareCareMenuDiagnostic(0);actor.Topmost=true;actor.Show();}
        Pet.ConfigureHouse(1, false);
        foreach (var surface in Pet.Furniture.ToArray()) surface.Close();
        Pet.Furniture.Clear(); food = new(); foodReservations.Clear();
        var floor = Pet.House!.Floors[0];
        var bowl = new RoomItem(Guid.NewGuid(), FurnitureKind.CatBowl, floor.Left + 300, floor.Y - 28);
        var table = new RoomItem(Guid.NewGuid(), FurnitureKind.DiningTable, floor.Left + 600, floor.Y - 160);
        Pet.AddRoomItem(bowl); Pet.AddRoomItem(table); RefreshFoodSurfaces();
        var bowlSurface = Pet.Furniture.Single(f => f.Item.Id == bowl.Id);
        var tableSurface = Pet.Furniture.Single(f => f.Item.Id == table.Id);
        var evidence = new List<object>();
        var cat = CharacterWindow(PetAppearance.Cat); var dog = CharacterWindow(PetAppearance.BorderCollie); var girl = CharacterWindow(PetAppearance.Girl);
        void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Reset(PetWindow actor) { actor.PrepareCareMenuDiagnostic(0); LifeFor(actor == cat ? PetAppearance.Cat : actor == dog ? PetAppearance.BorderCollie : PetAppearance.Girl).ApplyCare(new() { Hunger = 70 }); }
        void Case(string name) => evidence.Add(new { Case = name, Passed = true });
        SmokeFoodArtwork(root);Case("five-distinct-transparent-meals-and-decreasing-kibble");
        Reset(cat); var original = LifeFor(PetAppearance.Cat).State;
        cat.StartFoodDiagnostic(); Assert(!cat.HasFoodReservationDiagnostic && cat.CurrentRoomActivity is null && LifeFor(PetAppearance.Cat).State == original, "Empty bowl produced eating or needs effect."); Case("empty-bowl-no-eating");
        bowlSurface.ChooseFoodDiagnostic(FoodKind.CatKibble);
        Assert(food.Find(bowl.Id) is { RemainingPortions: 60 }, "Real bowl menu did not fill stock."); Case("real-bowl-menu-fills");
        bowlSurface.Relocate(bowl.X, floor.Y - 250); Reset(cat); cat.StartFoodDiagnostic();
        Assert(!cat.HasFoodReservationDiagnostic && food.Find(bowl.Id)!.RemainingPortions == 60 && LifeFor(PetAppearance.Cat).State.Hunger == 70, "Suspended bowl granted remote food.");
        bowlSurface.Relocate(bowl.X, bowl.Y); Case("suspended-bowl-not-reachable");
        Reset(dog); dog.StartFoodDiagnostic(); Assert(!dog.HasFoodReservationDiagnostic, "Dog ate cat-specific kibble."); Case("dog-diet-rejection");
        Reset(girl); girl.StartFoodDiagnostic(); Assert(!girl.HasFoodReservationDiagnostic, "Girl ate pet kibble."); Case("girl-diet-rejection");
        Reset(cat); cat.StartFoodDiagnostic(); Assert(cat.HasFoodReservationDiagnostic && food.Find(bowl.Id)!.RemainingPortions == 60 && LifeFor(PetAppearance.Cat).State.Hunger == 70,
            "Approach did not reserve unchanged stock and hunger: "+JsonSerializer.Serialize(new{cat.HasFoodReservationDiagnostic,Serving=food.Find(bowl.Id),
                Hunger=LifeFor(PetAppearance.Cat).State.Hunger,cat.IsVisible,cat.Interacting,cat.FinishingMotion,cat.CurrentAction,cat.CurrentPhase,Residents=FoodResidents(),Reservations=foodReservations.ActiveCount})); Case("approach-does-not-feed");
        cat.FinishFoodDiagnostic(1, true);
        Assert(food.Find(bowl.Id)!.RemainingPortions == 59 && LifeFor(PetAppearance.Cat).State.Hunger == 68 && cat.FoodContactCount == 1, "Actual supported eating did not consume exactly one portion."); Case("actual-eating-consumes-one");
        Assert(SavePetState(), "Food save failed."); var saved = organismStore.Load();
        Assert(saved.Food.Find(bowl.Id)!.RemainingPortions == 59 && saved.GetCharacter(PetAppearance.Cat)!.Pet.State.Hunger == 68, "Food and hunger did not reload together."); Case("atomic-food-and-needs-reload");
        cat.SuspendPresence(); cat.Show(); Assert(foodReservations.ActiveCount == 0, "Hiding did not release food claim."); Case("hide-releases-food");
        bowlSurface.ChooseFoodDiagnostic(FoodKind.SharedKibble);
        Reset(cat); cat.StartFoodDiagnostic(); Reset(dog); dog.StartFoodDiagnostic(); Assert(cat.HasFoodReservationDiagnostic && !dog.HasFoodReservationDiagnostic, "Shared bowl allowed two eaters."); Case("exclusive-shared-bowl");
        cat.ResetPosition(); Reset(dog); dog.StartFoodDiagnostic(); Assert(dog.HasFoodReservationDiagnostic, "Released bowl stayed locked."); Case("reset-releases-to-another-resident");
        dog.SetPaused(true); Assert(foodReservations.ActiveCount == 0, "Pause retained food reservation."); dog.SetPaused(false); dog.FinishFoodDiagnostic(1, false); Case("pause-cancels-without-bite");
        Reset(cat); cat.StartFoodDiagnostic(); var beforeSwap = cat.FoodContactCount;
        bowlSurface.ChooseFoodDiagnostic(FoodKind.DogKibble); cat.FinishFoodDiagnostic(1, false);
        Assert(food.Find(bowl.Id) is { Kind: FoodKind.DogKibble, RemainingPortions: 60 } && cat.FoodContactCount == beforeSwap, "Meal replacement consumed a stale portion."); Case("replacement-invalidates-old-approach");
        tableSurface.ChooseFoodDiagnostic(FoodKind.Ramen); Reset(girl); girl.StartFoodDiagnostic(); Assert(girl.HasFoodReservationDiagnostic, "Girl did not reserve table meal.");
        girl.FinishFoodDiagnostic(1, true); Assert(food.Find(table.Id) is { RemainingPortions: 11 } && LifeFor(PetAppearance.Girl).State.Hunger == 67.5, "Girl did not eat the meal at its table."); Case("girl-table-eating");
        var misplacedChair=new RoomItem(Guid.NewGuid(),FurnitureKind.Chair,table.X-170,floor.Y-145);
        Pet.AddRoomItem(misplacedChair);Reset(girl);var beforeChairMeal=food.Find(table.Id)!.RemainingPortions;girl.StartFoodDiagnostic();
        Assert(girl.HasFoodReservationDiagnostic,"A misaligned decorative chair blocked a reachable standing meal.");
        girl.FinishFoodDiagnostic(1,true);Assert(food.Find(table.Id)!.RemainingPortions==beforeChairMeal-1,"Standing fallback did not consume real stock.");
        var chairSurface=Pet.Furniture.Single(f=>f.Item.Id==misplacedChair.Id);Pet.Furniture.Remove(chairSurface);chairSurface.Close();Case("misaligned-chair-falls-back-to-real-standing-meal");
        Reset(girl); var remaining = food.Find(table.Id)!.RemainingPortions; TickFood();
        Assert(food.Find(table.Id)!.RemainingPortions == remaining && girl.HasFoodReservationDiagnostic, $"Autonomous food request ate before contact or did not start: paused={aiPaused}, visible={girl.IsVisible}, editing={Pet.EditingRoom}, busy={girl.FinishingMotion}, action={girl.CurrentAction}, claim={girl.HasFoodReservationDiagnostic}."); Case("autonomous-hungry-request");
        girl.ResetPosition();
        tableSurface.Relocate(table.X, table.Y - 180); Reset(girl); girl.StartFoodDiagnostic();
        Assert(!girl.HasFoodReservationDiagnostic && food.Find(table.Id)!.RemainingPortions == remaining, "Girl received food from an elevated table.");
        tableSurface.Relocate(table.X, table.Y); Case("remote-table-not-reachable");
        var single = FoodInventory.Replace(food, FoodRoom(), bowl.Id, FoodKind.DogKibble, 1); food = single.Inventory;
        Reset(dog); dog.StartFoodDiagnostic(); dog.FinishFoodDiagnostic(1, true); var finalCount = dog.FoodContactCount;
        dog.FinishFoodDiagnostic(1, false); Assert(food.Find(bowl.Id)!.RemainingPortions == 0 && dog.FoodContactCount == finalCount, "Empty bowl over-consumed."); Case("last-portion-stops-eating");
        Reset(cat); var hungerBefore = LifeFor(PetAppearance.Cat).State.Hunger; var careBefore = CompanionFor(PetAppearance.Cat).CareCount;
        SelectCare(PetAppearance.Cat); CareSelected(CareKind.Feed);
        Assert(LifeFor(PetAppearance.Cat).State.Hunger == hungerBefore && CompanionFor(PetAppearance.Cat).CareCount == careBefore, "Legacy feeding bypassed physical inventory."); Case("legacy-command-cannot-grant-food");
        var previousIds = Pet.Furniture.Select(f => f.Item.Id).ToHashSet();
        FurnitureOptions.SelectedItem = FurnitureKind.CatBowl; AddFurniture(this, new RoutedEventArgs());
        var added = Pet.Furniture.Single(f => !previousIds.Contains(f.Item.Id));
        added.ChooseFoodDiagnostic(FoodKind.SharedKibble);
        Assert(food.Find(added.Item.Id) is { RemainingPortions: 60 }, "New food surface was not interactive until dragged.");
        EditRoom.IsChecked = false; Case("new-bowl-interactive-without-drag");
        Pet.Furniture.Remove(tableSurface); tableSurface.Close(); PruneFoodToRoom(); Assert(SavePetState(), "Removed-food furniture could not save.");
        Assert(organismStore.Load().Food.Find(table.Id) is null, "Removed table left orphan food."); Case("removed-furniture-prunes-food");
        await SavePetStateAsync();
        File.WriteAllText(Path.Combine(root, "food-interaction-checks.json"), JsonSerializer.Serialize(new { Passed = true, Scope = "Native food transactions and rendered illustrations; not elapsed stress or Full verification", Cases = evidence }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
