using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace DesktopLife.Core;

public enum FoodKind { CatKibble, DogKibble, SharedKibble, BurgerMeal, FriedChickenMeal, HotPot, Ramen, ChickenCutlet }
public enum FoodFailure
{
    None, InvalidRequest, UnknownResident, MissingFurniture, UnsupportedFurniture, WrongDiet,
    NoFood, FoodChanged, DifferentFood, Full, Reserved, ReservationMismatch, NoContact, StaleContact, NotHungry
}

public static class FoodCatalog
{
    public static bool IsPetFood(FoodKind kind) => kind is FoodKind.CatKibble or FoodKind.DogKibble or FoodKind.SharedKibble;
    public static int Capacity(FoodKind kind) => !Enum.IsDefined(kind) ? 0 : IsPetFood(kind) ? 60 : kind == FoodKind.HotPot ? 16 : 12;
    public static bool CanServe(FurnitureKind furniture, FoodKind food) => Enum.IsDefined(food) && furniture switch
    {
        FurnitureKind.CatBowl => IsPetFood(food),
        FurnitureKind.DiningTable => !IsPetFood(food),
        _ => false
    };
    public static bool CanEat(PetAppearance resident, FoodKind food) => Enum.IsDefined(food) && resident switch
    {
        PetAppearance.Cat => food is FoodKind.CatKibble or FoodKind.SharedKibble,
        PetAppearance.BorderCollie => food is FoodKind.DogKibble or FoodKind.SharedKibble,
        PetAppearance.Girl => !IsPetFood(food),
        _ => false
    };
    public static double HungerPerPortion(FoodKind food) => IsPetFood(food) ? 2 : 2.5;
}

// A refill or an explicit replacement starts a new serving. Within that serving,
// each accepted bite advances Revision; contact events retain their captured revision.
public sealed record FoodServing
{
    [JsonRequired] public Guid FurnitureId { get; init; }
    [JsonRequired] public Guid ServingId { get; init; }
    [JsonRequired] public FoodKind Kind { get; init; }
    [JsonRequired] public int TotalPortions { get; init; }
    [JsonRequired] public int RemainingPortions { get; init; }
    [JsonRequired] public int Revision { get; init; }
    [JsonIgnore] public int Capacity => FoodCatalog.Capacity(Kind);
    public void Validate()
    {
        if (FurnitureId == Guid.Empty || ServingId == Guid.Empty || !Enum.IsDefined(Kind)
            || TotalPortions < 1 || TotalPortions > Capacity || RemainingPortions < 0
            || RemainingPortions > TotalPortions || Revision != TotalPortions - RemainingPortions)
            throw new InvalidDataException("食物批次或剩餘份量無效。");
    }
}

public sealed record FoodInventoryState
{
    public const int MaximumContainers = 24;
    [JsonRequired] public ImmutableArray<FoodServing> Servings { get; init; } = [];
    public FoodServing? Find(Guid furnitureId) => Servings.FirstOrDefault(s => s.FurnitureId == furnitureId);
    public void Validate()
    {
        if (Servings.IsDefault || Servings.Length > MaximumContainers || Servings.Any(s => s is null)
            || Servings.Select(s => s.FurnitureId).Distinct().Count() != Servings.Length
            || Servings.Select(s => s.ServingId).Distinct().Count() != Servings.Length)
            throw new InvalidDataException("食物庫存無效。");
        foreach (var serving in Servings) serving.Validate();
    }
    internal FoodInventoryState WithServing(FoodServing serving)
    {
        var index = -1;
        for (var i = 0; i < Servings.Length; i++) if (Servings[i].FurnitureId == serving.FurnitureId) { index = i; break; }
        return this with { Servings = index < 0 ? Servings.Add(serving) : Servings.SetItem(index, serving) };
    }
}

public sealed record FoodInventoryResult(FoodInventoryState Inventory, bool Accepted, FoodFailure Failure);
public sealed record FoodConsumptionResult(FoodInventoryState Inventory, PetState Pet, bool Accepted, FoodFailure Failure)
{
    public int ConsumedPortions => Accepted ? 1 : 0;
}

