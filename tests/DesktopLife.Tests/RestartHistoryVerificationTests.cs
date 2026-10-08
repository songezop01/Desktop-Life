using System.Text.Json;
using DesktopLife.App;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;
public class RestartHistoryVerificationTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.FromUnixTimeSeconds(2000000000/60*60);
    private static OrganismSnapshot Saved()
    {
        var value=new OrganismSnapshot();
        value.Learning.Adaptation.LastMinute=Now.ToUnixTimeSeconds()/60-1;
        value.Learning.Adaptation.Traits[AdaptiveTrait.Curiosity]=new(){Evidence=20,Offset=.01,LastEventMinute=value.Learning.Adaptation.LastMinute};
        value.Learning.Room.Items.Add(new(Guid.NewGuid(),FurnitureKind.Desk,300,500));
        return value;
    }
    private static OrganismSnapshot Copy(OrganismSnapshot value)=>JsonSerializer.Deserialize<OrganismSnapshot>(JsonSerializer.Serialize(value))!;

    [Theory][InlineData(1)][InlineData(6)][InlineData(1440)]
    public void ExactStartupEvidenceDecayIsAcceptedWithoutMutatingBaseline(int minutes)
    {
        var before=Saved();before.Learning.Adaptation.LastMinute=Now.ToUnixTimeSeconds()/60-minutes;
        var initial=JsonSerializer.Serialize(before);var after=Copy(before);
        after.Learning.Adaptation.Advance(Now);
        Assert.True(RestartHistoryVerification.IsPreserved(before,after,Now));
        Assert.Equal(initial,JsonSerializer.Serialize(before));
    }
    [Fact]
    public void StartupDecayDoesNotPermitNewEvidenceOrLostRewards()
    {
        var before=Saved();before.Learning.PositiveRewards=7;var after=Copy(before);after.Learning.Adaptation.Advance(Now);
        after.Learning.Adaptation.Traits[AdaptiveTrait.Curiosity].Evidence+=.0001;
        Assert.False(RestartHistoryVerification.IsPreserved(before,after,Now));
        after=Copy(before);after.Learning.Adaptation.Advance(Now);after.Learning.PositiveRewards--;
        Assert.False(RestartHistoryVerification.IsPreserved(before,after,Now));
    }
    [Fact]
    public void FutureClockAndChangedFurnitureIdentityAreRejected()
    {
        var before=Saved();var after=Copy(before);after.Learning.Adaptation.Advance(Now.AddMinutes(1));
        Assert.False(RestartHistoryVerification.IsPreserved(before,after,Now));
        after=Copy(before);after.Learning.Adaptation.Advance(Now);after.Learning.Room.Items[0]=after.Learning.Room.Items[0] with{Id=Guid.NewGuid()};
        Assert.False(RestartHistoryVerification.IsPreserved(before,after,Now));
    }
}
