using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public sealed class FoodPersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "DesktopLifeFoodPersistence", Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Epoch = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
    private static readonly PetAppearance[] Residents = [PetAppearance.Cat, PetAppearance.Girl, PetAppearance.BorderCollie];

    private static OrganismSnapshot House(bool withFood = true)
    {
        var cat = CharacterProfile.Create(PetAppearance.Cat, Epoch) with { Pet = new() { LastSaveTime = Epoch, State = new() { Hunger = 55 } } };
        var girl = CharacterProfile.Create(PetAppearance.Girl, Epoch) with { Position = new(250, 500), Pet = new() { LastSaveTime = Epoch, State = new() { Hunger = 65 } } };
        var dog = CharacterProfile.Create(PetAppearance.BorderCollie, Epoch) with { Position = new(450, 800) };
        foreach (var profile in new[] { cat, girl, dog })
        {
            profile.Learning.Companion.Remember(Epoch, profile.Kind + " 原有的記憶");
            profile.Learning.Companion.Bond = 75; profile.Learning.PositiveRewards = 42;
        }
        cat.Learning.Room = new() { FloorCount = 3, WorkArea = new(0, 0, 1920, 1080), Items =
            [new(Guid.NewGuid(), FurnitureKind.CatBowl, 10, 20), new(Guid.NewGuid(), FurnitureKind.DiningTable, 250, 500, 1), new(Guid.NewGuid(), FurnitureKind.PetBed, 450, 800, 2)] };
        var snapshot = new OrganismSnapshot { Pet = cat.Pet, Learning = cat.Learning, Personality = cat.Personality,
            Settings = new() { PetAppearance = PetAppearance.Cat, Presence = PresenceMode.All }, OtherCharacter = girl, AdditionalCharacter = dog, PrimaryPosition = new(10, 20) };
        if (withFood)
        {
            var bowl = snapshot.Learning.Room.Items[0]; var table = snapshot.Learning.Room.Items[1];
            var bowlFood = FoodInventory.Add(snapshot.Food, snapshot.Learning.Room.Items, bowl.Id, FoodKind.SharedKibble, 6);
            var meal = FoodInventory.Add(bowlFood.Inventory, snapshot.Learning.Room.Items, table.Id, FoodKind.Ramen, 4);
            Assert.True(bowlFood.Accepted); Assert.True(meal.Accepted); snapshot = snapshot with { Food = meal.Inventory };
        }
        snapshot.Validate(); return snapshot;
    }

    private static FoodReservation Reserve(OrganismSnapshot snapshot, FoodReservations claims, int item, PetAppearance actor)
    {
        var furniture = snapshot.Learning.Room.Items[item]; var serving = snapshot.Food.Find(furniture.Id)!;
        var result = claims.Reserve(snapshot.Food, snapshot.Learning.Room.Items, Residents, furniture.Id, serving.ServingId, actor);
        Assert.True(result.Accepted); return result.Reservation!;
    }
    private static FoodContact Contact(OrganismSnapshot snapshot, FoodReservation claim) => new(claim.Token, claim.FurnitureId,
        claim.ServingId, claim.Resident, snapshot.Food.Find(claim.FurnitureId)!.Revision, true);
    private static OrganismSnapshot Feed(OrganismSnapshot snapshot, FoodReservations claims, int item, PetAppearance actor)
    {
        var claim = Reserve(snapshot, claims, item, actor);
        var result = claims.Consume(snapshot.Food, snapshot.GetCharacter(actor)!.Pet.State, snapshot.Learning.Room.Items, Residents, Contact(snapshot, claim));
        Assert.True(result.Accepted);
        if (actor == snapshot.Settings.PetAppearance) return snapshot with { Food = result.Inventory, Pet = snapshot.Pet with { State = result.Pet } };
        if (snapshot.OtherCharacter?.Kind == actor) return snapshot with { Food = result.Inventory, OtherCharacter = snapshot.OtherCharacter with { Pet = snapshot.OtherCharacter.Pet with { State = result.Pet } } };
        throw new InvalidOperationException("Unsupported test resident.");
    }
    private static string LegacyJson(OrganismSnapshot snapshot, int schema)
    {
        var node = JsonSerializer.SerializeToNode(snapshot)!.AsObject(); node["SchemaVersion"] = schema; node.Remove("Food"); return node.ToJsonString();
    }

    [Fact] public void ActualBitesSaveFoodAndBothResidentsSatietyInOneAtomicSnapshot()
    {
        var original = House(); var claims = new FoodReservations();
        var consumed = Feed(Feed(original, claims, 0, PetAppearance.Cat), claims, 1, PetAppearance.Girl);
        var store = new OrganismStore(directory); store.Save(original); FrozenOrganismSnapshot.Capture(consumed).SaveTo(store);
        var restored = store.Load(); Assert.Equal(8, restored.SchemaVersion);
        Assert.Equal(5, restored.Food.Find(original.Learning.Room.Items[0].Id)!.RemainingPortions);
        Assert.Equal(3, restored.Food.Find(original.Learning.Room.Items[1].Id)!.RemainingPortions);
        Assert.Equal(53, restored.Pet.State.Hunger); Assert.Equal(62.5, restored.OtherCharacter!.Pet.State.Hunger);
        Assert.Equal(JsonSerializer.Serialize(consumed), JsonSerializer.Serialize(restored));
        Assert.Equal(6, original.Food.Servings[0].RemainingPortions); Assert.Equal(55, original.Pet.State.Hunger); Assert.Equal(65, original.OtherCharacter!.Pet.State.Hunger);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp-*"));
    }

    [Fact] public void FailedAtomicWriteLeavesThePreviousFoodAndHungerPairThenRetriesOnce()
    {
        var original = House(); var store = new OrganismStore(directory); store.Save(original); var before = File.ReadAllText(store.PathName);
        var consumed = Feed(original, new(), 0, PetAppearance.Cat); var frozen = FrozenOrganismSnapshot.Capture(consumed);
        using (var locked = new FileStream(store.PathName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<IOException>(() => frozen.SaveTo(store)); Assert.Equal(before, File.ReadAllText(store.PathName));
        }
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(store.Load()));
        frozen.SaveTo(store); Assert.Equal(JsonSerializer.Serialize(consumed), JsonSerializer.Serialize(store.Load()));
        Assert.Equal(53, store.Load().Pet.State.Hunger); Assert.Equal(5, store.Load().Food.Servings[0].RemainingPortions);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp-*"));
    }

    [Theory] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void EverySupportedOldSchemaMigratesToEmptyFoodAndPreservesTheEntireHouse(int schema)
    {
        var expected = House(withFood: false); var old = LegacyJson(expected, schema); var store = new OrganismStore(directory);
        Directory.CreateDirectory(directory); File.WriteAllText(store.PathName, old);
        var migrated = store.Load(); Assert.Equal(8, migrated.SchemaVersion); Assert.Empty(migrated.Food.Servings);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(migrated)); Assert.Equal(old, File.ReadAllText(store.PathName));
        store.Save(migrated); Assert.Equal(old, File.ReadAllText(store.PathName + ".bak"));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(store.Load()));
        Assert.Equal(3, store.Load().Learning.Room.FloorCount); Assert.Equal(3, store.Load().Learning.Room.Items.Count);
        Assert.Single(store.Load().Learning.Companion.Memories); Assert.Single(store.Load().OtherCharacter!.Learning.Companion.Memories);
        Assert.Single(store.Load().AdditionalCharacter!.Learning.Companion.Memories);
    }

    [Fact] public void OldBackupRecoversWithoutCreatingFoodAndPreservesDamagedPrimary()
    {
        var expected = House(withFood: false); var oldBackup = LegacyJson(expected, 7); var store = new OrganismStore(directory);
        Directory.CreateDirectory(directory); File.WriteAllText(store.PathName, "{broken"); File.WriteAllText(store.PathName + ".bak", oldBackup);
        var recovered = store.Load(); Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(recovered));
        Assert.Empty(recovered.Food.Servings); Assert.Equal(oldBackup, File.ReadAllText(store.PathName + ".bak"));
        Assert.Equal("{broken", File.ReadAllText(Directory.GetFiles(directory, "*.damaged-*").Single()));
        Assert.NotNull(store.RecoveryMessage); Assert.Equal(8, store.Load().SchemaVersion);
    }

    [Theory]
    [InlineData("missing")] [InlineData("null")] [InlineData("missing-servings")] [InlineData("negative")]
    [InlineData("revision")] [InlineData("unknown-furniture")] [InlineData("wrong-furniture")] [InlineData("duplicate")]
    public void CurrentSchemaCorruptFoodIsRejectedWithoutResettingOrOverwriting(string corruption)
    {
        var snapshot = House(); var node = JsonSerializer.SerializeToNode(snapshot)!.AsObject();
        var food = node["Food"]!.AsObject(); var servings = food["Servings"]!.AsArray();
        switch (corruption)
        {
            case "missing": node.Remove("Food"); break;
            case "null": node["Food"] = null; break;
            case "missing-servings": food.Remove("Servings"); break;
            case "negative": servings[0]!["RemainingPortions"] = -1; break;
            case "revision": servings[0]!["Revision"] = 999; break;
            case "unknown-furniture": servings[0]!["FurnitureId"] = Guid.NewGuid(); break;
            case "wrong-furniture": servings[0]!["FurnitureId"] = snapshot.Learning.Room.Items[2].Id; break;
            case "duplicate": servings.Add(servings[0]!.DeepClone()); break;
        }
        var broken = node.ToJsonString(); var store = new OrganismStore(directory); Directory.CreateDirectory(directory); File.WriteAllText(store.PathName, broken);
        Assert.Throws<InvalidDataException>(() => store.Load()); Assert.Equal(broken, File.ReadAllText(store.PathName)); Assert.Null(store.RecoveryMessage);
        Assert.ThrowsAny<Exception>(() => store.StageImport(store.PathName)); Assert.False(File.Exists(Path.Combine(directory, "organism.import.json")));
    }

    [Fact] public void MissingFoodInCurrentSchemaRecoversTheValidBackupInventoryInsteadOfEmptyDefault()
    {
        var snapshot = House(); var store = new OrganismStore(directory); store.Save(snapshot); store.Save(snapshot);
        var broken = JsonSerializer.SerializeToNode(snapshot)!.AsObject(); broken.Remove("Food"); File.WriteAllText(store.PathName, broken.ToJsonString());
        var recovered = store.Load(); Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(recovered));
        Assert.Equal(6, recovered.Food.Servings[0].RemainingPortions); Assert.NotNull(store.RecoveryMessage);
        Assert.Contains("\"SchemaVersion\":8", File.ReadAllText(Directory.GetFiles(directory, "*.damaged-*").Single()));
    }

    [Fact] public void SecondaryRoomCannotAuthorizeFoodOrphansAndInvalidSaveKeepsExistingFile()
    {
        var snapshot = House(); var secondaryTable = new RoomItem(Guid.NewGuid(), FurnitureKind.DiningTable, 10, 20);
        snapshot.OtherCharacter!.Learning.Room.Items.Add(secondaryTable);
        var orphan = FoodInventory.Add(snapshot.Food, snapshot.OtherCharacter.Learning.Room.Items, secondaryTable.Id, FoodKind.HotPot, 4).Inventory;
        var invalid = snapshot with { Food = orphan }; Assert.Throws<InvalidDataException>(() => invalid.Validate());
        var store = new OrganismStore(directory); store.Save(snapshot); var before = File.ReadAllText(store.PathName);
        Assert.Throws<InvalidDataException>(() => store.Save(invalid)); Assert.Equal(before, File.ReadAllText(store.PathName));
    }

    [Fact] public void FutureSchemaCannotRecoverOrSaveOverFoodEvenWithOldBackups()
    {
        var snapshot = House(); var store = new OrganismStore(directory); store.Save(snapshot); store.Save(snapshot); var oldBackup = File.ReadAllText(store.PathName + ".bak");
        var future = JsonSerializer.SerializeToNode(snapshot)!.AsObject(); future["SchemaVersion"] = 9; var json = future.ToJsonString(); File.WriteAllText(store.PathName, json);
        Assert.Throws<NotSupportedException>(() => store.Load()); Assert.Throws<NotSupportedException>(() => store.Save(snapshot));
        Assert.Throws<NotSupportedException>(() => store.StageImport(store.PathName)); Assert.Equal(json, File.ReadAllText(store.PathName));
        Assert.Equal(oldBackup, File.ReadAllText(store.PathName + ".bak")); Assert.Empty(Directory.GetFiles(directory, "*.damaged-*"));
    }

    [Fact] public void FrozenCaptureRetainsTheConsumedPairWhenLiveHouseAndLaterFoodChange()
    {
        var consumed = Feed(House(), new(), 0, PetAppearance.Cat); var expected = JsonSerializer.Serialize(consumed); var frozen = FrozenOrganismSnapshot.Capture(consumed);
        var bowl = consumed.Learning.Room.Items[0]; var refilled = FoodInventory.Add(consumed.Food, consumed.Learning.Room.Items, bowl.Id, FoodKind.SharedKibble, 10);
        var live = consumed with { Food = refilled.Inventory, Pet = consumed.Pet with { State = consumed.Pet.State with { Hunger = 99 } } };
        live.Learning.Room.Items.Clear(); live.Learning.Companion.Memories.Clear();
        var store = new OrganismStore(directory); frozen.SaveTo(store);
        Assert.Equal(expected, JsonSerializer.Serialize(store.Load())); Assert.Equal(53, store.Load().Pet.State.Hunger);
        Assert.Equal(5, store.Load().Food.Find(bowl.Id)!.RemainingPortions); Assert.Equal(3, store.Load().Learning.Room.Items.Count);
    }

    [Fact] public void OfflineNeedsKeepInventoryAndReservationsAreNeverPersisted()
    {
        var snapshot = House(); var claims = new FoodReservations(); var claim = Reserve(snapshot, claims, 0, PetAppearance.Cat); var oldContact = Contact(snapshot, claim);
        var life = new HomeostasisSession(snapshot.Pet.State); life.ApplyCompanionOffline(TimeSpan.FromDays(7));
        var offline = snapshot with { Pet = snapshot.Pet with { State = life.State } }; var store = new OrganismStore(directory); store.Save(offline); var json = File.ReadAllText(store.PathName);
        Assert.DoesNotContain(claim.Token.ToString(), json); Assert.DoesNotContain("Reservation", json);
        var restored = store.Load(); Assert.Equal(JsonSerializer.Serialize(snapshot.Food), JsonSerializer.Serialize(restored.Food));
        var restarted = new FoodReservations(); Assert.Equal(0, restarted.ActiveCount);
        var stale = restarted.Consume(restored.Food, restored.Pet.State, restored.Learning.Room.Items, Residents, oldContact);
        Assert.False(stale.Accepted); Assert.Equal(FoodFailure.ReservationMismatch, stale.Failure); Assert.Same(restored.Food, stale.Inventory);
    }

    [Fact] public void ConsumedRevisionSurvivesWholeSaveReloadAndRejectsReplayedBite()
    {
        var original = House(); var claims = new FoodReservations(); var oldClaim = Reserve(original, claims, 0, PetAppearance.Cat); var contact = Contact(original, oldClaim);
        var consumed = Feed(original, claims, 0, PetAppearance.Cat); var store = new OrganismStore(directory); store.Save(consumed); var restored = store.Load();
        var restarted = new FoodReservations(); var newClaim = Reserve(restored, restarted, 0, PetAppearance.Cat);
        var replay = restarted.Consume(restored.Food, restored.Pet.State, restored.Learning.Room.Items, Residents, contact with { ReservationToken = newClaim.Token });
        Assert.False(replay.Accepted); Assert.Equal(FoodFailure.StaleContact, replay.Failure); Assert.Same(restored.Food, replay.Inventory); Assert.Same(restored.Pet.State, replay.Pet);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
