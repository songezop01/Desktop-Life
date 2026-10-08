using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;

public class FurnitureCompatibilityTests
{
    [Fact]
    public void GirlSleepHasNoFelineChoreographyEvenWithCatWakeVariant()
    {
        var sequence = new BehaviorSequence(SequenceKind.Sleep, new(), 40, wakeVariant:1, character:PetAppearance.Girl);
        var phases = new HashSet<BehaviorPhase>();
        for(var i=0;i<1200&&!sequence.Finished;i++)
        {
            phases.Add(sequence.Phase);
            sequence.Step(.1,new(Reached:true,Grounded:true,Rested:true));
        }
        Assert.True(sequence.Finished);
        Assert.Contains(BehaviorPhase.Sleep, phases);
        Assert.Contains(BehaviorPhase.Wake, phases);
        Assert.DoesNotContain(BehaviorPhase.Curl, phases);
        Assert.DoesNotContain(BehaviorPhase.Knead, phases);
        Assert.DoesNotContain(BehaviorPhase.LickPaw, phases);
        Assert.DoesNotContain(BehaviorPhase.WashFace, phases);
    }

    [Theory]
    [InlineData(FurnitureKind.PetBed, FurnitureUse.Sleep)]
    [InlineData(FurnitureKind.Box, FurnitureUse.Rest)]
    [InlineData(FurnitureKind.Scratcher, FurnitureUse.Scratch)]
    [InlineData(FurnitureKind.CatTree, FurnitureUse.Rest)]
    [InlineData(FurnitureKind.Bookshelf, FurnitureUse.Rest)]
    public void CatOnlyAffordancesNeverBecomeGirlOpportunities(FurnitureKind furniture, FurnitureUse use)
    {
        Assert.True(FurnitureCompatibility.CanUse(PetAppearance.Cat, furniture, use));
        Assert.False(FurnitureCompatibility.CanUse(PetAppearance.Girl, furniture, use));
        Assert.Equal(FurnitureUse.None, FurnitureCompatibility.AvailableUses(PetAppearance.Girl, furniture) & use);
    }

    [Fact]
    public void UnofferedOrInvalidUsesAreRejected()
    {
        Assert.False(FurnitureCompatibility.CanUse(PetAppearance.Cat, FurnitureKind.Desk, FurnitureUse.Sleep));
        Assert.False(FurnitureCompatibility.CanUse(PetAppearance.Girl, (FurnitureKind)999, FurnitureUse.Rest));
        Assert.False(FurnitureCompatibility.CanUse((PetAppearance)999, FurnitureKind.Cushion, FurnitureUse.Rest));
        Assert.False(FurnitureCompatibility.CanUse(PetAppearance.Cat, FurnitureKind.PetBed, FurnitureUse.Sleep | FurnitureUse.Scratch));
    }
}
