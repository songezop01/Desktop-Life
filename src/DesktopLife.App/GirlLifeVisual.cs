using System.Windows;
using System.Windows.Media;
using DesktopLife.Core;
namespace DesktopLife.App;

/// <summary>Small WPF vector poses sharing the established hair, skin and dress palette.</summary>
public sealed class GirlLifeVisual : FrameworkElement
{
    private RoomActivity? activity;
    private BehaviorPhase phase;
    private bool sleep,bed;
    private double time,age;
    private int template;
    private static readonly Dictionary<string,Brush> colors=[];
    private static Brush B(string hex)
    {if(!colors.TryGetValue(hex,out var b)){b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));b.Freeze();colors[hex]=b;}return b;}
    public void Pose(RoomActivity? activity,BehaviorPhase phase,double time,double age,bool sleep,bool bed,int template)
    {this.activity=activity;this.phase=phase;this.time=time;this.age=age;this.sleep=sleep;this.bed=bed;this.template=template;InvalidateVisual();}
    protected override void OnRender(DrawingContext d)
    {
        void Oval(double x,double y,double rx,double ry,string c)=>d.DrawEllipse(B(c),null,new(x,y),rx,ry);
        void Box(double x,double y,double w,double h,string c,double r=3)=>d.DrawRoundedRectangle(B(c),null,new(x,y,w,h),r,r);
        void Line(double x,double y,double ex,double ey,string c,double width=3)=>d.DrawLine(new Pen(B(c),width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new(x,y),new(ex,ey));
        var resting=sleep&&phase is BehaviorPhase.LieDown or BehaviorPhase.Sleep;
        if(resting&&bed)
        {
            var breathe=Math.Sin(time*1.5)*.7;
            Box(4,127,30,15,"#F5ECD8",7);Oval(25,120,20,19,"#635572");Oval(28,121,14,13,"#FFD9C2");
            Box(42,119+breathe,52,22,"#91BFA5",9);Box(89,128,23,12,"#635572",5);
            Line(21,121,25,121,"#493E54",1.5);Line(31,121,35,121,"#493E54",1.5);Line(47,128,73,134,"#FFD9C2",7);return;
        }
        var seated=phase is not (BehaviorPhase.Notice or BehaviorPhase.Approach or BehaviorPhase.Stand);
        var headY=seated?76d:55d;
        var closed=resting||time%4.7<.15;
        Oval(49,headY,26,30,"#635572");Oval(49,headY+3,20,22,"#FFD9C2");
        Oval(32,headY+10,7,23,"#635572");Oval(68,headY+10,7,23,"#635572");
        d.DrawGeometry(B("#79678A"),null,Geometry.Parse($"M 28,{headY-8} Q 37,{headY-32} 60,{headY-17} L 69,{headY-4} Q 50,{headY-7} 42,{headY-17} Q 38,{headY-5} 28,{headY-8}"));
        if(closed){Line(39,headY+5,44,headY+5,"#493E54",1.4);Line(54,headY+5,59,headY+5,"#493E54",1.4);}
        else{Oval(42,headY+5,2,3,"#493E54");Oval(56,headY+5,2,3,"#493E54");}
        Line(47,headY+13,51,headY+13,"#AF7777",1.5);Box(60,headY-16,11,7,"#F6CDB0");
        Box(33,headY+26,33,seated?25:41,"#91BFA5",9);
        if(seated){Line(38,126,57,128,"#635572",8);Line(57,128,58,140,"#635572",7);Line(57,126,75,129,"#635572",8);Line(75,129,76,140,"#635572",7);}
        else{Line(41,125,40,140,"#635572",9);Line(58,125,59,140,"#635572",9);}
        var work=phase==BehaviorPhase.Work;
        double handX=74,handY=113;
        if(activity==RoomActivity.HairCare&&work){handX=66;handY=headY-4+Math.Sin(time*3)*10;}
        else if(activity==RoomActivity.Feed&&work){var f=(Math.Sin(time*3)+1)/2;handX=83-28*f;handY=114-25*f;}
        else if(work){handX=81+Math.Sin(time*5)*6;handY=112+Math.Cos(time*4)*3;}
        Line(36,headY+29,27,headY+42,"#FFD9C2",7);Line(27,headY+42,62,117,"#FFD9C2",7);
        Line(63,headY+29,70,headY+38,"#FFD9C2",7);Line(70,headY+38,handX,handY,"#FFD9C2",7);
        if(phase is BehaviorPhase.Notice or BehaviorPhase.Approach or BehaviorPhase.Sit or BehaviorPhase.Stand)return;
        switch(activity)
        {
            case RoomActivity.Feed:
                Box(62,119,51,5,"#C4A580");Oval(88,116,19,4,"#F8F0DD");Oval(89,114,10,3,"#BF8E59");Box(101,103,10,12,"#C5DDD3");
                Line(handX,handY,handX+7,handY-5,"#85958C",2);break;
            case RoomActivity.HairCare:
                if(work){Box(handX,handY-8,4,17,"#BD9078",1);for(var i=0;i<5;i++)Line(handX+2,handY-7+i*3,handX+8,handY-7+i*3,"#DEC29B",1);}break;
            case RoomActivity.Draw:case RoomActivity.Write:case RoomActivity.Read:
                Box(63,109,47,16,"#F7EDD9",2);Line(84,111,84,123,"#C5B99F",1);
                for(var i=0;i<3;i++)Line(88,113+i*3,104,113+i*3,"#9BAFA3",1);
                if(activity!=RoomActivity.Read){Line(handX,handY,handX+5,handY-13,"#D8AB55",3);Line(68,117,77,113,"#C3948A",1.5);}break;
            case RoomActivity.Computer:
                Box(78,82,34,28,"#637D76");Box(81,85,28,21,"#BADAD0");Box(70,114,43,7,"#C6CCC0");Line(83,115,107,115,"#81968B",1);break;
            case RoomActivity.Lego:
                Box(65,124,47,5,"#C0AF8E");var count=phase==BehaviorPhase.Work?Math.Clamp((int)age,1,6):6;
                for(var i=0;i<count;i++){var x=template%2==0?80+(i%2)*12:67+(i%3)*14;var y=template%2==0?118-i/2*8:118-i/3*9;Box(x,y,12,8,i%3==0?"#ACCAAF":i%3==1?"#D4AC71":"#C79591",1);Box(x+3,y-2,6,3,"#E4CDA7",1);}
                if(template==2){Oval(75,129,4,4,"#67736C");Oval(103,129,4,4,"#67736C");}break;
        }
    }
}
