using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class CharacterCapabilityTests
{
    [Theory]
    [InlineData(PetAppearance.Cat,CharacterExpression.WriteNote,false)]
    [InlineData(PetAppearance.Cat,CharacterExpression.DrawDoodle,false)]
    [InlineData(PetAppearance.Cat,CharacterExpression.Purr,true)]
    [InlineData(PetAppearance.Cat,CharacterExpression.Scratch,true)]
    [InlineData(PetAppearance.Girl,CharacterExpression.WriteNote,true)]
    [InlineData(PetAppearance.Girl,CharacterExpression.DrawDoodle,true)]
    [InlineData(PetAppearance.Girl,CharacterExpression.Purr,false)]
    [InlineData(PetAppearance.Girl,CharacterExpression.Scratch,false)]
    [InlineData(PetAppearance.Girl,CharacterExpression.LickPaw,false)]
    [InlineData(PetAppearance.Girl,CharacterExpression.Knead,false)]
    public void ExpressionsRespectIdentity(PetAppearance c,CharacterExpression e,bool expected)=>Assert.Equal(expected,CharacterCapability.Allows(c,e));
    [Fact] public void LegacyPreferenceCannotOverrideCatEligibility()
    {
        var learning=new RewardLearning();learning.State.ActionPreference[BodyAction.DrawDoodle]=.75;learning.State.ActionPreference[BodyAction.WriteNote]=.75;
        var context=new EnvironmentContext(new(),new(){Creativity=1},null,LearningContext.QuietDesktop,Affordances:new(true,true,true,true),Appearance:PetAppearance.Cat);
        var selector=new ActionSelection(1);var brain=new UtilityBrain().Evaluate(context);
        Assert.DoesNotContain(selector.Evaluate(context,brain,learning),a=>a.Action is BodyAction.WriteNote or BodyAction.DrawDoodle);
        Assert.NotEqual(BodyAction.DrawDoodle,selector.Select(context,brain,learning,BodyAction.DrawDoodle,true).Action);
    }
}
