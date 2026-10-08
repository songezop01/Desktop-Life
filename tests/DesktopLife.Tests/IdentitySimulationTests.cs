using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;

public class IdentitySimulationTests
{
    private static readonly DateTimeOffset Epoch=new(2026,1,1,0,0,0,TimeSpan.Zero);
    public static IEnumerable<object[]> Profiles()=>new[]{
        new object[]{"Explorer",new PersonalityProfile{Curiosity=.95,Independence=.8,Social=.25}},
        new object[]{"Social",new PersonalityProfile{Social=.95,Independence=.15}},
        new object[]{"Playful",new PersonalityProfile{Playfulness=.95,Laziness=.1}},
        new object[]{"Relaxed",new PersonalityProfile{Laziness=.95,Playfulness=.2}}};

    [Theory, MemberData(nameof(Profiles)), Trait("Verification","Identity")]
    public void FourHoursUsesProductionSelectorAndRespectsCapabilities(string name,PersonalityProfile profile)
    {
        foreach(var appearance in Enum.GetValues<PetAppearance>())
        {
            var selector=new ActionSelection(81);var brain=new UtilityBrain();var learning=new RewardLearning();
            var state=new PetState();var action=BodyAction.Idle;var histogram=new Dictionary<BodyAction,int>();
            for(var second=0;second<4*3600;second+=10)
            {
                var now=Epoch.AddSeconds(second);
                var context=new EnvironmentContext(state,profile,null,LearningContext.UserNearby,Affordances:new(true,true,true,true),Appearance:appearance,Now:now);
                action=selector.Select(context,brain.Evaluate(context),learning,action,false).Action??BodyAction.Idle;
                Assert.True(CharacterCapability.Allows(appearance,action),$"{name}: {appearance} chose {action}");
                state=CompanionCare.Step(state,TimeSpan.FromSeconds(10),action,true);state.Validate();
                histogram[action]=histogram.GetValueOrDefault(action)+1;
                // No rendered contact evidence exists in a logical simulation, so do not credit successful sequences.
            }
            Assert.True(histogram.Count>1,$"{name} never varied its action");
        }
    }

    [Theory, InlineData(0),InlineData(1),InlineData(2),InlineData(3),Trait("Verification","Longitudinal")]
    public void SixWeeksOfDifferentHistoriesRemainBoundedAndReloadable(int history)
    {
        var adaptation=new PersonalityAdaptation();var basis=new PersonalityProfile{Social=.2,Playfulness=.3};
        for(var minute=0;minute<42*1440;minute++)
        {
            var now=Epoch.AddMinutes(minute);
            if(history==0)adaptation.Observe(AdaptiveTrait.Social,now);
            else if(history==1)adaptation.Observe(AdaptiveTrait.Playfulness,now);
            else if(history==2 && minute%60<10)adaptation.Observe(AdaptiveTrait.Curiosity,now);
            else adaptation.Advance(now);
            Assert.All(adaptation.Traits.Values,t=>Assert.True(t.Valid));
            adaptation.Effective(basis).Validate();
            if(minute%1440==0)adaptation=System.Text.Json.JsonSerializer.Deserialize<PersonalityAdaptation>(System.Text.Json.JsonSerializer.Serialize(adaptation))!;
        }
        Assert.Equal(.2,basis.Social);Assert.Equal(.3,basis.Playfulness);
        if(history==3)Assert.Empty(adaptation.Traits);
        if(history==0){Assert.InRange(adaptation.Offset(AdaptiveTrait.Social),.1,.12);Assert.Equal(0,adaptation.Offset(AdaptiveTrait.Playfulness));}
        if(history==1){Assert.InRange(adaptation.Offset(AdaptiveTrait.Playfulness),.1,.12);Assert.Equal(0,adaptation.Offset(AdaptiveTrait.Social));}
    }
}
