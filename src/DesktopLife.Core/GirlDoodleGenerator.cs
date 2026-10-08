namespace DesktopLife.Core;
public enum DoodleTemplate { Heart, Star, Sun, Cloud, Flower, Smile, CatFace, House, Moon, Tree, Person, Rabbit, Butterfly, Cup, Rainbow, Border }
public static class GirlDoodleGenerator
{
    public static string Label(DoodleTemplate t)=>new[]{"愛心","星星","太陽","雲朵","花朵","笑臉","貓臉","小屋","月亮","樹","小人","兔子","蝴蝶","杯子","彩虹","小花邊"}[(int)t];
    public static DoodleTemplate Select(BehaviorParameters p,BehaviorVariationState memory)
    {
        var random=new Random(p.Seed);var types=Enum.GetValues<DoodleTemplate>();
        double Weight(DoodleTemplate t)
        {
            var lively=t is DoodleTemplate.Star or DoodleTemplate.Smile or DoodleTemplate.CatFace or DoodleTemplate.Rabbit or DoodleTemplate.Butterfly;
            var quiet=t is DoodleTemplate.Cloud or DoodleTemplate.Flower or DoodleTemplate.Moon or DoodleTemplate.House;
            var weight=1+(lively?p.Drives.Playfulness*2:quiet?p.Drives.Calmness*2:p.Drives.Creativity);
            return weight/(1+4*memory.RecentOutputHistory.TakeLast(5).Count(h=>h.Signature=="doodle:"+t));
        }
        var weights=types.Select(Weight).ToArray();var draw=random.NextDouble()*weights.Sum();
        for(var i=0;i<types.Length;i++){draw-=weights[i];if(draw<=0)return types[i];}return types[^1];
    }
    public static GeneratedDrawing Generate(DoodleTemplate template,int seed,double creativity=.5,double calm=.5)
    {
        if(!Enum.IsDefined(template)||!double.IsFinite(creativity)||!double.IsFinite(calm))throw new ArgumentOutOfRangeException(nameof(template));
        creativity=Math.Clamp(creativity,0,1);calm=Math.Clamp(calm,0,1);var r=new Random(seed);var strokes=new List<DoodlePoint[]>();
        void Line(params double[] xy)=>strokes.Add(Enumerable.Range(0,xy.Length/2).Select(i=>new DoodlePoint(xy[i*2],xy[i*2+1])).ToArray());
        void Arc(double x,double y,double rx,double ry,double start=0,double end=Math.PI*2)=>strokes.Add(Enumerable.Range(0,33).Select(i=>{var a=start+(end-start)*i/32;return new DoodlePoint(x+rx*Math.Cos(a),y+ry*Math.Sin(a));}).ToArray());
        void Star(double x,double y,double radius)=>strokes.Add(Enumerable.Range(0,11).Select(i=>{var a=-Math.PI/2+i*Math.PI/5;var rad=i%2==0?radius:radius*.43;return new DoodlePoint(x+Math.Cos(a)*rad,y+Math.Sin(a)*rad);}).ToArray());
        void Face(){Arc(39,45,2,3);Arc(61,45,2,3);Arc(50,52,13,10,0,Math.PI);}
        switch(template)
        {
            case DoodleTemplate.Heart:strokes.Add(Enumerable.Range(0,65).Select(i=>{var a=i*Math.PI/32;return new DoodlePoint(50+32*Math.Pow(Math.Sin(a),3),48-2*(13*Math.Cos(a)-5*Math.Cos(2*a)-2*Math.Cos(3*a)-Math.Cos(4*a)));}).ToArray());break;
            case DoodleTemplate.Star:Star(50,50,34);break;
            case DoodleTemplate.Sun:Arc(50,50,23,23);for(var i=0;i<10;i++){var a=i*Math.PI/5;Line(50+Math.Cos(a)*29,50+Math.Sin(a)*29,50+Math.Cos(a)*38,50+Math.Sin(a)*38);}Face();break;
            case DoodleTemplate.Cloud:Arc(32,51,17,16,Math.PI/2,Math.PI*1.6);Arc(50,39,20,19,Math.PI,Math.PI*2);Arc(71,52,15,15,-Math.PI/2,Math.PI/2);Line(32,67,71,67);break;
            case DoodleTemplate.Flower:var petals=seed%2==0?5:6;for(var i=0;i<petals;i++){var a=i*Math.PI*2/petals;Arc(50+15*Math.Cos(a),39+15*Math.Sin(a),11,11);}Arc(50,39,8,8);Line(50,61,50,87);Line(50,76,32,65,36,79,50,82);break;
            case DoodleTemplate.Smile:Arc(50,50,33,33);Face();break;
            case DoodleTemplate.CatFace:Line(19,43,20,16,38,29);Line(62,29,80,16,81,43);Arc(50,52,31,26);Arc(39,48,2,4);Arc(61,48,2,4);Line(47,58,53,58,50,62,47,58);Line(50,62,43,66);Line(50,62,57,66);Line(30,57,13,52);Line(30,62,12,64);Line(70,57,87,52);Line(70,62,88,64);break;
            case DoodleTemplate.House:Line(16,45,50,16,84,45,16,45);Line(24,46,24,84,76,84,76,46);Line(44,84,44,62,58,62,58,84);Line(30,54,39,54,39,65,30,65,30,54);break;
            case DoodleTemplate.Moon:Arc(50,49,34,34,Math.PI*.3,Math.PI*1.7);Arc(70,49,30,28,Math.PI*.6,Math.PI*1.4);Star(78,27,7);break;
            case DoodleTemplate.Tree:Arc(50,40,28,27);Line(44,67,42,87,59,87,56,67);Line(50,67,50,51,39,44);Line(50,56,64,43);break;
            case DoodleTemplate.Person:Arc(50,27,13,13);Line(50,40,50,65,31,85);Line(50,65,69,85);Line(24,45,50,50,76,42);Arc(46,25,1,1);Arc(54,25,1,1);Arc(50,29,5,4,0,Math.PI);break;
            case DoodleTemplate.Rabbit:Arc(37,30,9,21);Arc(63,30,9,21);Arc(50,62,28,24);Arc(39,57,2,3);Arc(61,57,2,3);Line(47,65,53,65,50,69,47,65);Arc(50,70,8,6,0,Math.PI);break;
            case DoodleTemplate.Butterfly:Arc(32,36,18,23);Arc(68,36,18,23);Arc(33,68,16,18);Arc(67,68,16,18);Arc(50,51,5,31);Line(48,21,41,12);Line(52,21,59,12);break;
            case DoodleTemplate.Cup:Line(25,40,30,79,67,79,72,40,25,40);Arc(73,56,14,12,-Math.PI/2,Math.PI/2);Line(19,84,80,84);for(var i=0;i<3;i++)Line(36+i*11,31,32+i*11,23,36+i*11,15);break;
            case DoodleTemplate.Rainbow:for(var i=0;i<4;i++)Arc(50,68,36-i*6,36-i*6,Math.PI,Math.PI*2);Line(12,71,37,71);Line(63,71,88,71);break;
            case DoodleTemplate.Border:Line(20,15,15,15,15,85,85,85,85,15,80,15);for(var i=0;i<5;i++)Star(25+i*12,17,4);Line(27,40,73,40);Line(27,52,66,52);Line(27,64,72,64);break;
        }
        if(creativity>.65&&calm<.7&&template is not (DoodleTemplate.Border or DoodleTemplate.Rainbow))Star(86,85,5);
        var angle=(r.NextDouble()-.5)*.10;var scale=.88+r.NextDouble()*.06;var dx=(r.NextDouble()-.5)*3;var dy=(r.NextDouble()-.5)*3;
        var transformed=strokes.Select(stroke=>stroke.Select(p=>{var x=(p.X-50)*scale;var y=(p.Y-50)*scale;return new DoodlePoint((50+x*Math.Cos(angle)-y*Math.Sin(angle)+dx+(r.NextDouble()-.5)*.35)*1.6,(50+x*Math.Sin(angle)+y*Math.Cos(angle)+dy+(r.NextDouble()-.5)*.35)*1.6);}).ToArray()).ToArray();
        var drawing=new GeneratedDrawing(transformed,160,160,2.1,seed%5<0?0:seed%5);drawing.Validate();return drawing;
    }
}
