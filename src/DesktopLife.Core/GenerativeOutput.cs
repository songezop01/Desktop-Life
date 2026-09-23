namespace DesktopLife.Core;

public sealed record GeneratedDrawing(DoodlePoint[][] Strokes,double Width,double Height,double StrokeWidth,int ColorIndex)
{
    public void Validate()
    {
        if(!double.IsFinite(Width)||Width is <40 or >240||!double.IsFinite(Height)||Height is <40 or >200||!double.IsFinite(StrokeWidth)||StrokeWidth is <1 or >5||ColorIndex is <0 or >4||Strokes is null||Strokes.Length is <1 or >64||Strokes.Any(s=>s is null||s.Length is <1 or >65)||Strokes.Sum(s=>s.Length)>4096
            ||Strokes.SelectMany(s=>s).Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||p.X<0||p.Y<0||p.X>Width||p.Y>Height))throw new InvalidDataException("程序繪畫資料超出限制。");
    }
}
public static class ProceduralDrawing
{
    public static GeneratedDrawing Generate(BehaviorParameters parameters)
    {
        parameters.Validate();var p=parameters.Draw;var random=new Random(parameters.Seed);
        var width=80+150*p.Size;var height=60+130*p.Size;var strokes=new List<DoodlePoint[]>();
        for(var i=0;i<p.StrokeCount;i++)
        {
            var kind=i==0?p.Primary:random.NextDouble()<p.Symbols*.3?(random.NextDouble()<.5?PrimitiveKind.Eye:PrimitiveKind.Heart)
                :random.NextDouble()<p.Geometry?((PrimitiveKind[])[PrimitiveKind.Line,PrimitiveKind.Circle,PrimitiveKind.Polygon,PrimitiveKind.Star])[random.Next(4)]
                :((PrimitiveKind[])[PrimitiveKind.Arc,PrimitiveKind.Spiral,PrimitiveKind.Ellipse,PrimitiveKind.Mouth,PrimitiveKind.Dot])[random.Next(5)];
            var radius=(.07+.19*p.StrokeLength)*(1-.4*p.Density)*(i==0?1.4:.5+random.NextDouble());
            var x=.1+random.NextDouble()*.8;var y=.1+random.NextDouble()*.8;
            var angle=p.Direction*Math.PI*2+(random.NextDouble()-.5)*parameters.Drives.Mischief*4;
            var local=Primitive(kind,p.Curvature,p.Complexity);
            DoodlePoint[] Transform(bool mirror)=>local.Select(v=>
            {
                var a=v.X*Math.Cos(angle)-v.Y*Math.Sin(angle);var b=v.X*Math.Sin(angle)+v.Y*Math.Cos(angle);
                var px=Math.Clamp(x+a*radius,.025,.975);var py=Math.Clamp(y+b*radius,.025,.975);
                return new DoodlePoint((mirror?1-px:px)*width,py*height);
            }).ToArray();
            var stroke=Transform(false);strokes.Add(stroke);
            if(random.NextDouble()<p.Symmetry)strokes.Add(Transform(true));
            if(i>0&&random.NextDouble()<p.Complexity*.45)strokes.Add([strokes[0][0],stroke[0]]);
            if(strokes.Count>=60)break;
        }
        var result=new GeneratedDrawing(strokes.Take(64).ToArray(),width,height,1.2+p.Density*2,(int)(parameters.Drives.Playfulness*4));result.Validate();return result;
    }
    private static DoodlePoint[] Primitive(PrimitiveKind kind,double curvature,double complexity)
    {
        var count=16+(int)(complexity*32);
        DoodlePoint[] Curve(Func<double,DoodlePoint> point)=>Enumerable.Range(0,count+1).Select(i=>point(i/(double)count)).ToArray();
        return kind switch
        {
            PrimitiveKind.Line=>Curve(t=>new(2*t-1,Math.Sin(t*Math.PI)*curvature*.5)),
            PrimitiveKind.Arc or PrimitiveKind.Mouth=>Curve(t=>new(Math.Cos(t*Math.PI),Math.Sin(t*Math.PI)*(kind==PrimitiveKind.Mouth?.45:1))),
            PrimitiveKind.Spiral=>Curve(t=>new(t*Math.Cos(t*Math.PI*(4+complexity*4)),t*Math.Sin(t*Math.PI*(4+complexity*4)))),
            PrimitiveKind.Heart=>Curve(t=>{var a=t*Math.PI*2;return new(Math.Pow(Math.Sin(a),3),-(13*Math.Cos(a)-5*Math.Cos(2*a)-2*Math.Cos(3*a)-Math.Cos(4*a))/17);}),
            PrimitiveKind.Star=>Enumerable.Range(0,11).Select(i=>{var a=i*Math.PI/5;var r=i%2==0?1:.4;return new DoodlePoint(r*Math.Cos(a),r*Math.Sin(a));}).ToArray(),
            PrimitiveKind.Polygon=>Enumerable.Range(0,7).Select(i=>new DoodlePoint(Math.Cos(i*Math.PI/3),Math.Sin(i*Math.PI/3))).ToArray(),
            _=>Curve(t=>new(Math.Cos(t*Math.PI*2)*(kind==PrimitiveKind.Dot?.12:1),Math.Sin(t*Math.PI*2)*(kind==PrimitiveKind.Dot?.12:kind is PrimitiveKind.Ellipse or PrimitiveKind.Eye?.5:1)))
        };
    }
}

