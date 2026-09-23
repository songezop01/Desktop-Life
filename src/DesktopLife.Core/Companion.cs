namespace DesktopLife.Core;

public enum CareKind { Feed, Pet, Play, Rest, Groom }
public sealed record PetMemory(DateTimeOffset At, string Text);
public sealed class CompanionState
{
    public string Name { get; set; } = "栗子";
    public double Bond { get; set; } = 15;
    public int CareCount { get; set; }
    public List<PetMemory> Memories { get; set; } = [];
    public Dictionary<CareKind, DateTimeOffset> LastCare { get; set; } = [];
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Name)||Name.Length>16||Name.Any(char.IsControl)||!double.IsFinite(Bond)||Bond<0||Bond>100||CareCount<0
            ||Memories is null||Memories.Count>30||Memories.Any(m=>m is null||string.IsNullOrWhiteSpace(m.Text)||m.Text.Length>120)
            ||LastCare is null||LastCare.Keys.Any(k=>!Enum.IsDefined(k)))throw new InvalidDataException("陪伴資料無效。");
    }
    public string Relationship => Bond<25?"正在認識你":Bond<50?"信任的玩伴":Bond<80?"黏人的家人":"最安心的依靠";
    public void Remember(DateTimeOffset now,string text)
    { Memories.Add(new(now,text));if(Memories.Count>30)Memories.RemoveRange(0,Memories.Count-30); }
}
public sealed record CareResult(PetState State, bool Accepted, string Message, BodyAction Action);
public static class CompanionCare
{
    public static CareResult Apply(PetState state,CompanionState companion,CareKind kind,DateTimeOffset now)
    {
        state.Validate();companion.Validate();if(!Enum.IsDefined(kind))throw new ArgumentOutOfRangeException(nameof(kind));
        var cooldown=kind==CareKind.Feed?60:kind==CareKind.Pet?8:20;
        if(companion.LastCare.TryGetValue(kind,out var last)&&now-last<TimeSpan.FromSeconds(cooldown))return new(state,false,"讓牠慢慢享受，稍後再來吧。",BodyAction.Idle);
        if(kind==CareKind.Feed&&state.Hunger<12)return new(state,false,"肚子還飽飽的，先把點心留著。",BodyAction.Idle);
        if(kind==CareKind.Play&&(state.Energy<20||state.Fatigue>85))return new(state,false,"現在有點累，先讓牠睡一會兒吧。",BodyAction.Sleep);
        static double C(double x)=>Math.Clamp(x,0,100);
        var next=kind switch
        {
            CareKind.Feed=>state with{Hunger=C(state.Hunger-28),Energy=C(state.Energy+8),Mood=C(state.Mood+4)},
            CareKind.Pet=>state with{Loneliness=C(state.Loneliness-12),Mood=C(state.Mood+5)},
            CareKind.Play=>state with{Boredom=C(state.Boredom-25),Loneliness=C(state.Loneliness-10),Mood=C(state.Mood+8),Energy=C(state.Energy-4)},
            CareKind.Groom=>state with{Mood=C(state.Mood+6),Loneliness=C(state.Loneliness-8)},
            _=>state
        };
        var (text,action)=kind switch
        {
            CareKind.Feed=>("啊嗚……這個好好吃！",BodyAction.Eat),
            CareKind.Pet=>(companion.Bond>50?"最喜歡待在你身邊了。":"呼嚕……再摸一下。",BodyAction.Nuzzle),
            CareKind.Play=>("你丟我追，換我了！",BodyAction.PlayToy),
            CareKind.Groom=>("毛毛變得好舒服。",BodyAction.Groom),
            _=>("我睡一下，你也記得休息。",BodyAction.Sleep)
        };
        companion.LastCare[kind]=now;companion.Bond=C(companion.Bond+(kind==CareKind.Pet?.25:.6));companion.CareCount++;
        companion.Remember(now,kind switch{CareKind.Feed=>"你準備了一份飯飯。",CareKind.Pet=>"你溫柔地摸了摸牠。",CareKind.Play=>"你們一起玩球。",CareKind.Groom=>"你幫牠整理毛毛。",_=>"你陪牠安穩入睡。"});
        return new(next,true,text,action);
    }
    public static PetState Step(PetState p,TimeSpan elapsed,BodyAction action,bool present)
    {
        p.Validate();if(elapsed<TimeSpan.Zero||!double.IsFinite(elapsed.TotalSeconds))throw new ArgumentOutOfRangeException(nameof(elapsed));
        var m=Math.Min(elapsed.TotalSeconds,10)/60;
        var sleep=action==BodyAction.Sleep;var play=action is BodyAction.PlayToy or BodyAction.ChaseCursor;
        static double C(double v)=>Math.Clamp(v,0,100);
        return p with{Hunger=C(p.Hunger+m*.55),Energy=C(p.Energy+m*(sleep?2:play?-.9:-.35)),Fatigue=C(p.Fatigue+m*(sleep?-3:.45)),
            Boredom=C(p.Boredom+m*(play?-2:.3)),Loneliness=C(p.Loneliness+m*(present?-.12:.35)),
            Mood=C(p.Mood+m*(p.Hunger>80?-.4:play?.4:0)),Excitement=C(p.Excitement+m*(play?1:-.2))};
    }
    public static string Mood(PetState p)=>p.Fatigue>75||p.Energy<25?"睏睏的":p.Hunger>65?"想吃飯飯":p.Loneliness>60?"想你陪陪":p.Boredom>65?"想找點樂子":p.Mood>75?"心滿意足":"安心陪伴";
}
