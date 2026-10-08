using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class HouseLayoutTests
{
    public static IEnumerable<object[]> WorkAreas()
    {
        foreach (var floors in new[] { 1, 2, 3 })
        {
            yield return [floors, new BodyBounds(0, 0, 1920, 1040)];
            yield return [floors, new BodyBounds(-1920, -280, 1280, 680)];
            yield return [floors, new BodyBounds(1920, 100, 360, 800)];
            yield return [floors, new BodyBounds(-432, 720, 288, 580)];
        }
    }

    [Theory]
    [MemberData(nameof(WorkAreas))]
    public void EveryPresetFitsAllCharacterBodiesAndConnectorsInsideWorkArea(int floors, BodyBounds area)
    {
        var layout = HouseLayout.Create(floors, area);
        Assert.Equal(floors, layout.Floors.Count);
        Assert.Equal(floors - 1, layout.Stairs.Count);
        Assert.InRange(layout.SceneScale, double.Epsilon, 1);
        var girlWidth = 116d / 144 * HouseLayout.GirlDesignHeight * layout.SceneScale;
        foreach (var floor in layout.Floors)
        {
            Assert.InRange(floor.Y - HouseLayout.GirlDesignHeight * layout.SceneScale, area.Top, area.Top + area.Height);
            Assert.InRange(floor.Left, area.Left, area.Left + area.Width);
            Assert.InRange(floor.Right, area.Left, area.Left + area.Width);
            Assert.True(floor.Width >= girlWidth);
            Assert.Equal(floor.Index, layout.FindFloor((floor.Left + floor.Right) / 2, floor.Y));
        }
        foreach (var stair in layout.Stairs)
        {
            Assert.Equal(stair.LowerFloor + 1, stair.UpperFloor);
            Assert.Equal(layout.Floors[stair.LowerFloor].Y, stair.LowerLanding.Y);
            Assert.Equal(layout.Floors[stair.UpperFloor].Y, stair.UpperLanding.Y);
            foreach (var landing in new[] { stair.LowerLanding, stair.UpperLanding })
            {
                Assert.InRange(landing.X - girlWidth / 2, area.Left, area.Left + area.Width);
                Assert.InRange(landing.X + girlWidth / 2, area.Left, area.Left + area.Width);
                Assert.Equal(landing.Y, stair.Support.HeightAt(landing.X), 8);
            }
            Assert.Equal(16, stair.Steps.Count);
            Assert.All(stair.Steps, step =>
            {
                Assert.True(step.Width > 0 && step.Rise > 0);
                Assert.InRange(step.X, area.Left, area.Left + area.Width);
                Assert.InRange(step.X + step.Width, area.Left, area.Left + area.Width);
            });
        }
    }

    [Theory]
    [MemberData(nameof(WorkAreas))]
    public void FloorsReallySupportBodiesUnderProductionGravity(int floors, BodyBounds area)
    {
        var layout = HouseLayout.Create(floors, area);
        foreach (var designHeight in new[] { HouseLayout.GirlDesignHeight, HouseLayout.CatDesignHeight, HouseLayout.DogDesignHeight })
            foreach (var floor in layout.Floors)
            {
                var height = designHeight * layout.SceneScale;
                var width = height * 116 / 144;
                var feetX = (floor.Left + floor.Right) / 2;
                var body = new GravityBody { X = feetX - width / 2, Y = floor.Y - height - 1 };
                for (var i = 0; i < 10; i++) body.Step(.016, area, width, height, 0, layout.Platforms);
                Assert.True(body.Grounded);
                Assert.Equal(floor.Y, body.Y + height, 6);
            }
    }

    [Fact]
    public void SmallPortraitMonitorScalesTheWholeSceneWithoutChangingCharacterRatios()
    {
        var layout = HouseLayout.Create(3, new(1920, -100, 360, 800));
        Assert.Equal(360 / 560d, layout.SceneScale, 8);
        Assert.Equal(3.2, HouseLayout.GirlDesignHeight / HouseLayout.CatDesignHeight, 8);
        Assert.Equal(1.3, HouseLayout.DogDesignHeight / HouseLayout.CatDesignHeight, 8);
        Assert.True(layout.Floors[2].Y - HouseLayout.GirlDesignHeight * layout.SceneScale > layout.WorkArea.Top);
    }

    [Theory]
    [MemberData(nameof(WorkAreas))]
    public void FurnitureSeatsAndTallCatTreesLeaveBodyHeadroomOnEveryFloor(int count,BodyBounds area)
    {
        var layout=HouseLayout.Create(count,area);
        var characterSupports=new[]{(Height:HouseLayout.GirlDesignHeight,Support:HouseLayout.FurnitureSupportClearance),
            (Height:HouseLayout.CatDesignHeight,Support:230d),(Height:HouseLayout.DogDesignHeight,Support:230d)};
        foreach(var floor in layout.Floors)
            foreach(var character in characterSupports)
            {
                var top=floor.Y-(character.Height+character.Support)*layout.SceneScale;
                Assert.True(top>=area.Top+24*layout.SceneScale-.00001);
                if(floor.Index<layout.FloorCount-1)
                    Assert.True(top>=layout.Floors[floor.Index+1].Y+16*layout.SceneScale-.00001);
                var supportY=floor.Y-character.Support*layout.SceneScale;
                var support=new RoomPlatform(floor.Left+floor.Width*.4,floor.Width*.2,supportY,supportY);
                var feetX=support.X+support.Width/2;
                var body=new GravityBody{X=feetX-20*layout.SceneScale,Y=supportY-character.Height*layout.SceneScale};
                body.Step(.016,area,40*layout.SceneScale,character.Height*layout.SceneScale,0,[support]);
                Assert.True(body.Grounded);
                Assert.Equal(supportY,body.Y+character.Height*layout.SceneScale,6);
            }
    }

    [Fact]
    public void OriginsAndMonitorPixelDensityDoNotAlterNormalizedGeometry()
    {
        var normal = HouseLayout.Create(3, new(0, 0, 960, 700));
        var offset = HouseLayout.Create(3, new(-1440, 350, 960, 700));
        Assert.Equal(normal.SceneScale, offset.SceneScale);
        foreach (var pair in normal.Floors.Zip(offset.Floors))
        {
            Assert.Equal(pair.First.Left - 1440, pair.Second.Left, 8);
            Assert.Equal(pair.First.Y + 350, pair.Second.Y, 8);
        }
        // The caller converts each monitor's pixels into DIP once; the templates consume only that logical area.
        var fromPixels = RoomCoordinates.FromPixels(-2160, 525, 1440, 1050, 1.5);
        Assert.Equal(offset.WorkArea, HouseLayout.Create(3, fromPixels).WorkArea);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void UnsupportedFloorCountsAreRejected(int floors) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HouseLayout.Create(floors, new(0, 0, 1200, 900)));

    [Fact]
    public void InvalidMonitorGeometryIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HouseLayout.Create(2, new(double.NaN, 0, 1200, 900)));
        Assert.Throws<ArgumentOutOfRangeException>(() => HouseLayout.Create(2, new(0, 0, 0, 900)));
        Assert.Throws<ArgumentOutOfRangeException>(() => HouseLayout.Create(2, new(0, 0, 1200, -900)));
        Assert.Throws<ArgumentOutOfRangeException>(() => HouseLayout.Create(2, new(0, 0, double.MaxValue, 900)));
        Assert.Throws<ArgumentOutOfRangeException>(() => HouseLayout.Create(2, new(0, 0, .0001, 900)));
    }

    [Fact]
    public void UnchangedPresetProducesStableConnectorReservationKeys()
    {
        var first = HouseLayout.Create(3, new(0, 0, 1920, 1040));
        var resized = HouseLayout.Create(3, new(-1800, 300, 360, 700));
        Assert.Equal(first.Stairs.Select(s => s.ReservationKey), resized.Stairs.Select(s => s.ReservationKey));
        Assert.Equal(2, first.Stairs.Select(s => s.ReservationKey).Distinct().Count());
    }
}
