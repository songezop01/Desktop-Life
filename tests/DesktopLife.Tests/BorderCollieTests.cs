using System.Text.Json;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class BorderCollieTests
{
    private static readonly DateTimeOffset Epoch=new(2026,10,1,0,0,0,TimeSpan.Zero);

    [Fact]
    public void ExistingSavedEnumValuesAndDualPresenceKeepTheirMeaning()
    {
        Assert.Equal(0,(int)PetAppearance.Girl);
        Assert.Equal(1,(int)PetAppearance.Cat);
        Assert.Equal(2,(int)PetAppearance.BorderCollie);
        Assert.Equal(0,(int)PresenceMode.CatOnly);
        Assert.Equal(1,(int)PresenceMode.GirlOnly);
        Assert.Equal(2,(int)PresenceMode.Both);
        var old=JsonSerializer.Deserialize<AppSettings>("{\"PetAppearance\":1,\"Presence\":2}")!;
        old.Validate();
        Assert.True(PresencePolicy.Includes(old.Presence!.Value,PetAppearance.Cat));
        Assert.True(PresencePolicy.Includes(old.Presence.Value,PetAppearance.Girl));
        Assert.False(PresencePolicy.Includes(old.Presence.Value,PetAppearance.BorderCollie));
        Assert.False(PresencePolicy.Includes(PresenceMode.All,(PetAppearance)99));
    }

    [Theory]
    [InlineData(PresenceMode.DogOnly,false,false,true)]
    [InlineData(PresenceMode.All,true,true,true)]
    public void PresenceCanSelectDogOrEntireHousehold(PresenceMode mode,bool cat,bool girl,bool dog)
    {
        Assert.Equal(cat,PresencePolicy.Includes(mode,PetAppearance.Cat));
        Assert.Equal(girl,PresencePolicy.Includes(mode,PetAppearance.Girl));
        Assert.Equal(dog,PresencePolicy.Includes(mode,PetAppearance.BorderCollie));
        Assert.Equal(PresenceMode.DogOnly,PresencePolicy.Only(PetAppearance.BorderCollie));
    }

    [Fact]
    public void NewDogStartsWithItsOwnNamePersonalityAndHistory()
    {
        var cat=CharacterProfile.Create(PetAppearance.Cat,Epoch);
        cat.Learning.Companion.Bond=90;
        cat.Learning.Companion.Remember(Epoch,"原有角色的記憶");
        cat.Learning.ActionPreference[BodyAction.DrawDoodle]=.75;
        var dog=CharacterProfile.Create(PetAppearance.BorderCollie,Epoch.AddHours(1));
        dog.Validate();
        Assert.Equal("邊牧",dog.Learning.Companion.Name);
        Assert.Equal(15,dog.Learning.Companion.Bond);
        Assert.Equal(Epoch.AddHours(1),dog.Pet.LastSaveTime);
        Assert.Equal(0,dog.Personality.Creativity);
        Assert.True(dog.Personality.Playfulness>cat.Personality.Playfulness);
        Assert.Empty(dog.Learning.Companion.Memories);
        Assert.Empty(dog.Learning.ActionPreference);
        CompanionCare.Apply(dog.Pet.State,dog.Learning.Companion,CareKind.Pet,Epoch);
        Assert.Equal(90,cat.Learning.Companion.Bond);
        Assert.Single(cat.Learning.Companion.Memories);
        Assert.True(dog.Learning.Companion.Bond>15);
    }

    [Fact]
    public void DogExpressionsAndSelectorRejectHumanAndFelineBehavior()
    {
        foreach(var expression in new[]{CharacterExpression.Speak,CharacterExpression.WriteNote,CharacterExpression.DrawDoodle,
            CharacterExpression.Purr,CharacterExpression.Scratch,CharacterExpression.Knead,CharacterExpression.LickPaw,CharacterExpression.HuntCrouch})
            Assert.False(CharacterCapability.Allows(PetAppearance.BorderCollie,expression));
        foreach(var expression in new[]{CharacterExpression.Bark,CharacterExpression.WagTail,CharacterExpression.Sniff})
        {
            Assert.True(CharacterCapability.Allows(PetAppearance.BorderCollie,expression));
            Assert.False(CharacterCapability.Allows(PetAppearance.Cat,expression));
            Assert.False(CharacterCapability.Allows(PetAppearance.Girl,expression));
        }
        var learning=new RewardLearning();
        learning.State.ActionPreference[BodyAction.WriteNote]=.75;
        learning.State.ActionPreference[BodyAction.DrawDoodle]=.75;
        var context=new EnvironmentContext(new(),new(){Creativity=1},null,LearningContext.QuietDesktop,
            Affordances:new(true,true,true,true),Appearance:PetAppearance.BorderCollie,Now:Epoch);
        var selector=new ActionSelection(7);
        var brain=new UtilityBrain().Evaluate(context);
        Assert.DoesNotContain(selector.Evaluate(context,brain,learning),a=>a.Action is BodyAction.WriteNote or BodyAction.DrawDoodle or BodyAction.BatToy);
        Assert.Equal(BodyAction.Explore,selector.Select(context,brain,learning,BodyAction.DrawDoodle,true).Action);
        Assert.Equal(BodyAction.PlayToy,CharacterCapability.Resolve(PetAppearance.BorderCollie,BodyAction.BatToy));
    }

    [Theory]
    [InlineData(FurnitureKind.PetBed,FurnitureUse.Sleep)]
    [InlineData(FurnitureKind.Cushion,FurnitureUse.Rest)]
    [InlineData(FurnitureKind.Sofa,FurnitureUse.Sleep)]
    [InlineData(FurnitureKind.HumanBed,FurnitureUse.Rest)]
    [InlineData(FurnitureKind.CatBowl,FurnitureUse.Eat)]
    [InlineData(FurnitureKind.BellBall,FurnitureUse.Play)]
    public void DogCanUseAppropriatePetFurniture(FurnitureKind furniture,FurnitureUse use)
        =>Assert.True(FurnitureCompatibility.CanUse(PetAppearance.BorderCollie,furniture,use));

    [Fact]
    public void DogCannotChooseCatPerchesScratchingOrHumanActivities()
    {
        foreach(var furniture in new[]{FurnitureKind.CatTree,FurnitureKind.Bookshelf,FurnitureKind.Desk,FurnitureKind.DiningTable,
            FurnitureKind.Scratcher,FurnitureKind.Box,FurnitureKind.Yarn,FurnitureKind.ToyMouse,FurnitureKind.Computer,
            FurnitureKind.DrawingBook,FurnitureKind.LegoBox,FurnitureKind.Slide})
            Assert.Equal(FurnitureUse.None,FurnitureCompatibility.AvailableUses(PetAppearance.BorderCollie,furniture));
        foreach(var furniture in Enum.GetValues<FurnitureKind>())
            Assert.Equal(FurnitureUse.None,FurnitureCompatibility.AvailableUses(PetAppearance.BorderCollie,furniture)&
                (FurnitureUse.Scratch|FurnitureUse.Hide|FurnitureUse.HairCare|FurnitureUse.Draw|FurnitureUse.Write|FurnitureUse.Compute|FurnitureUse.Build|FurnitureUse.Read));
        Assert.True(FurnitureCompatibility.Priority(PetAppearance.BorderCollie,FurnitureKind.PetBed,FurnitureUse.Sleep)<
            FurnitureCompatibility.Priority(PetAppearance.BorderCollie,FurnitureKind.HumanBed,FurnitureUse.Sleep));
        Assert.True(RoomActivityPolicy.Allowed(PetAppearance.BorderCollie,RoomActivity.Feed));
        Assert.True(RoomActivityPolicy.Allowed(PetAppearance.BorderCollie,RoomActivity.Rest));
        foreach(var activity in new[]{RoomActivity.HairCare,RoomActivity.Draw,RoomActivity.Write,RoomActivity.Computer,RoomActivity.Lego,RoomActivity.Read})
            Assert.False(RoomActivityPolicy.Allowed(PetAppearance.BorderCollie,activity));
    }

    [Theory]
    [InlineData(SequenceKind.Sleep,0)]
    [InlineData(SequenceKind.Sleep,1)]
    [InlineData(SequenceKind.Sleep,2)]
    [InlineData(SequenceKind.SelfGroom,0)]
    [InlineData(SequenceKind.Play,0)]
    public void DogSequencesCompleteWithoutFelineChoreography(SequenceKind kind,int wakeVariant)
    {
        var sequence=new BehaviorSequence(kind,new(),60,locationKind:FurnitureKind.PetBed,wakeVariant:wakeVariant,character:PetAppearance.BorderCollie);
        var phases=new HashSet<BehaviorPhase>();
        for(var i=0;i<1200&&!sequence.Finished;i++)
        {
            phases.Add(sequence.Phase);
            sequence.Step(.1,new(Reached:true,Grounded:true,Rested:true,Contact:true,TargetSpeed:100));
        }
        Assert.True(sequence.Finished);
        Assert.Equal(0,sequence.Style.PurrChance);
        foreach(var phase in new[]{BehaviorPhase.Scratch,BehaviorPhase.Knead,BehaviorPhase.LickPaw,BehaviorPhase.WashFace,BehaviorPhase.Crouch})
            Assert.DoesNotContain(phase,phases);
        if(kind==SequenceKind.Play)Assert.True(sequence.Contacts>0);
        if(kind==SequenceKind.Sleep)Assert.Contains(BehaviorPhase.Sleep,phases);
    }

    [Fact]
    public void DogCannotStartFelineOnlySequencesEvenWhenRequestedDirectly()
    {
        Assert.Throws<ArgumentException>(()=>new BehaviorSequence(SequenceKind.Scratch,new(),60,character:PetAppearance.BorderCollie));
        Assert.Throws<ArgumentException>(()=>new BehaviorSequence(SequenceKind.Box,new(),60,character:PetAppearance.BorderCollie));
        Assert.Throws<ArgumentException>(()=>new BehaviorSequence(SequenceKind.Activity,new(),60,character:PetAppearance.BorderCollie,activity:RoomActivity.Draw));
    }

    [Fact]
    public void ThreeResidentsHaveIndependentReservationsAndRelease()
    {
        var sofa=new RoomItem(Guid.NewGuid(),FurnitureKind.Sofa,0,0);
        var world=new HouseholdOccupancy();
        var residents=Enum.GetValues<PetAppearance>();
        var reservations=residents.Select(kind=>FurnitureCompatibility.Reservation(sofa,kind)).ToArray();
        Assert.Equal(3,reservations.Distinct().Count());
        foreach(var kind in residents)Assert.True(world.TryAcquire(FurnitureCompatibility.Reservation(sofa,kind),kind));
        world.Release(PetAppearance.BorderCollie);
        Assert.Equal(2,world.Count);
        Assert.True(world.Available(FurnitureCompatibility.Reservation(sofa,PetAppearance.BorderCollie),PetAppearance.Cat));
        Assert.False(world.Available(FurnitureCompatibility.Reservation(sofa,PetAppearance.Cat),PetAppearance.Girl));
    }

    [Fact]
    public void ThirdProfileRoundTripsWithoutChangingTheOriginalResidents()
    {
        var root=Path.Combine(Path.GetTempPath(),"DesktopLifeBorderCollieTests",Guid.NewGuid().ToString("N"));
        try
        {
            var cat=CharacterProfile.Create(PetAppearance.Cat,Epoch);
            cat.Learning.Companion.Bond=87;
            var girl=CharacterProfile.Create(PetAppearance.Girl,Epoch);
            girl.Learning.Companion.Name="小花";
            var dog=CharacterProfile.Create(PetAppearance.BorderCollie,Epoch) with{Position=new(300,400)};
            dog.Learning.Companion.Name="雪球";
            dog.Learning.Companion.Bond=42;
            var snapshot=new OrganismSnapshot{Pet=cat.Pet,Learning=cat.Learning,Personality=cat.Personality,
                Settings=new(){PetAppearance=PetAppearance.Cat,Presence=PresenceMode.All},OtherCharacter=girl,AdditionalCharacter=dog};
            var store=new OrganismStore(root);
            store.Save(snapshot);
            var loaded=store.Load();
            Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,loaded.SchemaVersion);
            Assert.Equal(87,loaded.GetCharacter(PetAppearance.Cat)!.Learning.Companion.Bond);
            Assert.Equal("小花",loaded.GetCharacter(PetAppearance.Girl)!.Learning.Companion.Name);
            Assert.Equal("雪球",loaded.GetCharacter(PetAppearance.BorderCollie)!.Learning.Companion.Name);
            Assert.Equal(new RoomPoint(300,400),loaded.AdditionalCharacter!.Position);
            CompanionCare.Apply(loaded.AdditionalCharacter.Pet.State,loaded.AdditionalCharacter.Learning.Companion,CareKind.Pet,Epoch.AddMinutes(1));
            Assert.Equal(87,loaded.Learning.Companion.Bond);
            Assert.Equal(15,loaded.OtherCharacter!.Learning.Companion.Bond);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public void DogCanBePrimaryAndDuplicateResidentKindsAreRejected()
    {
        var dog=CharacterProfile.Create(PetAppearance.BorderCollie,Epoch);
        var snapshot=new OrganismSnapshot{Pet=dog.Pet,Personality=dog.Personality,Learning=dog.Learning,
            Settings=new(){PetAppearance=PetAppearance.BorderCollie,Presence=PresenceMode.DogOnly},
            OtherCharacter=CharacterProfile.Create(PetAppearance.Cat,Epoch),AdditionalCharacter=CharacterProfile.Create(PetAppearance.Girl,Epoch)};
        var copy=JsonSerializer.Deserialize<OrganismSnapshot>(JsonSerializer.Serialize(snapshot))!;
        copy.Validate();
        Assert.Equal("邊牧",copy.GetCharacter(PetAppearance.BorderCollie)!.Learning.Companion.Name);
        Assert.Equal("女孩",copy.GetCharacter(PetAppearance.Girl)!.Learning.Companion.Name);
        Assert.Throws<InvalidDataException>(()=>(copy with{AdditionalCharacter=dog}).Validate());
        Assert.Throws<InvalidDataException>(()=>(copy with{AdditionalCharacter=copy.OtherCharacter}).Validate());
    }

    [Fact]
    public void VersionSixMakesLegacyVersionFiveReadersRejectInsteadOfDroppingThirdHistory()
    {
        var dog=CharacterProfile.Create(PetAppearance.BorderCollie,Epoch);
        dog.Learning.Companion.Name="第三位朋友的獨有名字";
        dog.Learning.Companion.Remember(Epoch,"第三位朋友的獨有回憶");
        var snapshot=new OrganismSnapshot{Pet=new(){LastSaveTime=Epoch},
            Settings=new(){PetAppearance=PetAppearance.Cat,Presence=PresenceMode.Both},
            OtherCharacter=CharacterProfile.Create(PetAppearance.Girl,Epoch),AdditionalCharacter=dog};
        var saved=JsonSerializer.Serialize(snapshot);
        Assert.Throws<NotSupportedException>(()=>ReadWithLegacyV09Contract(saved));

        // Show the actual failure the version boundary prevents: the older DTO
        // accepts v5 and silently ignores the third resident on its next save.
        var compatibleLooking=System.Text.Json.Nodes.JsonNode.Parse(saved)!;
        compatibleLooking["SchemaVersion"]=5;
        var legacy=ReadWithLegacyV09Contract(compatibleLooking.ToJsonString());
        var legacyResave=JsonSerializer.Serialize(legacy);
        Assert.DoesNotContain("AdditionalCharacter",legacyResave);
        Assert.DoesNotContain("第三位朋友的獨有名字",JsonSerializer.Serialize(legacy,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        Assert.Equal("第三位朋友的獨有回憶",snapshot.AdditionalCharacter!.Learning.Companion.Memories.Single().Text);
    }

    // Frozen compatibility fixture: the 0.9 reader's >5 gate and known fields,
    // independent of the new reader's current-version constant and migrations.
    private static LegacyV09Snapshot ReadWithLegacyV09Contract(string json)
    {
        using(var document=JsonDocument.Parse(json))
            if(document.RootElement.TryGetProperty("SchemaVersion",out var version)&&version.ValueKind==JsonValueKind.Number&&version.TryGetInt32(out var number)&&number>5)
                throw new NotSupportedException("Legacy 0.9 cannot read this checkpoint.");
        return JsonSerializer.Deserialize<LegacyV09Snapshot>(json)!;
    }
    private sealed record LegacyV09Snapshot
    {
        public LegacyV09Snapshot() { }
        public int SchemaVersion {get;init;}=5;
        public PetSnapshot Pet {get;init;}=new();
        public LearningState Learning {get;init;}=new();
        public PersonalityProfile Personality {get;init;}=new();
        public AppSettings Settings {get;init;}=new();
        public CharacterProfile? OtherCharacter {get;init;}
        public RoomPoint? PrimaryPosition {get;init;}
    }
}
