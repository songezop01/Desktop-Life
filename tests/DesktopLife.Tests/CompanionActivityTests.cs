using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;

public class CompanionActivityTests
{
    private static BodyAction Resolve(BodyAction intent,BehaviorSequence? sequence=null,bool visible=true,bool paused=false,
        bool interacting=false,bool editing=false,bool grounded=true,bool playContact=false)
        =>CompanionActivity.Resolve(intent,sequence,visible,paused,interacting,editing,grounded,playContact);
    private static void AdvanceTo(BehaviorSequence sequence,BehaviorPhase phase,SequenceContext context)
    {
        for(var i=0;i<1000&&sequence.Phase!=phase&&!sequence.Finished;i++)sequence.Step(.05,context);
        Assert.Equal(phase,sequence.Phase);
    }
    [Theory][InlineData(PetAppearance.Girl)][InlineData(PetAppearance.Cat)][InlineData(PetAppearance.BorderCollie)]
    public void SleepApproachAndWakeDoNotRecoverNeeds(PetAppearance character)
    {
        var sequence=new BehaviorSequence(SequenceKind.Sleep,new(),50,"bed",character:character);
        AdvanceTo(sequence,BehaviorPhase.Approach,new());
        var state=new PetState();var approaching=CompanionCare.Step(state,TimeSpan.FromSeconds(10),Resolve(BodyAction.Sleep,sequence),false);
        Assert.True(approaching.Energy<state.Energy);Assert.True(approaching.Fatigue>state.Fatigue);
        AdvanceTo(sequence,BehaviorPhase.Sleep,new(Reached:true,Grounded:true));
        var sleeping=CompanionCare.Step(state,TimeSpan.FromSeconds(10),Resolve(BodyAction.Sleep,sequence),false);
        Assert.True(sleeping.Energy>state.Energy);Assert.True(sleeping.Fatigue<state.Fatigue);
        Assert.True(sequence.Interrupt(BehaviorInterruptReason.Care));
        Assert.Equal(BehaviorPhase.Wake,sequence.Phase);
        Assert.NotEqual(BodyAction.Sleep,Resolve(BodyAction.Sleep,sequence));
    }
    [Theory][InlineData(true,false,false,false)][InlineData(false,true,false,false)][InlineData(false,false,true,false)][InlineData(false,false,false,true)]
    public void FrozenOrHiddenSleepUsesOrdinaryMetabolism(bool hidden,bool paused,bool interacting,bool editing)
    {
        var sequence=new BehaviorSequence(SequenceKind.Sleep,new(),50);
        AdvanceTo(sequence,BehaviorPhase.Sleep,new(Grounded:true));
        var state=new PetState();var resolved=Resolve(BodyAction.Sleep,sequence,!hidden,paused,interacting,editing);
        Assert.Equal(BodyAction.Idle,resolved);
        Assert.Equal(CompanionCare.Step(state,TimeSpan.FromSeconds(10),BodyAction.Idle,false),CompanionCare.Step(state,TimeSpan.FromSeconds(10),resolved,false));
        Assert.Equal(BehaviorPhase.Sleep,sequence.Phase);
    }
    [Theory][InlineData(BodyAction.Sleep)][InlineData(BodyAction.PlayToy)][InlineData(BodyAction.ChaseCursor)]
    public void HiddenStaleIntentHasNoSleepOrPlayBenefit(BodyAction intent)
    {
        var state=new PetState();var hidden=Resolve(intent,visible:false);
        Assert.Equal(BodyAction.Idle,hidden);
        Assert.Equal(CompanionCare.Step(state,TimeSpan.FromSeconds(10),BodyAction.Idle,true),CompanionCare.Step(state,TimeSpan.FromSeconds(10),hidden,true));
    }
    [Fact]public void AirborneSleepingPoseDoesNotRecoverUntilGrounded()
    {
        var sequence=new BehaviorSequence(SequenceKind.Sleep,new(),50);
        AdvanceTo(sequence,BehaviorPhase.Sleep,new(Grounded:true));
        Assert.NotEqual(BodyAction.Sleep,Resolve(BodyAction.Sleep,sequence,grounded:false));
        Assert.Equal(BodyAction.Sleep,Resolve(BodyAction.Sleep,sequence,grounded:true));
        Assert.NotEqual(BodyAction.Sleep,Resolve(BodyAction.Sleep));
    }
    [Theory][InlineData(PetAppearance.Girl)][InlineData(PetAppearance.Cat)][InlineData(PetAppearance.BorderCollie)]
    public void PlayCreditFollowsPhysicalPlayInsteadOfNoticeAndApproach(PetAppearance character)
    {
        var sequence=new BehaviorSequence(SequenceKind.Play,new(),50,"ball",character:character);
        Assert.NotEqual(BodyAction.PlayToy,Resolve(BodyAction.PlayToy,sequence));
        AdvanceTo(sequence,BehaviorPhase.Approach,new());
        var state=new PetState();var approaching=CompanionCare.Step(state,TimeSpan.FromSeconds(10),Resolve(BodyAction.PlayToy,sequence),true);
        Assert.True(approaching.Boredom>state.Boredom);
        AdvanceTo(sequence,BehaviorPhase.Contact,new(Reached:true));
        Assert.NotEqual(BodyAction.PlayToy,Resolve(BodyAction.PlayToy,sequence));
        var playing=CompanionCare.Step(state,TimeSpan.FromSeconds(10),Resolve(BodyAction.PlayToy,sequence,playContact:true),true);
        Assert.True(playing.Boredom<state.Boredom);
        Assert.NotEqual(BodyAction.PlayToy,Resolve(BodyAction.PlayToy,sequence,grounded:false,playContact:true));
        Assert.Equal(BodyAction.Idle,Resolve(BodyAction.PlayToy,sequence,paused:true,playContact:true));
        sequence.Step(.05,new(Reached:true,Contact:true));
        Assert.Equal(BehaviorPhase.Recover,sequence.Phase);
        Assert.NotEqual(BodyAction.PlayToy,Resolve(BodyAction.PlayToy,sequence));
    }
    [Fact]public void BarePlayIntentRequiresGroundedPhysicalContact()
    {
        var state=new PetState();var approaching=CompanionCare.Step(state,TimeSpan.FromSeconds(10),Resolve(BodyAction.PlayToy),true);
        Assert.True(approaching.Boredom>state.Boredom);
        Assert.Equal(BodyAction.PlayToy,Resolve(BodyAction.PlayToy,playContact:true));
        Assert.NotEqual(BodyAction.PlayToy,Resolve(BodyAction.PlayToy,grounded:false,playContact:true));
        Assert.Equal(BodyAction.Idle,Resolve(BodyAction.PlayToy,visible:false,playContact:true));
    }
    [Theory][InlineData(SequenceKind.Play)][InlineData(SequenceKind.SelfGroom)]
    public void FinishedSequenceDoesNotCreditStalePlayEvenWithLastContact(SequenceKind kind)
    {
        var sequence=new BehaviorSequence(kind,new(),50,"ball");
        Assert.True(sequence.Interrupt(BehaviorInterruptReason.Safety));
        for(var i=0;i<30&&!sequence.Finished;i++)sequence.Step(.05,new(Grounded:true));
        Assert.True(sequence.Finished);
        Assert.Equal(BodyAction.Idle,Resolve(BodyAction.PlayToy,sequence,playContact:true));
    }
    [Fact]public void GirlLegoNeedsFollowActualWorkAndPause()
    {
        var sequence=new BehaviorSequence(SequenceKind.Activity,new(),50,"desk",character:PetAppearance.Girl,activity:RoomActivity.Lego);
        Assert.NotEqual(BodyAction.PlayToy,Resolve(BodyAction.PlayToy,sequence));
        AdvanceTo(sequence,BehaviorPhase.Work,new(Reached:true));
        Assert.Equal(BodyAction.PlayToy,Resolve(BodyAction.PlayToy,sequence));
        Assert.Equal(BodyAction.Idle,Resolve(BodyAction.PlayToy,sequence,paused:true));
        AdvanceTo(sequence,BehaviorPhase.Pause,new(Reached:true));
        Assert.NotEqual(BodyAction.PlayToy,Resolve(BodyAction.PlayToy,sequence));
    }
    [Fact]public void VisibleOrdinaryIdleRetainsPresenceAndRuntimeWithoutChangingBond()
    {
        var session=new HomeostasisSession(new());var companion=new CompanionState();var bond=companion.Bond;
        session.AdvanceCompanion(TimeSpan.FromSeconds(5),Resolve(BodyAction.PlayToy,visible:false),true);
        Assert.Equal(5,session.TotalRuntimeSeconds);Assert.True(session.State.Hunger>20);
        Assert.True(session.State.Loneliness<20);Assert.Equal(bond,companion.Bond);
    }
}
