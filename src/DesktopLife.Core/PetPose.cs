namespace DesktopLife.Core;

public readonly record struct PetPose(double Bob, double Tilt, double ScaleY, double Leg, double Arm, double Tail, bool Sleeping)
{
    public static PetPose At(BodyAction action,double time)
    {
        var run=action is BodyAction.ChaseCursor or BodyAction.AvoidCursor;
        var move=run || action is BodyAction.Walk or BodyAction.Wander or BodyAction.Explore;
        var wave=Math.Sin(time*(run?18:9));
        if(action==BodyAction.Fall)return new(0,Math.Sin(time*5)*4,1.06,-15,65,35,false);
        if(action==BodyAction.BatToy)return new(5,12,.9,-10,55+Math.Sin(time*8)*25,20,false);
        if(action==BodyAction.Eat)return new(9+Math.Sin(time*7)*2,10,.88,12,25+Math.Sin(time*7)*8,Math.Sin(time*2)*8,false);
        if(action==BodyAction.Groom)return new(9,-8+Math.Sin(time*3)*4,.90,20,95+Math.Sin(time*6)*20,Math.Sin(time*2)*12,false);
        if(action==BodyAction.Nuzzle)return new(Math.Sin(time*3)*2,Math.Sin(time*3)*10,.96,4,30,Math.Sin(time*5)*30,false);
        if(action==BodyAction.Greet)return new(-Math.Abs(Math.Sin(time*4))*9,Math.Sin(time*4)*5,1,8,125+Math.Sin(time*9)*25,Math.Sin(time*7)*35,false);
        if(action==BodyAction.Sleep)return new(18,78,.85+Math.Sin(time*2)*.025,0,-15,0,true);
        if(action==BodyAction.Stretch)return new(-5,0,1.08+Math.Sin(time*2)*.06,12,155,30,false);
        if(action is BodyAction.Sit or BodyAction.RestInCorner or BodyAction.Hide)return new(18,0,.78,65,-10,Math.Sin(time*2)*15,false);
        if(action==BodyAction.PlayToy)return new(-Math.Abs(Math.Sin(time*5))*13,Math.Sin(time*5)*12,.95,25*wave,60*wave,40*wave,false);
        if(action is BodyAction.DrawDoodle or BodyAction.WriteNote)return new(0,-8,1,0,65+Math.Sin(time*10)*25,15,false);
        if(action is BodyAction.PseudoPushIcon or BodyAction.PushShortcut)return new(4,18,.94,-15,85,25,false);
        return new(move?-Math.Abs(wave)*(run?7:3):Math.Sin(time*2)*.6,run?13:0,1,move?wave*(run?48:28):0,move?-wave*(run?60:32):0,Math.Sin(time*3)*20,false);
    }
}
