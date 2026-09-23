namespace DesktopLife.Core;
public interface INoteGenerator { string Generate(PetState state); }
public sealed class TemplateNoteGenerator : INoteGenerator
{
    public string Generate(PetState s)=>s.Hunger>65?"有點餓了……":s.Loneliness>60?"主人去哪裡了？":s.Fatigue>70?"休息一下再出發。":s.Excitement>65?"好多能量！":s.Mood>70?"今天好開心！":s.Boredom>60?"好無聊，來畫點什麼吧！":"一起慢慢探索吧。";
}
public enum DoodlePattern { Heart, Star, Smile, Sun, Circle, SimpleFace, RandomLines }
public readonly record struct DoodlePoint(double X,double Y);
public static class DoodleGenerator
{
    public static IReadOnlyList<IReadOnlyList<DoodlePoint>> Generate(DoodlePattern pattern,int seed)
    {
        List<IReadOnlyList<DoodlePoint>> strokes=new();
        List<DoodlePoint> Circle(double x,double y,double radius,double start=0,double end=Math.PI*2)=>Enumerable.Range(0,33).Select(i=>new DoodlePoint(x+Math.Cos(start+(end-start)*i/32)*radius,y+Math.Sin(start+(end-start)*i/32)*radius)).ToList();
        switch(pattern)
        {
            case DoodlePattern.Heart:strokes.Add(Enumerable.Range(0,65).Select(i=>{var t=i*Math.PI*2/64;return new DoodlePoint(50+2.5*16*Math.Pow(Math.Sin(t),3),48-2.5*(13*Math.Cos(t)-5*Math.Cos(2*t)-2*Math.Cos(3*t)-Math.Cos(4*t)));}).ToList());break;
            case DoodlePattern.Star:strokes.Add(Enumerable.Range(0,11).Select(i=>{var angle=-Math.PI/2+i*Math.PI/5;var r=i%2==0?42:18;return new DoodlePoint(50+Math.Cos(angle)*r,50+Math.Sin(angle)*r);}).ToList());break;
            case DoodlePattern.Circle:strokes.Add(Circle(50,50,40));break;
            case DoodlePattern.Sun:strokes.Add(Circle(50,50,25));for(var i=0;i<12;i++){var a=i*Math.PI/6;strokes.Add([new(50+Math.Cos(a)*32,50+Math.Sin(a)*32),new(50+Math.Cos(a)*45,50+Math.Sin(a)*45)]);}break;
            case DoodlePattern.Smile:case DoodlePattern.SimpleFace:
                strokes.Add(Circle(50,50,42));strokes.Add(Circle(35,40,3));strokes.Add(Circle(65,40,3));strokes.Add(Circle(50,48,22,0,Math.PI));
                if(pattern==DoodlePattern.SimpleFace)strokes.Add([new(48,46),new(45,55),new(52,55)]);break;
            case DoodlePattern.RandomLines:var random=new Random(seed);strokes.Add(Enumerable.Range(0,9).Select(_=>new DoodlePoint(random.Next(10,90),random.Next(10,90))).ToList());break;
        }
        return strokes;
    }
}
public sealed record CreativeWork(DoodlePattern Pattern,string? Text,double X,double Y,int Seed,DateTimeOffset CreatedAt,bool Keep=false,GeneratedDrawing? Drawing=null);
