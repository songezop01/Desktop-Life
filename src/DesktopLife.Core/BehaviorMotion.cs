namespace DesktopLife.Core;

public readonly record struct MotionTarget(double X,double Y,bool Waiting,bool Sit);
public static class BehaviorMotion
{
    public static MotionTarget Social(BehaviorParameters parameters,double elapsed,double cursorX,double cursorY,double x,double y)
    {
        var radius=parameters.Movement.CursorDistance;var pattern=parameters.Social.Pattern;
        var angle=Math.Atan2(y+72-cursorY,x+58-cursorX);
        if(parameters.Social.AvoidBriefly&&elapsed<4)radius+=120;
        else if(pattern==SocialPattern.Observe || pattern is SocialPattern.Follow or SocialPattern.Wait && elapsed>3)return new(x,y,true,false);
        if(pattern==SocialPattern.Orbit)angle+=Math.Sin(elapsed*.6)*1.5;
        if(pattern==SocialPattern.Peek&&elapsed<2)radius+=80;
        var tx=cursorX+Math.Cos(angle)*radius-58;var ty=cursorY+Math.Sin(angle)*radius-72;
        var arrived=Math.Abs(tx-x)+Math.Abs(ty-y)<15;
        return new(tx,ty,arrived,pattern==SocialPattern.SitNearby&&arrived);
    }
    public static bool Pausing(MovementParameters parameters,double elapsed)=>elapsed>1&&elapsed%6<parameters.PauseFrequency*1.8;
}
