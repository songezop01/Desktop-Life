using DesktopLife.Core;
using System.Collections.Immutable;

namespace DesktopLife.App;

public partial class MainWindow
{
    private FoodInventoryState food = new();
    private readonly FoodReservations foodReservations = new();
    private readonly HashSet<Guid> attachedFoodSurfaces = [];
    private RoomItem[] FoodRoom() => Pet.Furniture.Select(f => f.Item).ToArray();
    private PetAppearance[] FoodResidents() => Enum.GetValues<PetAppearance>()
        .Where(kind => PresencePolicy.Includes(CurrentPresence, kind) && CharacterWindow(kind).IsVisible).ToArray();
    private HomeostasisSession LifeFor(PetAppearance kind) => kind == settings.PetAppearance ? Life : RuntimeFor(kind)!.Life;

    private void InitializeFood(OrganismSnapshot checkpoint)
    {
        food = checkpoint.Food;
        Pet.FurnitureAdded += _ => RefreshFoodSurfaces();
        foreach (var actor in characterWindows)
        {
            actor.FoodForFurniture = id => food.Find(id);
            actor.ReserveFood = (id, kind) => food.Find(id) is {} serving
                ? foodReservations.Reserve(food, FoodRoom(), FoodResidents(), id, serving.ServingId, kind).Reservation : null;
            actor.ReleaseFood = reservation => foodReservations.Release(reservation);
            actor.FoodContactRequested = contact =>
            {
                var life = LifeFor(contact.Resident);
                var result = foodReservations.Consume(food, life.State, FoodRoom(), FoodResidents(), contact);
                if (!result.Accepted) return false;
                // Both immutable results become live on the dispatcher before a save
                // or another resident's contact can capture either half of the meal.
                food = result.Inventory;
                life.ApplyCare(result.Pet);
                RefreshFoodSurfaces();
                QueuePetSave();
                return true;
            };
        }
        Pet.RoomChanged += () =>
        {
            var room = FoodRoom();
            PruneFoodToRoom();
            foodReservations.Prune(food, room, FoodResidents());
            RefreshFoodSurfaces();
        };
        RefreshFoodSurfaces();
    }

    private void PruneFoodToRoom()
    {
        var room = FoodRoom();
        food = food with { Servings = food.Servings.Where(s => room.Any(item => item.Id == s.FurnitureId && FoodCatalog.CanServe(item.Kind, s.Kind))).ToImmutableArray() };
    }

    private void RefreshFoodSurfaces()
    {
        foreach (var surface in Pet.Furniture.Where(f => f.IsFoodSurface))
        {
            if (attachedFoodSurfaces.Add(surface.Item.Id)) surface.FoodRequested += (kind, replace) => SupplyFood(surface, kind, replace);
            surface.SetFood(food.Find(surface.Item.Id));
        }
        attachedFoodSurfaces.IntersectWith(Pet.Furniture.Select(f => f.Item.Id));
    }

    private void SupplyFood(RoomWindow surface, FoodKind kind, bool replace)
    {
        if (saveClosing || runtimeDiagnosticSceneSuspended) return;
        var result = replace ? FoodInventory.Replace(food, FoodRoom(), surface.Item.Id, kind, FoodCatalog.Capacity(kind))
            : FoodInventory.Add(food, FoodRoom(), surface.Item.Id, kind, FoodCatalog.Capacity(kind));
        if (!result.Accepted)
        {
            surface.ShowFoodNotice(result.Failure == FoodFailure.Full ? "已經裝滿了。" : "目前無法補充，請重新選擇料理。");
            return;
        }
        food = result.Inventory;
        foodReservations.Prune(food, FoodRoom(), FoodResidents());
        RefreshFoodSurfaces();
        QueuePetSave();
    }

    private void TickFood()
    {
        foodReservations.Prune(food, FoodRoom(), FoodResidents());
        if (aiPaused || saveClosing || suspendedAt is not null || Pet.EditingRoom || runtimeDiagnosticSceneSuspended) return;
        foreach (var kind in FoodResidents())
        {
            var actor = CharacterWindow(kind);
            if (LifeFor(kind).State.Hunger < 55 || actor.Interacting || actor.FinishingMotion || actor.CurrentAction == BodyAction.Sleep || actor.SequenceCommitted) continue;
            actor.TryStartFoodActivity();
        }
    }
}
