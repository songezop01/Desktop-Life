using DesktopLife.Core;
using System.Windows;
namespace DesktopLife.App;
public partial class PetWindow
{
    public void NoticeOther(PetWindow other)
    {
        if(Interacting||FinishingMotion||EditingRoom)return;
        var center=body.X+HalfWidth;var otherCenter=other.body.X+other.HalfWidth;
        var separation=HalfWidth+other.HalfWidth+16*sceneScale;
        var reaction=OtherPresence.Decide(center,body.Y+BodyHeight,otherCenter,other.body.Y+other.BodyHeight,SequenceCommitted,BehaviorPersonality.Social,movementRandom.NextDouble(),separation);
        if(reaction==OtherPresenceReaction.None)return;
        attentionPoint=new Point(otherCenter,other.body.Y+other.BodyHeight*.4);
        facing=otherCenter>=center?1:-1;
        if(reaction is OtherPresenceReaction.AvoidOverlap or OtherPresenceReaction.BriefApproach)
        {
            var sign=center<=otherCenter?-1:1;
            SetAction(BodyAction.Walk);
            target=(Math.Clamp(otherCenter+sign*(reaction==OtherPresenceReaction.AvoidOverlap?separation+20*sceneScale:separation+40*sceneScale)-HalfWidth,Bounds().Left,Bounds().Left+Bounds().Width-BodyWidth),body.Y);
        }
    }
}
