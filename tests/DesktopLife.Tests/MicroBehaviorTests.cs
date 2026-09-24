using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class MicroBehaviorTests
{
    [Fact] public void SafetyRecoveryCannotBeReplacedByToyStimulus()
    {
        var s=new BehaviorSequence(SequenceKind.Play,new(),50,"ball");
        Assert.True(s.Interrupt(BehaviorInterruptReason.Safety));
        Assert.False(s.Interrupt(BehaviorInterruptReason.Stimulus));
        Assert.Equal(BehaviorInterruptReason.Safety,s.InterruptedBy);
    }
    [Fact] public void PendulumSleepsAndWakesWhenBatted()
    {
        var toy=new HangingToy();
        for(var i=0;i<10000;i++)toy.Step(.033);
        Assert.True(toy.IsResting);
        toy.Bat(120);toy.Step(.033);
        Assert.False(toy.IsResting);Assert.True(toy.X>0);
    }
    [Fact] public void SeededGesturesAreRepeatableAndBounded()
    {
        var a = new MicroBehavior(7); var b = new MicroBehavior(7);
        for (var i = 0; i < 10000; i++)
        {
            var p = a.Step(.033, new(), false, false);
            Assert.Equal(p, b.Step(.033, new(), false, false));
            Assert.InRange(p.Blink, 0, 1); Assert.InRange(p.Ear, -7, 7);
            Assert.InRange(p.Head, -2, 2); Assert.InRange(p.Weight, -.7, .7);
        }
    }
    [Fact] public void BlinkIntervalsVary()
    {
        var m = new MicroBehavior(11); var times = new List<int>(); var previous = 0d;
        for (var i = 0; i < 5000; i++)
        { var p = m.Step(.033, new(), false, false); if (p.Blink > 0 && previous == 0) times.Add(i); previous = p.Blink; }
        Assert.True(times.Count > 15);
        Assert.True(times.Zip(times.Skip(1), (a, b) => b-a).Distinct().Count() > 5);
    }
    [Fact] public void ContactAndSleepPreserveSupportAndContactPoints()
    {
        var m = new MicroBehavior(1);
        for (var i = 0; i < 1000; i++)
        { var p = m.Step(.033, new(), true, true); Assert.Equal(1, p.Blink); Assert.Equal(0, p.Weight); Assert.Equal(0, p.Head); }
    }
}
