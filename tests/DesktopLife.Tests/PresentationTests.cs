using DesktopLife.Core;
using System.Text.Json;
using Xunit;
namespace DesktopLife.Tests;
public class PresentationTests
{
    [Theory]
    [InlineData(false,false,false,true,true,true,true)]
    [InlineData(true,true,false,true,false,false,true)]
    [InlineData(false,true,false,true,true,false,true)]
    [InlineData(false,false,true,true,true,true,true)]
    public void PriorityTruthTable(bool full,bool max,bool desktop,bool highest,bool high,bool medium,bool low)
    {
        var state=new ForegroundState(full,max,desktop);
        Assert.Equal(new[]{highest,high,medium,low},Enum.GetValues<DisplayPriority>().Select(p=>DisplayPolicy.Visible(p,state)));
        Assert.All(Enum.GetValues<DisplayPriority>(),p=>Assert.False(DisplayPolicy.Visible(p,state,true)));
    }
    [Fact] public void LegacySettingsUseNonIntrusiveDefault()
    {
        var settings=JsonSerializer.Deserialize<AppSettings>("{\"SchemaVersion\":1}")!;
        Assert.Equal(DisplayPriority.Medium,settings.DisplayPriority);settings.Validate();
        var copy=JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings with {DisplayPriority=DisplayPriority.Desktop,PetAppearance=PetAppearance.Cat}))!;
        Assert.Equal(DisplayPriority.Desktop,copy.DisplayPriority);Assert.Equal(PetAppearance.Cat,copy.PetAppearance);
        Assert.Throws<InvalidDataException>(()=>(copy with{PetAppearance=(PetAppearance)99}).Validate());
    }
    [Fact] public void WalkingRunningAndSleepingHaveDistinctPhysicalPoses()
    {
        var walk=PetPose.At(BodyAction.Walk,.15);var run=PetPose.At(BodyAction.ChaseCursor,.15);var sleep=PetPose.At(BodyAction.Sleep,.15);
        Assert.NotEqual(0,walk.Leg);Assert.NotEqual(walk.Leg,PetPose.At(BodyAction.Walk,.4).Leg);
        Assert.True(run.Tilt>walk.Tilt);Assert.True(sleep.Sleeping);Assert.True(sleep.Tilt>60);
        Assert.NotEqual(sleep.ScaleY,PetPose.At(BodyAction.Sleep,.8).ScaleY);
        Assert.True(PetPose.At(BodyAction.Stretch,0).Arm>100);
    }
}
