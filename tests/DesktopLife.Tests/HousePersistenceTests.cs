using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public sealed class HousePersistenceTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"DesktopLifeHousePersistenceTests",Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Epoch=new(2026,10,6,0,0,0,TimeSpan.Zero);

    [Fact]
    public void VersionSixMigrationDefaultsToOneFloorAndPreservesEveryResidentAndOriginalHistory()
    {
        var expected=ThreeResidents();
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(expected))!;
        legacy["SchemaVersion"]=6;
        legacy.AsObject().Remove("Food");
        foreach(var learning in new[]{legacy["Learning"]!,legacy["OtherCharacter"]!["Learning"]!,legacy["AdditionalCharacter"]!["Learning"]!})
        {
            var room=learning["Room"]!.AsObject();room.Remove("FloorCount");
            foreach(var item in room["Items"]!.AsArray())item!.AsObject().Remove("FloorIndex");
        }
        var original=legacy.ToJsonString();
        var store=new OrganismStore(root);Directory.CreateDirectory(root);File.WriteAllText(store.PathName,original);
        var loaded=store.Load();
        Assert.Equal(OrganismSnapshot.CurrentSchemaVersion,loaded.SchemaVersion);
        Assert.Empty(loaded.Food.Servings);
        Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(loaded));
        foreach(var character in Enum.GetValues<PetAppearance>())
        {
            var restored=loaded.GetCharacter(character)!;
            Assert.Equal(1,restored.Learning.Room.FloorCount);
            Assert.Equal(0,Assert.Single(restored.Learning.Room.Items).FloorIndex);
            Assert.Single(restored.Learning.Companion.Memories);
            Assert.Single(restored.Learning.ActionPreference);
        }
        Assert.Equal(original,File.ReadAllText(store.PathName));
        store.Save(loaded);
        Assert.Equal(original,File.ReadAllText(store.PathName+".bak"));
        Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(store.Load()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SelectedFloorPresetAndAssignedFurnitureSurviveSaveExportAndImport(int count)
    {
        var snapshot=ThreeResidents();
        foreach(var character in Enum.GetValues<PetAppearance>())
        {
            var room=snapshot.GetCharacter(character)!.Learning.Room;room.FloorCount=count;
            room.Items[0]=room.Items[0] with{FloorIndex=count-1};
        }
        var store=new OrganismStore(root);store.Save(snapshot);
        var export=Path.Combine(root,"house-export.json");store.Export(export);
        var imported=new OrganismStore(Path.Combine(root,"imported"));imported.StageImport(export);
        Assert.True(imported.ApplyPendingImport());
        Assert.Equal(JsonSerializer.Serialize(snapshot),JsonSerializer.Serialize(imported.Load()));
        Assert.Equal(count,imported.Load().Learning.Room.FloorCount);
        Assert.Equal(count-1,Assert.Single(imported.Load().Learning.Room.Items).FloorIndex);
    }

    [Fact]
    public void NextSchemaIsRejectedWithoutDiscardingFutureHouseDataOrFallingBackToOlderBackup()
    {
        var store=new OrganismStore(root);var snapshot=ThreeResidents();snapshot.Learning.Room.FloorCount=3;
        snapshot.Learning.Room.Items[0]=snapshot.Learning.Room.Items[0] with{FloorIndex=2};
        store.Save(snapshot);store.Save(snapshot);
        var backup=File.ReadAllText(store.PathName+".bak");
        var next=JsonNode.Parse(JsonSerializer.Serialize(snapshot))!;
        next["SchemaVersion"]=OrganismSnapshot.CurrentSchemaVersion+1;
        next["UnknownHouseHistory"]=new JsonObject{["FourthFloorMemory"]="未來樓層資料不能被較舊版本丟棄"};
        var future=next.ToJsonString();File.WriteAllText(store.PathName,future);
        Assert.Throws<NotSupportedException>(()=>store.Load());
        Assert.Throws<NotSupportedException>(()=>store.Save(snapshot));
        Assert.Throws<NotSupportedException>(()=>store.StageImport(store.PathName));
        Assert.Equal(future,File.ReadAllText(store.PathName));
        Assert.Equal(backup,File.ReadAllText(store.PathName+".bak"));
        Assert.Empty(Directory.GetFiles(root,"*.damaged-*"));
        Assert.Null(store.RecoveryMessage);
    }

    [Fact]
    public void FrozenSaveOwnsFloorCountAndFurnitureAssignmentsForAllResidents()
    {
        var snapshot=ThreeResidents();
        var rooms=Enum.GetValues<PetAppearance>().Select(kind=>snapshot.GetCharacter(kind)!.Learning.Room).ToArray();
        for(var i=0;i<rooms.Length;i++){rooms[i].FloorCount=i+1;rooms[i].Items[0]=rooms[i].Items[0] with{FloorIndex=i};}
        var expected=JsonSerializer.Serialize(snapshot);
        var frozen=FrozenOrganismSnapshot.Capture(snapshot);
        foreach(var room in rooms){room.FloorCount=1;room.Items.Clear();}
        var store=new OrganismStore(root);frozen.SaveTo(store);
        Assert.Equal(expected,JsonSerializer.Serialize(store.Load()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4)]
    public void UnsupportedPresetIsRejectedBeforeReplacingExistingSave(int count)
    {
        var snapshot=ThreeResidents();var store=new OrganismStore(root);store.Save(snapshot);var original=File.ReadAllText(store.PathName);
        snapshot.Learning.Room.FloorCount=count;
        Assert.Throws<InvalidDataException>(()=>store.Save(snapshot));
        Assert.Equal(original,File.ReadAllText(store.PathName));
    }

    [Fact]
    public void ReducingFloorCountWithoutReassigningFurnitureCannotSaveAnOrphanedItem()
    {
        var room=new RoomState{FloorCount=3,Items=[new(Guid.NewGuid(),FurnitureKind.HumanBed,100,200,2)]};
        room.Validate();room.FloorCount=2;
        Assert.Throws<InvalidDataException>(()=>room.Validate());
        room.Items[0]=room.Items[0] with{FloorIndex=1};room.Validate();
    }

    [Theory]
    [MemberData(nameof(HouseLayoutTests.WorkAreas),MemberType=typeof(HouseLayoutTests))]
    public void CharacterResizeKeepsSharedFootAndCenterAnchorsForAllSceneScales(int count,BodyBounds area)
    {
        var house=HouseLayout.Create(count,area);
        foreach(var floor in house.Floors)
        {
            var body=new DesktopBody();var footX=(floor.Left+floor.Right)/2;
            body.Place(footX-DesktopBody.Width/2,floor.Y-DesktopBody.Height,area);
            foreach(var designHeight in new[]{HouseLayout.GirlDesignHeight,HouseLayout.CatDesignHeight,HouseLayout.DogDesignHeight,HouseLayout.GirlDesignHeight})
            {
                var height=designHeight*house.SceneScale;var width=116d/144*height;
                body.Resize(width,height,area);
                Assert.Equal(footX,body.X+body.BodyWidth/2,6);
                Assert.Equal(floor.Y,body.Y+body.BodyHeight,6);
                Assert.Equal(width,body.BodyWidth,6);Assert.Equal(height,body.BodyHeight,6);
                Assert.InRange(body.Y,area.Top,area.Top+area.Height-height);
            }
        }
    }

    [Fact]
    public void ResizeOnAReplacedSmallMonitorClampsTheWholeBodyAndRejectsInvalidDimensions()
    {
        var body=new DesktopBody();body.Place(1700,800,new(0,0,1920,1080));
        var area=new BodyBounds(-360,0,360,640);body.Resize(140,174,area);
        Assert.InRange(body.X,area.Left,area.Left+area.Width-140);
        Assert.InRange(body.Y,area.Top,area.Top+area.Height-174);
        Assert.Throws<ArgumentOutOfRangeException>(()=>body.Resize(double.NaN,174,area));
        Assert.Throws<ArgumentOutOfRangeException>(()=>body.Resize(140,0,area));
    }

    private static OrganismSnapshot ThreeResidents()
    {
        var profiles=Enum.GetValues<PetAppearance>().ToDictionary(kind=>kind,kind=>CharacterProfile.Create(kind,Epoch));
        foreach(var pair in profiles)
        {
            pair.Value.Learning.Companion.Name=pair.Key switch{PetAppearance.Girl=>"娜娜",PetAppearance.Cat=>"橘子",_=>"十一"};
            pair.Value.Learning.Companion.Remember(Epoch,$"{pair.Value.Learning.Companion.Name}的獨立回憶");
            pair.Value.Learning.ActionPreference[BodyAction.Walk]=.1+(int)pair.Key*.05;
            pair.Value.Learning.PositiveRewards=10+(int)pair.Key;
            pair.Value.Learning.Room.Items.Add(new(Guid.NewGuid(),FurnitureKind.Box,100+(int)pair.Key*100,500));
        }
        var cat=profiles[PetAppearance.Cat];
        return new(){Pet=cat.Pet,Learning=cat.Learning,Personality=cat.Personality,
            Settings=new(){PetAppearance=PetAppearance.Cat,Presence=PresenceMode.All},
            OtherCharacter=profiles[PetAppearance.Girl],AdditionalCharacter=profiles[PetAppearance.BorderCollie],PrimaryPosition=new(300,500)};
    }

    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
}