public sealed record ComposedNote(string Text,string Signature);
public static class ComposableText
{
    private static readonly string[][] Emotions=[
        ["心裡暖暖的","有一點開心","感覺輕飄飄的","忍不住微笑"],
        ["有一點無聊","想找點新鮮事","快要發芽了","有點坐不住"],
        ["有一點想你","想靠近一點","想找個伴","覺得四周很安靜"],
        ["充滿精神","想跳一下","有好多點子","像裝了小馬達"],
        ["有點害羞","想悄悄說話","還在鼓起勇氣","有點不好意思"],
        ["有點悶悶的","想安靜一下","心裡有一小團雲","想整理一下心情"]];
    private static readonly string[] Objects=["一幅小畫","一顆星星","桌角的風景","一條彎彎的線","那顆圓球","一個奇怪的形狀","一個小祕密","一片想像的天空"];
    private static readonly string[][] Intents=[
        ["陪你看看","和你分享","一起欣賞","陪你研究"],
        ["碎念一下","抱怨兩句","嘀咕幾句","發牢騷"],
        ["想到","發現","畫了","收藏了"],
        ["畫","看看","研究","找找"],
        ["想像","觀察","研究","回想"]];
    private static readonly string[] Modifiers=["慢慢地","悄悄地","認真地","開心地","偷偷地","悠閒地"];
    private static readonly string[] Endings=["。","呢。","呀。","喔。","，可以嗎？","，就一下下。"];
    public static ComposedNote Generate(BehaviorParameters parameters,BehaviorVariationState memory)
    {
        parameters.Validate();var random=new Random(parameters.Seed);var p=parameters.Write;
        string Pick(string[] parts)=>parts[random.Next(parts.Length)];
        ComposedNote candidate=new("","text");
        for(var attempt=0;attempt<24;attempt++)
        {
            var address=p.Intent==TextIntent.Reflect?"":p.Direct>.55?Pick(["主人，","嘿，主人，","你看，"]):Pick(["嗯，","那個……",""]);
            var emotion=p.Emotional>.4&&p.Length>.35?"我"+Pick(Emotions[(int)p.Emotion])+"，":"";
            var verb=Pick(Intents[(int)p.Intent]);var obj=Pick(Objects);
            var modifier=p.Length>.55?Pick(Modifiers):"";
            var ending=p.Direct<.35?"，如果你願意。":Pick(Endings);
            var clause=p.Intent switch
            {
                TextIntent.Company=>"想"+modifier+verb+obj,
                TextIntent.Complain=>"想"+modifier+"對"+obj+verb,
                TextIntent.Share=>"剛剛"+modifier+verb+obj,
                TextIntent.Invite=>"要不要一起"+modifier+verb+obj,
                _=>"正在"+modifier+verb+obj
            };
            var text=address+emotion+(emotion.Length==0&&p.Intent!=TextIntent.Invite?"我":"")+clause+ending;
            if(p.Length>.7)text+=p.Playful>.55?Pick(["它說它不是方塊，是迷路的月亮。","這一定是桌面精靈留下的線索。","別告訴圓球，我比較喜歡星星。"]):Pick(["待在這裡也很舒服。","不急，我們慢慢來。","今天就先記住這一刻。"]);
            else if(p.Playful>.65)text+=Pick(["嘿嘿。","這算祕密喔。","被你發現了！"]);
            candidate=new(text,$"sentence:{p.Intent}:{verb}:{obj}");
            if(!memory.RecentOutputHistory.Any(e=>e.Text==text||e.Signature==candidate.Signature))return candidate;
        }
        return candidate;
    }
}