public static class FoodInventory
{
    public static FoodInventoryResult Add(FoodInventoryState inventory, IReadOnlyList<RoomItem> furniture,
        Guid furnitureId, FoodKind food, int portions) => Put(inventory, furniture, furnitureId, food, portions, replace: false);

    // Calling this method represents the user's explicit choice to remove the old dish.
    public static FoodInventoryResult Replace(FoodInventoryState inventory, IReadOnlyList<RoomItem> furniture,
        Guid furnitureId, FoodKind food, int portions) => Put(inventory, furniture, furnitureId, food, portions, replace: true);

    private static FoodInventoryResult Put(FoodInventoryState inventory, IReadOnlyList<RoomItem> furniture,
        Guid furnitureId, FoodKind food, int portions, bool replace)
    {
        inventory.Validate();
        FoodInventoryResult Reject(FoodFailure failure) => new(inventory, false, failure);
        if (furnitureId == Guid.Empty || !Enum.IsDefined(food) || portions < 1 || portions > FoodCatalog.Capacity(food))
            return Reject(FoodFailure.InvalidRequest);
        var item = furniture.FirstOrDefault(i => i.Id == furnitureId);
        if (item is null) return Reject(FoodFailure.MissingFurniture);
        if (!FoodCatalog.CanServe(item.Kind, food)) return Reject(FoodFailure.UnsupportedFurniture);
        var previous = inventory.Find(furnitureId);
        if (previous is null && inventory.Servings.Length >= FoodInventoryState.MaximumContainers)
            return Reject(FoodFailure.Full);
        if (!replace && previous is { RemainingPortions: > 0 })
        {
            if (previous.Kind != food) return Reject(FoodFailure.DifferentFood);
            if (previous.RemainingPortions == FoodCatalog.Capacity(food)) return Reject(FoodFailure.Full);
        }
        var amount = Math.Min(FoodCatalog.Capacity(food), portions + (!replace && previous?.Kind == food ? previous.RemainingPortions : 0));
        var serving = new FoodServing { FurnitureId = furnitureId, ServingId = Guid.NewGuid(), Kind = food,
            TotalPortions = amount, RemainingPortions = amount, Revision = 0 };
        return new(inventory.WithServing(serving), true, FoodFailure.None);
    }

    public static FoodInventoryResult Remove(FoodInventoryState inventory, Guid furnitureId)
    {
        inventory.Validate();
        if (furnitureId == Guid.Empty) return new(inventory, false, FoodFailure.InvalidRequest);
        if (inventory.Find(furnitureId) is not { } serving) return new(inventory, false, FoodFailure.NoFood);
        return new(inventory with { Servings = inventory.Servings.Remove(serving) }, true, FoodFailure.None);
    }
}

public sealed record FoodReservation(Guid Token, Guid FurnitureId, Guid ServingId, PetAppearance Resident);
public sealed record FoodReservationResult(FoodReservation? Reservation, bool Accepted, FoodFailure Failure);
public sealed record FoodContact(Guid ReservationToken, Guid FurnitureId, Guid ServingId,
    PetAppearance Resident, int ExpectedRevision, bool IsActualContact);

// Transient ownership is deliberately absent from the save. Use this on the scene's
// single owner thread, applying the returned inventory + PetState together before
// processing another contact. Disk persistence belongs to the combined organism save.
public sealed class FoodReservations
{
    private readonly Dictionary<Guid, FoodReservation> claims = [];
    public int ActiveCount => claims.Count;

    public FoodReservationResult Reserve(FoodInventoryState inventory, IReadOnlyList<RoomItem> furniture,
        IReadOnlyCollection<PetAppearance> residents, Guid furnitureId, Guid servingId, PetAppearance resident)
    {
        inventory.Validate();
        FoodReservationResult Reject(FoodFailure failure) => new(null, false, failure);
        var failure = CheckTarget(inventory, furniture, residents, furnitureId, servingId, resident, out var serving);
        if (failure != FoodFailure.None) return Reject(failure);
        Prune(inventory, furniture, residents);
        if (claims.TryGetValue(furnitureId, out var held))
            return held.Resident == resident ? new(held, true, FoodFailure.None) : Reject(FoodFailure.Reserved);
        var reservation = new FoodReservation(Guid.NewGuid(), furnitureId, serving!.ServingId, resident);
        claims.Add(furnitureId, reservation);
        return new(reservation, true, FoodFailure.None);
    }

