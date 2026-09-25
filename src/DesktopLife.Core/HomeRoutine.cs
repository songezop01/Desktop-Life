namespace DesktopLife.Core;

/// <summary>Small bounded tendencies, evaluated by the existing brain clock.</summary>
public sealed class HomeRoutine(int seed)
{
    private readonly Random random=new(seed);
    private double cooldown=20,active,playCooldown;
    public bool PlayCooling=>playCooldown>0;
    public void Advance(double dt,BodyAction action)
    {
        dt=Math.Clamp(dt,0,1);cooldown=Math.Max(0,cooldown-dt);playCooldown=Math.Max(0,playCooldown-dt);
        active=action==BodyAction.Sleep?0:Math.Min(900,active+dt);
    }
    public void Finished(SequenceKind kind){if(kind==SequenceKind.Play)playCooldown=75;}
    public BodyAction? Opportunity(PetState state,PersonalityProfile p,FurnitureUse available,bool quiet,bool unstable)
    {
        if(unstable||cooldown>0)return null;
        cooldown=30+random.NextDouble()*35;
        if(state.Fatigue>80||state.Energy<25||active>300)return BodyAction.Sleep;
        var choices=new List<(BodyAction Action,double Weight)>();
        if((available&FurnitureUse.Hide)!=0)choices.Add((BodyAction.Hide,.5+p.Curiosity));
        if(!quiet&&(available&FurnitureUse.Scratch)!=0)choices.Add((BodyAction.Stretch,.3+p.Playfulness+state.Boredom/100));
        if((available&FurnitureUse.Observe)!=0)choices.Add((BodyAction.ObserveCursor,.5+p.Curiosity));
        if((available&FurnitureUse.Rest)!=0)choices.Add((BodyAction.Sleep,.15+p.Laziness*.4));
        if(!quiet&&state.Loneliness>40)choices.Add((BodyAction.Nuzzle,p.Social));
        if(choices.Count==0)return null;
        var draw=random.NextDouble()*choices.Sum(c=>c.Weight);
        foreach(var choice in choices){draw-=choice.Weight;if(draw<=0)return choice.Action;}return choices[^1].Action;
    }
}
