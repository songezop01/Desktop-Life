using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class IndividualityIntegrationTests
{
    [Fact] public void NeedPriorityWinsOverStrongLearnedHabit()
    {var learning=new RewardLearning();var now=DateTimeOffset.UtcNow;learning.State.Transitions.Pairs.Add(new(LifeBehavior.Wake,LifeBehavior.Play,1,100,now.ToUnixTimeSeconds()/60));learning.State.Transitions.Complete(LifeBehavior.Wake,now);var c=new EnvironmentContext(new(){Fatigue=99},new(){Playfulness=1},null,LearningContext.UserNearby,Affordances:new(true,true,true,true),Appearance:PetAppearance.Cat,Now:now);Assert.Equal(BodyAction.Sleep,new ActionSelection(3).Select(c,new UtilityBrain().Evaluate(c),learning,BodyAction.Idle,false).Action);}
    [Fact] public void BondDoesNotChangeInnateOrEffectivePersonality()
    {var p=new PersonalityProfile{Social=.1,Independence=.9};var adaptation=new PersonalityAdaptation();var before=adaptation.Effective(p);var c=new CompanionState{Bond=90};CompanionCare.Apply(new(),c,CareKind.Pet,DateTimeOffset.UtcNow);Assert.Equal(before,adaptation.Effective(p));}
    [Fact] public void WakeBranchesRemainOverlapping()
    {foreach(var p in new[]{new PersonalityProfile{Laziness=1},new PersonalityProfile{Curiosity=1},new PersonalityProfile{Social=1}}){var r=new Random(80);var count=new int[3];for(var i=0;i<1000;i++)count[HomeRoutine.WakeVariant(p,r)]++;Assert.All(count,c=>Assert.InRange(c,100,750));}}
    [Fact] public void IndependentHighBondPetStillChoosesDistantSpots()
    {var spots=new[]{new RestSpot("near",FurnitureKind.PetBed,100,700),new RestSpot("far",FurnitureKind.PetBed,850,700)};int Far(PersonalityProfile p){var r=new Random(9);var preference=new RestSpotPreference();return Enumerable.Range(0,2000).Count(_=>preference.ChooseHome(spots,p,95,475,100,DateTimeOffset.UnixEpoch,20,r)!.Id=="far");}Assert.True(Far(new(){Social=.1,Independence=.95})>Far(new(){Social=.95,Independence=.1})+250);}
}
