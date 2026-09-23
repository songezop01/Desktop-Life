namespace DesktopLife.Core;

public enum PrimitiveKind { Line, Arc, Circle, Ellipse, Spiral, Polygon, Dot, Eye, Mouth, Heart, Star }
public enum TextIntent { Company, Complain, Share, Invite, Reflect }
public enum TextEmotion { Happy, Bored, Missing, Excited, Shy, Upset }
public enum SocialPattern { Approach, Observe, Follow, Orbit, SitNearby, Peek, Wait }
public enum StyleFeature { Symmetry, Complexity, Shape, Density, Size, Corner, Length, Direct, Playful, Emotional, Speed, Exploration, CursorAffinity }
public sealed record StyleDrives(double Creativity,double Novelty,double Symmetry,double Complexity,double Social,double Mischief,double Playfulness,double Calmness,double Exploration);
public sealed record DrawParameters(int StrokeCount,double StrokeLength,double Curvature,double Direction,double Symmetry,double Size,double Complexity,double Density,double Corner,double Geometry,double Symbols,PrimitiveKind Primary);
public sealed record WriteParameters(double Length,double Direct,double Playful,double Emotional,TextEmotion Emotion,TextIntent Intent);
public sealed record MovementParameters(double Speed,double Distance,double PauseFrequency,double DirectionChange,double PreferredRegion,double CursorDistance,double ObjectInterest,double PathCurvature);
public sealed record PlayParameters(double Force,double Bounce);
public sealed record SocialParameters(double Affinity,SocialPattern Pattern,bool AvoidBriefly);
public sealed record BehaviorParameters(int Seed,StyleDrives Drives,DrawParameters Draw,WriteParameters Write,MovementParameters Movement,PlayParameters Play,SocialParameters Social)
{
    public static BehaviorParameters Default=>BehaviorDecoder.Decode(BodyAction.Idle,new(new(),new(),null,LearningContext.Unknown),new(new Dictionary<BehaviorDrive,double>(),[],[]),new(),null,0,1);
    public Dictionary<StyleFeature,double> Features(BodyAction action)=>action switch
    {
        BodyAction.DrawDoodle=>new(){[StyleFeature.Symmetry]=Draw.Symmetry,[StyleFeature.Complexity]=Draw.Complexity,[StyleFeature.Shape]=Draw.Geometry,[StyleFeature.Density]=Draw.Density,[StyleFeature.Size]=Draw.Size,[StyleFeature.Corner]=Draw.Corner},
        BodyAction.WriteNote=>new(){[StyleFeature.Length]=Write.Length,[StyleFeature.Direct]=Write.Direct,[StyleFeature.Playful]=Write.Playful,[StyleFeature.Emotional]=Write.Length>.35?Write.Emotional:0},
        BodyAction.Idle or BodyAction.Sleep or BodyAction.Sit or BodyAction.RestInCorner or BodyAction.Hide=>new(){[StyleFeature.Speed]=0,[StyleFeature.CursorAffinity]=0,[StyleFeature.Corner]=action is BodyAction.RestInCorner or BodyAction.Hide?1:Movement.PreferredRegion},
        _=>new(){[StyleFeature.Speed]=(Movement.Speed-20)/130,[StyleFeature.Exploration]=Movement.DirectionChange,[StyleFeature.Corner]=Movement.PreferredRegion,[StyleFeature.CursorAffinity]=Social.Affinity}
    };
    public string Signature(BodyAction action)=>action switch
    {
        BodyAction.DrawDoodle=>$"draw:{Draw.Primary}:{(int)(Draw.Complexity*3)}:{(int)(Draw.Symmetry*3)}",
        BodyAction.WriteNote=>$"text:{Write.Intent}:{Write.Emotion}:{(int)(Write.Length*3)}",
        _=>$"move:{action}:{Social.Pattern}:{(int)(Movement.PreferredRegion*3)}"
    };
    public void Validate()
    {
        if(Drives is null||Draw is null||Write is null||Movement is null||Play is null||Social is null)throw new InvalidDataException("缺少行為參數。");
        var unit=new[]{Drives.Creativity,Drives.Novelty,Drives.Symmetry,Drives.Complexity,Drives.Social,Drives.Mischief,Drives.Playfulness,Drives.Calmness,Drives.Exploration,Draw.StrokeLength,Draw.Curvature,Draw.Direction,Draw.Symmetry,Draw.Size,Draw.Complexity,Draw.Density,Draw.Corner,Draw.Geometry,Draw.Symbols,Write.Length,Write.Direct,Write.Playful,Write.Emotional,Movement.PauseFrequency,Movement.DirectionChange,Movement.PreferredRegion,Movement.ObjectInterest,Movement.PathCurvature,Play.Bounce,Social.Affinity};
        if(unit.Any(v=>!double.IsFinite(v)||v<0||v>1)||Draw.StrokeCount is <2 or >28||!Enum.IsDefined(Draw.Primary)||!Enum.IsDefined(Write.Intent)||!Enum.IsDefined(Write.Emotion)||!Enum.IsDefined(Social.Pattern)
            ||!double.IsFinite(Movement.Speed)||Movement.Speed is <20 or >150||!double.IsFinite(Movement.Distance)||Movement.Distance is <40 or >480||!double.IsFinite(Movement.CursorDistance)||Movement.CursorDistance is <70 or >240||!double.IsFinite(Play.Force)||Play.Force is <60 or >260)
            throw new InvalidDataException("行為參數超出範圍。");
    }
}

