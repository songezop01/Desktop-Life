namespace DesktopLife.Core;

// Both retains its original cat-and-girl meaning for existing saves.
public enum PresenceMode { CatOnly = 0, GirlOnly = 1, Both = 2, DogOnly = 3, All = 4 }
public static class PresencePolicy
{
    public static bool Includes(PresenceMode mode,PetAppearance kind)=>Enum.IsDefined(kind)&&mode switch
    {
        PresenceMode.CatOnly=>kind==PetAppearance.Cat,
        PresenceMode.GirlOnly=>kind==PetAppearance.Girl,
        PresenceMode.Both=>kind is PetAppearance.Cat or PetAppearance.Girl,
        PresenceMode.DogOnly=>kind==PetAppearance.BorderCollie,
        PresenceMode.All=>true,
        _=>false
    };

    public static PresenceMode Only(PetAppearance kind)=>kind switch
    {
        PetAppearance.Cat=>PresenceMode.CatOnly,
        PetAppearance.Girl=>PresenceMode.GirlOnly,
        PetAppearance.BorderCollie=>PresenceMode.DogOnly,
        _=>throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

// Runtime-only reservations. Never serialize occupants or share a behavior sequence.
public sealed class HouseholdOccupancy
{
    private readonly Dictionary<string,PetAppearance> owners=[];
    public bool Available(string target,PetAppearance who)=>!owners.TryGetValue(target,out var owner)||owner==who;
    public bool TryAcquire(string target,PetAppearance who)
    {if(!Available(target,who))return false;owners[target]=who;return true;}
    public void Release(PetAppearance who)
    {foreach(var key in owners.Where(x=>x.Value==who).Select(x=>x.Key).ToArray())owners.Remove(key);}
    public void Release(string target,PetAppearance who)
    {if(owners.TryGetValue(target,out var owner)&&owner==who)owners.Remove(target);}
    public int Count=>owners.Count;
}

public enum OtherPresenceReaction { None, ObserveOther, AvoidOverlap, BriefApproach }
public static class OtherPresence
{
    public static OtherPresenceReaction Decide(double x,double y,double otherX,double otherY,bool committed,double social,double sample,double overlapDistance=85)
    {
        if(committed||Math.Abs(y-otherY)>80)return OtherPresenceReaction.None;
        var distance=Math.Abs(x-otherX);
        if(distance<overlapDistance)return OtherPresenceReaction.AvoidOverlap;
        if(distance<300)return sample<social*.25?OtherPresenceReaction.BriefApproach:OtherPresenceReaction.ObserveOther;
        return OtherPresenceReaction.None;
    }
}
