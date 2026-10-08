using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class RoomActivitySupportTests
{
    [Theory]
    [InlineData(FurnitureKind.CatBowl,-8,1536,816)]
    [InlineData(FurnitureKind.CatBowl,8,224,816)]
    [InlineData(FurnitureKind.Computer,-8,224,816)]
    [InlineData(FurnitureKind.Computer,8,1536,816)]
    [InlineData(FurnitureKind.DrawingBook,-8,1536,816)]
    [InlineData(FurnitureKind.DrawingBook,8,224,816)]
    [InlineData(FurnitureKind.Bookshelf,-8,224,816)]
    [InlineData(FurnitureKind.Bookshelf,18,1536,816)]
    public void MovedStandingFurnitureTargetsItsAssignedPhysicalFloor(FurnitureKind kind,double offset,double width,double height)
    {
        var bounds=new BodyBounds(-1600,-200,width,height);
        var house=HouseLayout.Create(3,bounds);
        var item=new RoomItem(Guid.NewGuid(),kind,-1500,house.Floors[1].Y-120*house.SceneScale+offset,1);
        var feet=RoomActivityPolicy.StandingFeet(item,house,bounds);

        Assert.Equal(house.Floors[1].Y,feet);
        Assert.Equal(1,house.FindFloor(-1500,feet));
        var route=HouseTraversal.Plan(house,house.Floors[2].Left+house.Floors[2].Width*.5,house.Floors[2].Y,1,-1500);
        Assert.NotNull(route);
        Assert.Single(route!,step=>step.Kind==HouseTravelKind.Stair);
    }

    [Fact]
    public void FloorIndexOverridesFurnitureImageCrossingTheFloorPlane()
    {
        var bounds=new BodyBounds(0,0,1536,816);var house=HouseLayout.Create(3,bounds);
        var bookshelf=new RoomItem(Guid.NewGuid(),FurnitureKind.Bookshelf,0,house.Floors[1].Y+18,1);
        Assert.Equal(house.Floors[1].Y,RoomActivityPolicy.StandingFeet(bookshelf,house,bounds));
        Assert.NotEqual(house.Floors[0].Y,RoomActivityPolicy.StandingFeet(bookshelf,house,bounds));
    }

    [Fact]
    public void LegacyStandingActivityUsesScreenSupport()
    {
        var bounds=new BodyBounds(-1200,-50,1200,800);
        Assert.Equal(750,RoomActivityPolicy.StandingFeet(new(Guid.NewGuid(),FurnitureKind.Computer,-1000,100),null,bounds));
    }

    [Fact]
    public void PhoneScaleChairPairingDoesNotCrossFloors()
    {
        var house=HouseLayout.Create(3,new(0,0,224,816));
        var table=new RoomItem(Guid.NewGuid(),FurnitureKind.Desk,90,house.Floors[1].Y-64,1);
        var wrongFloorChair=new RoomItem(Guid.NewGuid(),FurnitureKind.Chair,90,table.Y+60,0);
        var sameFloorChair=new RoomItem(Guid.NewGuid(),FurnitureKind.Chair,150,table.Y+15,1);
        Assert.Null(RoomActivityPolicy.NearbyChair(table,[table,wrongFloorChair]));
        Assert.Equal(sameFloorChair,RoomActivityPolicy.NearbyChair(table,[table,wrongFloorChair,sameFloorChair]));
    }
}
