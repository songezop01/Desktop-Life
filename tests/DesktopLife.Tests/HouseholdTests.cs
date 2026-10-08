using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class HouseholdTests
{
    [Theory][InlineData(PresenceMode.CatOnly,true,false)][InlineData(PresenceMode.GirlOnly,false,true)][InlineData(PresenceMode.Both,true,true)]
    public void ModeSelectsCharactersWithoutChangingAppearance(PresenceMode mode,bool cat,bool girl)
    {Assert.Equal(cat,PresencePolicy.Includes(mode,PetAppearance.Cat));Assert.Equal(girl,PresencePolicy.Includes(mode,PetAppearance.Girl));}
    [Fact] public void ReservationIsExclusiveAndReleasedOnHide()
    {var world=new HouseholdOccupancy();Assert.True(world.TryAcquire("bed",PetAppearance.Cat));Assert.False(world.TryAcquire("bed",PetAppearance.Girl));Assert.True(world.TryAcquire("ball",PetAppearance.Girl));world.Release(PetAppearance.Cat);Assert.True(world.TryAcquire("bed",PetAppearance.Girl));Assert.Equal(2,world.Count);}
    [Fact] public void SleepingCharacterIsNotPulledOutOfItsSequenceByAwareness()
    {Assert.Equal(OtherPresenceReaction.None,OtherPresence.Decide(100,100,120,100,true,.9,0));Assert.Equal(OtherPresenceReaction.AvoidOverlap,OtherPresence.Decide(100,100,120,100,false,.9,0));Assert.Equal(OtherPresenceReaction.ObserveOther,OtherPresence.Decide(100,100,250,100,false,.1,.9));Assert.Equal(OtherPresenceReaction.BriefApproach,OtherPresence.Decide(100,100,250,100,false,.9,0));}
}
