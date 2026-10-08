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
    public static int WakeVariant(PersonalityProfile p,Random random)
    {var weights=new[]{.6+p.Social,.4+1.4*p.Laziness+.5*p.Independence,.4+1.8*p.Curiosity};var draw=random.NextDouble()*weights.Sum();for(var i=0;i<3;i++){draw-=weights[i];if(draw<=0)return i;}return 2;}
    public static double TransitionTendency(LifeBehavior? previous,BodyAction action,PersonalityProfile p,double bond)
    {
        var next=BehaviorTransitionPreference.Classify(action);
        return previous switch
        {
            LifeBehavior.Wake=>next switch{LifeBehavior.Explore=>.25*p.Curiosity+.1*p.Independence,LifeBehavior.Groom=>.18*p.Laziness,LifeBehavior.Social=>.3*p.Social*(.5+Math.Clamp(bond,0,100)/200)*(1-.4*p.Independence),_=>0},
            LifeBehavior.Play=>next==LifeBehavior.Scratch?.18*p.Playfulness:next==LifeBehavior.Rest?.15*p.Laziness:0,
            LifeBehavior.Scratch=>next==LifeBehavior.Rest?.18*p.Laziness:0,
            LifeBehavior.Explore=>next==LifeBehavior.Observe?.18*p.Curiosity:0,
            LifeBehavior.Eat=>next==LifeBehavior.Groom?.15:0,
            _=>0
        };
    }
    public void Finished(SequenceKind kind){if(kind==SequenceKind.Play)playCooldown=75;}
    public BodyAction? Opportunity(PetState state,PersonalityProfile p,FurnitureUse available,bool quiet,bool unstable,BehaviorTransitionPreference? transitions=null,DateTimeOffset? now=null,double bond=15)
    {
        if(unstable||cooldown>0)return null;
        cooldown=30+random.NextDouble()*35;
        if(state.Fatigue>80||state.Energy<25||active>300)return BodyAction.Sleep;
        var choices=new List<(BodyAction Action,double Weight)>();
        if(!quiet&&(available&FurnitureUse.Draw)!=0)choices.Add((BodyAction.DrawDoodle,.2+2*p.Creativity*p.Creativity));
        if(!quiet&&(available&(FurnitureUse.Build|FurnitureUse.Read|FurnitureUse.Compute))!=0)choices.Add((BodyAction.PlayToy,.2+p.Playfulness+p.Curiosity));
        if((available&FurnitureUse.Hide)!=0)choices.Add((BodyAction.Hide,.2+2*p.Curiosity*p.Curiosity));
        if(!quiet&&(available&FurnitureUse.Scratch)!=0)choices.Add((BodyAction.Stretch,.2+p.Playfulness*1.4+state.Boredom/200));
        if((available&FurnitureUse.Observe)!=0)choices.Add((BodyAction.ObserveCursor,.2+2*p.Curiosity*p.Curiosity));
        if((available&FurnitureUse.Rest)!=0)choices.Add((BodyAction.Sleep,.15+p.Laziness*p.Laziness*2));
        if(!quiet&&state.Loneliness>25)choices.Add((BodyAction.Nuzzle,.15+p.Social*p.Social*2*(.6+bond/250)*(1-.4*p.Independence)));
        if(!quiet&&!PlayCooling&&(available&FurnitureUse.Play)!=0)choices.Add((BodyAction.PlayToy,.15+2*p.Playfulness*p.Playfulness));
        for(var i=0;i<choices.Count;i++){var c=choices[i];choices[i]=(c.Action,c.Weight*Math.Exp(2*((transitions?.Bias(c.Action,now??DateTimeOffset.UtcNow)??0)+TransitionTendency(transitions?.Previous,c.Action,p,bond))));}
        if(choices.Count==0)return null;
        var draw=random.NextDouble()*choices.Sum(c=>c.Weight);
        foreach(var choice in choices){draw-=choice.Weight;if(draw<=0)return choice.Action;}return choices[^1].Action;
    }
}