public sealed record RecentOutput(BodyAction Action,string Signature,string? Text,DateTimeOffset CreatedAt);
public sealed class BehaviorVariationState
{
    public Dictionary<StyleFeature,double> DrawStylePreference {get;set;}=new();
    public Dictionary<StyleFeature,double> TextStylePreference {get;set;}=new();
    public Dictionary<StyleFeature,double> MovementStylePreference {get;set;}=new();
    public Dictionary<PrimitiveKind,double> ShapePreference {get;set;}=new();
    public List<RecentOutput> RecentOutputHistory {get;set;}=new();
    public Dictionary<StyleFeature,double> Weights(BodyAction action)=>action==BodyAction.DrawDoodle?DrawStylePreference:action==BodyAction.WriteNote?TextStylePreference:MovementStylePreference;
    public void Remember(RecentOutput entry)
    {
        RecentOutputHistory.RemoveAll(e=>entry.CreatedAt-e.CreatedAt>TimeSpan.FromMinutes(30));
        RecentOutputHistory.Add(entry);if(RecentOutputHistory.Count>64)RecentOutputHistory.RemoveAt(0);
    }
    public double Repetition(string signature,DateTimeOffset now)=>RecentOutputHistory.Where(e=>e.Signature==signature).Sum(e=>Math.Exp(-Math.Max(0,(now-e.CreatedAt).TotalSeconds)/120));
    public void Reinforce(BodyAction action,BehaviorParameters parameters,double change)
    {
        var weights=Weights(action);
        foreach(var (feature,value) in parameters.Features(action))weights[feature]=Math.Clamp(weights.GetValueOrDefault(feature)+change*(2*value-1),-.75,.75);
        if(action==BodyAction.DrawDoodle)ShapePreference[parameters.Draw.Primary]=Math.Clamp(ShapePreference.GetValueOrDefault(parameters.Draw.Primary)+change,-.75,.75);
    }
    public void Validate()
    {
        if(DrawStylePreference is null||TextStylePreference is null||MovementStylePreference is null||ShapePreference is null||RecentOutputHistory is null)throw new InvalidDataException("缺少風格記憶。");
        foreach(var weights in new[]{DrawStylePreference,TextStylePreference,MovementStylePreference})
            if(weights.Keys.Any(k=>!Enum.IsDefined(k))||weights.Values.Any(v=>!double.IsFinite(v)||Math.Abs(v)>.75))throw new InvalidDataException("風格偏好無效。");
        if(ShapePreference.Keys.Any(k=>!Enum.IsDefined(k))||ShapePreference.Values.Any(v=>!double.IsFinite(v)||Math.Abs(v)>.75)||RecentOutputHistory.Count>64||RecentOutputHistory.Any(e=>e is null||!Enum.IsDefined(e.Action)||e.Signature is null||e.Signature.Length>100||e.Text?.Length>100))throw new InvalidDataException("風格歷史無效。");
    }
}

