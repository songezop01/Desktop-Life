namespace DesktopLife.Core;

/// <summary>
/// Resolves the activity actually being performed for need changes. Hidden or
/// frozen characters retain ordinary online metabolism; they do not perform the
/// last requested activity. Offline rest remains a separate, bounded policy.
/// </summary>
public static class CompanionActivity
{
    public static BodyAction Resolve(BodyAction intent,BehaviorSequence? sequence,bool visible,bool paused,
        bool interacting,bool editing,bool grounded,bool playContact=false)
    {
        if(!visible||paused||interacting||editing)return BodyAction.Idle;
        if(sequence is { } current)
        {
            if(current.Finished)return BodyAction.Idle;
            if(current.Kind==SequenceKind.Sleep)
                return current.Phase==BehaviorPhase.Sleep&&grounded?BodyAction.Sleep:
                    current.Phase==BehaviorPhase.Approach?BodyAction.Walk:BodyAction.Sit;
            if(current.Kind==SequenceKind.Play)
                return grounded&&playContact&&current.Phase==BehaviorPhase.Contact?BodyAction.PlayToy:
                    current.Phase==BehaviorPhase.Approach?BodyAction.Walk:BodyAction.Sit;
            if(current.Kind==SequenceKind.Activity)
                return current.Phase==BehaviorPhase.Work&&grounded&&current.Activity==RoomActivity.Lego?BodyAction.PlayToy:
                    current.Phase==BehaviorPhase.Approach?BodyAction.Walk:BodyAction.Sit;
            if(current.Kind==SequenceKind.Box)return BodyAction.Sit;
        }
        // Sleep always has a sequence. A bare intent cannot establish rest or play.
        if(intent==BodyAction.Sleep)return BodyAction.Sit;
        if(intent==BodyAction.PlayToy)return grounded&&playContact?BodyAction.PlayToy:BodyAction.Sit;
        return intent;
    }
}