    public FoodConsumptionResult Consume(FoodInventoryState inventory, PetState pet,
        IReadOnlyList<RoomItem> furniture, IReadOnlyCollection<PetAppearance> residents, FoodContact contact)
    {
        inventory.Validate(); pet.Validate();
        FoodConsumptionResult Reject(FoodFailure failure) => new(inventory, pet, false, failure);
        if (contact.ReservationToken == Guid.Empty || contact.ExpectedRevision < 0) return Reject(FoodFailure.InvalidRequest);
        var failure = CheckTarget(inventory, furniture, residents, contact.FurnitureId, contact.ServingId, contact.Resident, out var serving);
        if (failure != FoodFailure.None) return Reject(failure);
        if (!claims.TryGetValue(contact.FurnitureId, out var held) || held.Token != contact.ReservationToken
            || held.Resident != contact.Resident || held.ServingId != contact.ServingId)
            return Reject(FoodFailure.ReservationMismatch);
        if (contact.ExpectedRevision != serving!.Revision) return Reject(FoodFailure.StaleContact);
        if (!contact.IsActualContact) return Reject(FoodFailure.NoContact);
        if (pet.Hunger <= 0) return Reject(FoodFailure.NotHungry);
        var next = serving with { RemainingPortions = serving.RemainingPortions - 1, Revision = serving.Revision + 1 };
        var fed = pet with { Hunger = Math.Max(0, pet.Hunger - FoodCatalog.HungerPerPortion(serving.Kind)),
            Energy = Math.Min(100, pet.Energy + .5), Mood = Math.Min(100, pet.Mood + .25) };
        return new(inventory.WithServing(next), fed, true, FoodFailure.None);
    }

    public bool Release(FoodReservation reservation)
    {
        if (!claims.TryGetValue(reservation.FurnitureId, out var held) || held != reservation) return false;
        return claims.Remove(reservation.FurnitureId);
    }
    public void ReleaseResident(PetAppearance resident)
    {
        foreach (var id in claims.Where(p => p.Value.Resident == resident).Select(p => p.Key).ToArray()) claims.Remove(id);
    }
    public void Clear() => claims.Clear();

    // Removing furniture, refilling, swapping meals or hiding a resident makes old
    // claims unusable even before the UI has performed its cancellation cleanup.
    public void Prune(FoodInventoryState inventory, IReadOnlyList<RoomItem> furniture, IReadOnlyCollection<PetAppearance> residents)
    {
        inventory.Validate();
        foreach (var pair in claims.ToArray())
            if (CheckTarget(inventory, furniture, residents, pair.Key, pair.Value.ServingId, pair.Value.Resident, out _) != FoodFailure.None)
                claims.Remove(pair.Key);
    }

    private static FoodFailure CheckTarget(FoodInventoryState inventory, IReadOnlyList<RoomItem> furniture,
        IReadOnlyCollection<PetAppearance> residents, Guid furnitureId, Guid servingId, PetAppearance resident, out FoodServing? serving)
    {
        serving = null;
        if (furnitureId == Guid.Empty || servingId == Guid.Empty) return FoodFailure.InvalidRequest;
        if (!Enum.IsDefined(resident) || !residents.Contains(resident)) return FoodFailure.UnknownResident;
        var item = furniture.FirstOrDefault(i => i.Id == furnitureId);
        if (item is null) return FoodFailure.MissingFurniture;
        serving = inventory.Find(furnitureId);
        if (serving is null) return FoodFailure.NoFood;
        if (serving.ServingId != servingId) return FoodFailure.FoodChanged;
        if (!FoodCatalog.CanServe(item.Kind, serving.Kind)) return FoodFailure.UnsupportedFurniture;
        if (!FoodCatalog.CanEat(resident, serving.Kind)) return FoodFailure.WrongDiet;
        return serving.RemainingPortions == 0 ? FoodFailure.NoFood : FoodFailure.None;
    }
}
