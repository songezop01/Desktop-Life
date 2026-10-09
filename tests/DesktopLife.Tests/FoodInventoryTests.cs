using System.Text.Json;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class FoodInventoryTests
{
    private static readonly PetAppearance[] Residents = [PetAppearance.Cat, PetAppearance.BorderCollie, PetAppearance.Girl];
    private static RoomItem Bowl() => new(Guid.NewGuid(), FurnitureKind.CatBowl, 10, 20);
    private static RoomItem Table() => new(Guid.NewGuid(), FurnitureKind.DiningTable, 100, 20);
    private static FoodInventoryState Stock(RoomItem item, FoodKind kind = FoodKind.SharedKibble, int amount = 3)
    {
        var result = FoodInventory.Add(new(), [item], item.Id, kind, amount);
        Assert.True(result.Accepted); result.Inventory.Validate(); return result.Inventory;
    }
    private static FoodReservation Claim(FoodReservations reservations, FoodInventoryState inventory, RoomItem item, PetAppearance resident = PetAppearance.Cat)
    {
        var result = reservations.Reserve(inventory, [item], Residents, item.Id, inventory.Find(item.Id)!.ServingId, resident);
        Assert.True(result.Accepted); return Assert.IsType<FoodReservation>(result.Reservation);
    }
    private static FoodContact Contact(FoodReservation claim, FoodInventoryState inventory, bool actual = true) =>
        new(claim.Token, claim.FurnitureId, claim.ServingId, claim.Resident, inventory.Find(claim.FurnitureId)!.Revision, actual);
    private static void RejectedUnchanged(FoodConsumptionResult result, FoodInventoryState inventory, PetState pet, FoodFailure failure)
    {
        Assert.False(result.Accepted); Assert.Equal(failure, result.Failure);
        Assert.Same(inventory, result.Inventory); Assert.Same(pet, result.Pet); Assert.Equal(0, result.ConsumedPortions);
    }

    [Fact] public void AddFoodDoesNotFeedUntilARealContactAndTheLastPortionIsFinite()
    {
        var bowl = Bowl(); var inventory = Stock(bowl); var pet = new PetState { Hunger = 40 };
        var reservations = new FoodReservations(); var claim = Claim(reservations, inventory, bowl);
        RejectedUnchanged(reservations.Consume(inventory, pet, [bowl], Residents, Contact(claim, inventory, false)), inventory, pet, FoodFailure.NoContact);
        for (var remaining = 2; remaining >= 0; remaining--)
        {
            var result = reservations.Consume(inventory, pet, [bowl], Residents, Contact(claim, inventory));
            Assert.True(result.Accepted); Assert.Equal(1, result.ConsumedPortions);
            Assert.Equal(remaining, result.Inventory.Find(bowl.Id)!.RemainingPortions);
            Assert.Equal(pet.Hunger - 2, result.Pet.Hunger); result.Inventory.Validate(); result.Pet.Validate();
            inventory = result.Inventory; pet = result.Pet;
        }
        RejectedUnchanged(reservations.Consume(inventory, pet, [bowl], Residents, Contact(claim, inventory)), inventory, pet, FoodFailure.NoFood);
        Assert.Equal(34, pet.Hunger);
    }

    [Fact] public void DuplicateAndOutOfOrderContactCannotConsumeAgain()
    {
        var bowl = Bowl(); var original = Stock(bowl); var pet = new PetState { Hunger = 40 };
        var reservations = new FoodReservations(); var claim = Claim(reservations, original, bowl); var contact = Contact(claim, original);
        var first = reservations.Consume(original, pet, [bowl], Residents, contact); Assert.True(first.Accepted);
        RejectedUnchanged(reservations.Consume(first.Inventory, first.Pet, [bowl], Residents, contact), first.Inventory, first.Pet, FoodFailure.StaleContact);
        RejectedUnchanged(reservations.Consume(first.Inventory, first.Pet, [bowl], Residents, contact with { ExpectedRevision = 2 }), first.Inventory, first.Pet, FoodFailure.StaleContact);
        Assert.Equal(3, original.Find(bowl.Id)!.RemainingPortions); Assert.Equal(40, pet.Hunger);
    }

    [Fact] public void DurableRevisionRejectsReplayAfterSerializeAndNewRuntimeReservation()
    {
        var bowl = Bowl(); var inventory = Stock(bowl); var reservations = new FoodReservations(); var claim = Claim(reservations, inventory, bowl);
        var oldContact = Contact(claim, inventory); var result = reservations.Consume(inventory, new() { Hunger = 40 }, [bowl], Residents, oldContact);
        var restored = JsonSerializer.Deserialize<FoodInventoryState>(JsonSerializer.Serialize(result.Inventory))!; restored.Validate();
        var restoredPet = JsonSerializer.Deserialize<PetState>(JsonSerializer.Serialize(result.Pet))!;
        var restarted = new FoodReservations(); Assert.Equal(0, restarted.ActiveCount);
        RejectedUnchanged(restarted.Consume(restored, restoredPet, [bowl], Residents, oldContact), restored, restoredPet, FoodFailure.ReservationMismatch);
        var newClaim = Claim(restarted, restored, bowl);
        // Even rebinding an old event to the new runtime lease cannot erase its saved stock revision.
        RejectedUnchanged(restarted.Consume(restored, restoredPet, [bowl], Residents, oldContact with { ReservationToken = newClaim.Token }), restored, restoredPet, FoodFailure.StaleContact);
        Assert.True(restarted.Consume(restored, restoredPet, [bowl], Residents, Contact(newClaim, restored)).Accepted);
    }

    [Fact] public void SameBowlIsExclusiveAndAnOldReleaseCannotEvictTheNewOwner()
    {
        var bowl = Bowl(); var inventory = Stock(bowl); var reservations = new FoodReservations(); var cat = Claim(reservations, inventory, bowl);
        Assert.Equal(cat, Claim(reservations, inventory, bowl)); Assert.Equal(1, reservations.ActiveCount);
        var dogWaiting = reservations.Reserve(inventory, [bowl], Residents, bowl.Id, cat.ServingId, PetAppearance.BorderCollie);
        Assert.False(dogWaiting.Accepted); Assert.Equal(FoodFailure.Reserved, dogWaiting.Failure);
        Assert.True(reservations.Release(cat)); Assert.False(reservations.Release(cat));
        var dog = Claim(reservations, inventory, bowl, PetAppearance.BorderCollie); Assert.False(reservations.Release(cat));
        var pet = new PetState { Hunger = 30 };
        RejectedUnchanged(reservations.Consume(inventory, pet, [bowl], Residents, Contact(cat, inventory)), inventory, pet, FoodFailure.ReservationMismatch);
        Assert.True(reservations.Consume(inventory, pet, [bowl], Residents, Contact(dog, inventory)).Accepted);
    }

    [Fact] public void RefillInvalidatesQueuedContactAndPreservesOnlyRemainingFood()
    {
        var bowl = Bowl(); var inventory = Stock(bowl); var reservations = new FoodReservations(); var oldClaim = Claim(reservations, inventory, bowl);
        var consumed = reservations.Consume(inventory, new() { Hunger = 40 }, [bowl], Residents, Contact(oldClaim, inventory));
        var oldContact = Contact(oldClaim, consumed.Inventory); var refilled = FoodInventory.Add(consumed.Inventory, [bowl], bowl.Id, FoodKind.SharedKibble, 4);
        Assert.True(refilled.Accepted); Assert.Equal(6, refilled.Inventory.Find(bowl.Id)!.RemainingPortions);
        Assert.NotEqual(oldClaim.ServingId, refilled.Inventory.Find(bowl.Id)!.ServingId);
        RejectedUnchanged(reservations.Consume(refilled.Inventory, consumed.Pet, [bowl], Residents, oldContact), refilled.Inventory, consumed.Pet, FoodFailure.FoodChanged);
        var newClaim = Claim(reservations, refilled.Inventory, bowl, PetAppearance.BorderCollie);
        Assert.False(reservations.Release(oldClaim)); Assert.Equal(1, reservations.ActiveCount);
        Assert.True(reservations.Consume(refilled.Inventory, consumed.Pet, [bowl], Residents, Contact(newClaim, refilled.Inventory)).Accepted);
    }

    [Fact] public void ChangingFoodNeedsExplicitReplacementAndOldDishCannotBeConsumed()
    {
        var table = Table(); var inventory = Stock(table, FoodKind.Ramen); var reservations = new FoodReservations(); var claim = Claim(reservations, inventory, table, PetAppearance.Girl);
        var accidental = FoodInventory.Add(inventory, [table], table.Id, FoodKind.HotPot, 4);
        Assert.False(accidental.Accepted); Assert.Equal(FoodFailure.DifferentFood, accidental.Failure); Assert.Same(inventory, accidental.Inventory);
        var changed = FoodInventory.Replace(inventory, [table], table.Id, FoodKind.HotPot, 4); Assert.True(changed.Accepted);
        Assert.Equal(4, changed.Inventory.Find(table.Id)!.RemainingPortions); Assert.Equal(FoodKind.HotPot, changed.Inventory.Find(table.Id)!.Kind);
        var pet = new PetState { Hunger = 40 };
        RejectedUnchanged(reservations.Consume(changed.Inventory, pet, [table], Residents, Contact(claim, inventory)), changed.Inventory, pet, FoodFailure.FoodChanged);
    }

    [Theory]
    [InlineData(FoodKind.BurgerMeal)] [InlineData(FoodKind.FriedChickenMeal)] [InlineData(FoodKind.HotPot)]
    [InlineData(FoodKind.Ramen)] [InlineData(FoodKind.ChickenCutlet)]
    public void FiveOriginalMealThemesAreOnlyForGirlsAtDiningTables(FoodKind meal)
    {
        var table = Table(); var inventory = Stock(table, meal); var reservations = new FoodReservations();
        var wrongActor = reservations.Reserve(inventory, [table], Residents, table.Id, inventory.Find(table.Id)!.ServingId, PetAppearance.Cat);
        Assert.False(wrongActor.Accepted); Assert.Equal(FoodFailure.WrongDiet, wrongActor.Failure);
        var girl = Claim(reservations, inventory, table, PetAppearance.Girl);
        var result = reservations.Consume(inventory, new() { Hunger = 40 }, [table], Residents, Contact(girl, inventory));
        Assert.True(result.Accepted); Assert.Equal(37.5, result.Pet.Hunger);
        var bowl = Bowl(); var unsupported = FoodInventory.Add(new(), [bowl], bowl.Id, meal, 3);
        Assert.False(unsupported.Accepted); Assert.Equal(FoodFailure.UnsupportedFurniture, unsupported.Failure);
    }

    [Theory]
    [InlineData(FoodKind.CatKibble, PetAppearance.Cat, true)] [InlineData(FoodKind.CatKibble, PetAppearance.BorderCollie, false)]
    [InlineData(FoodKind.DogKibble, PetAppearance.BorderCollie, true)] [InlineData(FoodKind.DogKibble, PetAppearance.Cat, false)]
    [InlineData(FoodKind.SharedKibble, PetAppearance.Cat, true)] [InlineData(FoodKind.SharedKibble, PetAppearance.BorderCollie, true)]
    [InlineData(FoodKind.SharedKibble, PetAppearance.Girl, false)]
    public void PetDietsRemainExplicit(FoodKind food, PetAppearance actor, bool allowed)
    {
        var bowl = Bowl(); var inventory = Stock(bowl, food); var reservations = new FoodReservations();
        var result = reservations.Reserve(inventory, [bowl], Residents, bowl.Id, inventory.Find(bowl.Id)!.ServingId, actor);
        Assert.Equal(allowed, result.Accepted); Assert.Equal(allowed ? FoodFailure.None : FoodFailure.WrongDiet, result.Failure);
        Assert.Equal(3, inventory.Find(bowl.Id)!.RemainingPortions);
    }

    [Fact] public void MissingChangedAndRemovedFurniturePreventConsumption()
    {
        var bowl = Bowl(); var inventory = Stock(bowl); var reservations = new FoodReservations(); var claim = Claim(reservations, inventory, bowl);
        var pet = new PetState { Hunger = 40 }; var contact = Contact(claim, inventory);
        RejectedUnchanged(reservations.Consume(inventory, pet, [], Residents, contact), inventory, pet, FoodFailure.MissingFurniture);
        RejectedUnchanged(reservations.Consume(inventory, pet, [bowl with { Kind = FurnitureKind.DiningTable }], Residents, contact), inventory, pet, FoodFailure.UnsupportedFurniture);
        var removed = FoodInventory.Remove(inventory, bowl.Id); Assert.True(removed.Accepted);
        RejectedUnchanged(reservations.Consume(removed.Inventory, pet, [bowl], Residents, contact), removed.Inventory, pet, FoodFailure.NoFood);
        reservations.Prune(removed.Inventory, [], Residents); Assert.Equal(0, reservations.ActiveCount);
    }

    [Fact] public void HiddenUnknownAndForgedResidentsCannotUseAnExistingClaim()
    {
        var bowl = Bowl(); var inventory = Stock(bowl); var reservations = new FoodReservations(); var claim = Claim(reservations, inventory, bowl);
        var pet = new PetState { Hunger = 40 }; var contact = Contact(claim, inventory);
        RejectedUnchanged(reservations.Consume(inventory, pet, [bowl], [PetAppearance.BorderCollie], contact), inventory, pet, FoodFailure.UnknownResident);
        RejectedUnchanged(reservations.Consume(inventory, pet, [bowl], Residents, contact with { Resident = (PetAppearance)999 }), inventory, pet, FoodFailure.UnknownResident);
        RejectedUnchanged(reservations.Consume(inventory, pet, [bowl], Residents, contact with { Resident = PetAppearance.BorderCollie }), inventory, pet, FoodFailure.ReservationMismatch);
        RejectedUnchanged(reservations.Consume(inventory, pet, [bowl], Residents, contact with { FurnitureId = Guid.NewGuid() }), inventory, pet, FoodFailure.MissingFurniture);
        reservations.Prune(inventory, [bowl], [PetAppearance.BorderCollie]); Assert.Equal(0, reservations.ActiveCount);
    }

    [Fact] public void FullAndInvalidAmountsNeverOverflowOrReplaceTheCurrentServing()
    {
        var bowl = Bowl(); var inventory = Stock(bowl, amount: 59);
        var filled = FoodInventory.Add(inventory, [bowl], bowl.Id, FoodKind.SharedKibble, 10); Assert.True(filled.Accepted);
        Assert.Equal(60, filled.Inventory.Find(bowl.Id)!.RemainingPortions);
        var full = FoodInventory.Add(filled.Inventory, [bowl], bowl.Id, FoodKind.SharedKibble, 1);
        Assert.False(full.Accepted); Assert.Equal(FoodFailure.Full, full.Failure); Assert.Same(filled.Inventory, full.Inventory);
        foreach (var amount in new[] { int.MinValue, -1, 0, 61, int.MaxValue })
        {
            var invalid = FoodInventory.Add(inventory, [bowl], bowl.Id, FoodKind.SharedKibble, amount);
            Assert.False(invalid.Accepted); Assert.Equal(FoodFailure.InvalidRequest, invalid.Failure); Assert.Same(inventory, invalid.Inventory);
        }
        Assert.False(FoodInventory.Add(inventory, [bowl], bowl.Id, (FoodKind)999, 1).Accepted);
    }

    [Fact] public void SatietyAndMaximumNeedsDoNotWasteFoodOrExceedBounds()
    {
        var bowl = Bowl(); var inventory = Stock(bowl); var reservations = new FoodReservations(); var claim = Claim(reservations, inventory, bowl);
        var full = new PetState { Hunger = 0, Energy = 100, Mood = 100 };
        RejectedUnchanged(reservations.Consume(inventory, full, [bowl], Residents, Contact(claim, inventory)), inventory, full, FoodFailure.NotHungry);
        var result = reservations.Consume(inventory, full with { Hunger = 1 }, [bowl], Residents, Contact(claim, inventory));
        Assert.True(result.Accepted); Assert.Equal(0, result.Pet.Hunger); Assert.Equal(100, result.Pet.Energy); Assert.Equal(100, result.Pet.Mood); result.Pet.Validate();
    }

    [Fact] public void OfflineNeedsDoNotDepleteSavedFood()
    {
        var bowl = Bowl(); var inventory = Stock(bowl); var json = JsonSerializer.Serialize(inventory);
        var life = new HomeostasisSession(new()); life.ApplyCompanionOffline(TimeSpan.FromDays(7));
        Assert.Equal(json, JsonSerializer.Serialize(inventory)); Assert.Equal(3, inventory.Find(bowl.Id)!.RemainingPortions);
    }

    [Fact] public void FoodSaveRejectsMissingFieldsDuplicateBindingsAndImpossibleAmounts()
    {
        var bowl = Bowl(); var inventory = Stock(bowl); var serving = inventory.Find(bowl.Id)!;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FoodInventoryState>("{}"));
        Assert.Throws<InvalidDataException>(() => (inventory with { Servings = [serving, serving] }).Validate());
        foreach (var broken in new[] { serving with { RemainingPortions = -1 }, serving with { RemainingPortions = 4 },
            serving with { TotalPortions = 61, RemainingPortions = 61 }, serving with { Revision = 1 },
            serving with { FurnitureId = Guid.Empty }, serving with { ServingId = Guid.Empty }, serving with { Kind = (FoodKind)999 } })
            Assert.Throws<InvalidDataException>(() => broken.Validate());
    }

    [Fact] public void RepeatedRemoveAndRefillCannotGrowRuntimeClaimsWithoutBounds()
    {
        var reservations = new FoodReservations(); var inventory = new FoodInventoryState();
        for (var i = 0; i < 100; i++)
        {
            var bowl = Bowl(); inventory = Stock(bowl); Claim(reservations, inventory, bowl);
            Assert.Equal(1, reservations.ActiveCount);
        }
        reservations.ReleaseResident(PetAppearance.Cat); Assert.Equal(0, reservations.ActiveCount);
        var item = Bowl(); inventory = Stock(item); Claim(reservations, inventory, item); reservations.Clear(); Assert.Equal(0, reservations.ActiveCount);
    }
}
