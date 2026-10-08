using DesktopLife.Core;
using System.Text.Json;
using Xunit;
namespace DesktopLife.Tests;
public class TransitionHabitTests
{
    private static readonly DateTimeOffset Now=new(2026,1,1,0,0,0,TimeSpan.Zero);
    [Fact] public void LearnsCompletedPairButNotInterruptedOrSelfLoop()
    {var h=new BehaviorTransitionPreference();h.Complete(LifeBehavior.Wake,Now);h.Complete(LifeBehavior.Groom,Now.AddMinutes(1));Assert.Single(h.Pairs);h.Complete(LifeBehavior.Play,Now.AddMinutes(2),false);h.Complete(LifeBehavior.Scratch,Now.AddMinutes(3));h.Complete(LifeBehavior.Scratch,Now.AddMinutes(4));Assert.Single(h.Pairs);}
    [Fact] public void RecentUseAndIdleGapPreventChains()
    {var h=new BehaviorTransitionPreference();h.Complete(LifeBehavior.Wake,Now);h.Complete(LifeBehavior.Groom,Now.AddMinutes(1));h.Complete(LifeBehavior.Wake,Now.AddMinutes(2));Assert.True(h.Bias(BodyAction.Groom,Now.AddMinutes(2))<0);Assert.Equal(0,h.Bias(BodyAction.Groom,Now.AddHours(1)));}
    [Fact] public void BoundedPairsRoundTripAndDecay()
    {
        var h=new BehaviorTransitionPreference();var r=new Random(19);for(var i=0;i<10000;i++)h.Complete((LifeBehavior)r.Next(9),Now.AddMinutes(i));Assert.InRange(h.Pairs.Count,1,32);Assert.All(h.Pairs,p=>Assert.True(p.Valid));
        var copy=JsonSerializer.Deserialize<BehaviorTransitionPreference>(JsonSerializer.Serialize(h))!;Assert.Null(copy.Previous);Assert.Equal(h.Pairs,copy.Pairs);
    }
    [Fact] public void InvalidOptionalPairsAreDiscarded()
    {var h=new BehaviorTransitionPreference{Pairs=[new(LifeBehavior.Play,LifeBehavior.Play,1,2,1),new(LifeBehavior.Play,LifeBehavior.Rest,double.NaN,2,1)]};h.Sanitize();Assert.Empty(h.Pairs);}
}