public static class BehaviorDecoder
{
    public static BehaviorParameters Decode(BodyAction action,EnvironmentContext context,BrainOutput brain,LearningState learning,RewardEvent? reward,double now,int seed)
    {
        var random=new Random(seed);var p=context.Personality;var state=context.Pet;var memory=learning.Variation;
        double C(double v)=>Math.Clamp(v,0,1);
        double Noise()=> (random.NextDouble()-.5)*.22;
        var recent=reward is null?0:reward.Amount/3*Math.Exp(-Math.Max(0,now-reward.Timestamp)/12);
        var association=learning.ContextAssociation.GetValueOrDefault($"{context.LearningContext}:{action}");
        var preference=learning.ActionPreference.GetValueOrDefault(action);
        var drives=new StyleDrives(C(.4*p.Creativity+.4*brain.Score(BehaviorDrive.Create)+.15*state.Curiosity/100+.05*preference),
            C(.4*p.Curiosity+.35*brain.Score(BehaviorDrive.Explore)+.25*state.Boredom/100),
            C(.2+.4*state.Mood/100+.3*brain.Score(BehaviorDrive.Rest)-.2*p.Mischief),
            C(.4*p.Creativity+.4*brain.Score(BehaviorDrive.Create)+.2*state.Excitement/100),
            C(.4*p.Social+.35*brain.Score(BehaviorDrive.Interact)+.25*brain.Score(BehaviorDrive.Approach)+recent*.05),p.Mischief,
            C(.5*p.Playfulness+.3*brain.Score(BehaviorDrive.Play)+.2*state.Excitement/100),
            C(.4*p.Laziness+.4*brain.Score(BehaviorDrive.Rest)+.2*state.Fatigue/100),
            C(.35*p.Curiosity+.4*brain.Score(BehaviorDrive.Explore)+.25*state.Energy/100));
        double Bias(BodyAction type,StyleFeature feature,double basis)=>C(basis+memory.Weights(type).GetValueOrDefault(feature)*.6+Noise()+association*.04);
        var complexity=Bias(BodyAction.DrawDoodle,StyleFeature.Complexity,drives.Complexity);
        var density=Bias(BodyAction.DrawDoodle,StyleFeature.Density,.25+.45*state.Excitement/100+.2*drives.Creativity);
        var symmetry=Bias(BodyAction.DrawDoodle,StyleFeature.Symmetry,drives.Symmetry);
        var size=Bias(BodyAction.DrawDoodle,StyleFeature.Size,.25+.45*state.Excitement/100-.15*state.Loneliness/100);
        var corner=Bias(BodyAction.DrawDoodle,StyleFeature.Corner,.2+.5*state.Loneliness/100+.15*p.Timidity);
        var geometry=Bias(BodyAction.DrawDoodle,StyleFeature.Shape,.55-.25*p.Mischief);
        var kinds=Enum.GetValues<PrimitiveKind>();
        var weights=kinds.Select(k=>Math.Exp(memory.ShapePreference.GetValueOrDefault(k)*2+(k is PrimitiveKind.Heart or PrimitiveKind.Eye?.4*state.Mood/100:0)+(k is PrimitiveKind.Polygon or PrimitiveKind.Line?geometry:1-geometry)) /
            (1+drives.Novelty*memory.RecentOutputHistory.TakeLast(12).Count(e=>e.Signature.StartsWith($"draw:{k}:")))).ToArray();
        var choice=random.NextDouble()*weights.Sum();var primary=kinds[^1];for(var i=0;i<kinds.Length;i++){choice-=weights[i];if(choice<=0){primary=kinds[i];break;}}
        var draw=new DrawParameters(2+(int)(complexity*(6+16*density)),C(.3+.5*complexity+Noise()),C(.25+.5*state.Mood/100+Noise()),random.NextDouble(),symmetry,size,complexity,density,corner,geometry,C(.2+.5*state.Mood/100),primary);
        var direct=Bias(BodyAction.WriteNote,StyleFeature.Direct,.5+.25*p.Social-.4*p.Timidity);
        var playful=Bias(BodyAction.WriteNote,StyleFeature.Playful,.2+.5*p.Mischief+.25*drives.Playfulness);
        var emotion=state.Mood<25?TextEmotion.Upset:state.Loneliness>65?TextEmotion.Missing:state.Boredom>70?TextEmotion.Bored:state.Excitement>70?TextEmotion.Excited:p.Timidity>.7?TextEmotion.Shy:TextEmotion.Happy;
        var intent=random.NextDouble()<p.Independence*.65?TextIntent.Reflect:random.NextDouble()<drives.Social?TextIntent.Company:(TextIntent)random.Next(5);
        var write=new WriteParameters(Bias(BodyAction.WriteNote,StyleFeature.Length,.45+.3*p.Creativity-.35*p.Timidity),direct,playful,Bias(BodyAction.WriteNote,StyleFeature.Emotional,.2+.5*(1-p.Independence)+recent*.1),emotion,intent);
        var speed=Bias(BodyAction.Walk,StyleFeature.Speed,.25+.4*state.Energy/100+.2*drives.Playfulness-.2*p.Laziness);
        var affinity=Bias(BodyAction.Walk,StyleFeature.CursorAffinity,drives.Social*(1-.5*p.Independence));
        var pattern=(SocialPattern)random.Next(7);
        if(random.NextDouble()>affinity)pattern=random.NextDouble()<.5?SocialPattern.Observe:SocialPattern.Wait;
        var movement=new MovementParameters(20+130*speed,40+440*drives.Exploration,C(.1+.55*drives.Calmness),Bias(BodyAction.Walk,StyleFeature.Exploration,drives.Novelty),Bias(BodyAction.Walk,StyleFeature.Corner,.2+.4*p.Timidity+.2*state.Loneliness/100),70+170*(1-affinity),C(.3+.6*drives.Playfulness),C(.15+.6*p.Mischief+Noise()));
        var result=new BehaviorParameters(seed,drives,draw,write,movement,new(60+200*drives.Playfulness,C(.3+.5*drives.Playfulness)),new(affinity,pattern,recent<-.1));result.Validate();return result;
    }
}
