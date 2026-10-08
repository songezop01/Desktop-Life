namespace DesktopLife.Core;

/// <summary>Identity constraints are applied before preferences, habits, or navigation.</summary>
public static class FurnitureCompatibility
{
    public static bool CanUse(PetAppearance character, FurnitureKind furniture, FurnitureUse use)
    {
        if (!Enum.IsDefined(character) || !Enum.IsDefined(furniture) || use == FurnitureUse.None)
            return false;
        var offered = FurnitureAffordance.For(furniture);
        if ((offered & use) != use) return false;
        return character switch
        {
            PetAppearance.Cat => (use & (FurnitureUse.HairCare | FurnitureUse.Draw | FurnitureUse.Write | FurnitureUse.Compute | FurnitureUse.Build | FurnitureUse.Read)) == 0
                && ((use & FurnitureUse.Eat) == 0 || furniture == FurnitureKind.CatBowl),
            PetAppearance.Girl => furniture switch
            {
                FurnitureKind.CatTree or FurnitureKind.PetBed or FurnitureKind.Box or
                FurnitureKind.Scratcher or FurnitureKind.Yarn or FurnitureKind.ToyMouse or FurnitureKind.CatBowl => false,
                // A shelf top and desk top are cat resting places, not human seats.
                FurnitureKind.Bookshelf => (use & (FurnitureUse.Rest | FurnitureUse.Sleep | FurnitureUse.Platform)) == 0,
                FurnitureKind.Desk => (use & (FurnitureUse.Sleep | FurnitureUse.Platform)) == 0,
                _ => (use & (FurnitureUse.Scratch | FurnitureUse.Hide)) == 0
            },
            PetAppearance.BorderCollie => furniture switch
            {
                FurnitureKind.CatBowl => use == FurnitureUse.Eat,
                FurnitureKind.BellBall => use == FurnitureUse.Play,
                FurnitureKind.PetBed or FurnitureKind.Cushion or FurnitureKind.HumanBed or FurnitureKind.Sofa
                    => (use & (FurnitureUse.Platform | FurnitureUse.Observe | FurnitureUse.Rest | FurnitureUse.Sleep | FurnitureUse.Social)) == use,
                _ => false
            },
            _ => false
        };
    }

    public static int Priority(PetAppearance character, FurnitureKind furniture, FurnitureUse use)
    {
        if(!CanUse(character,furniture,use))return int.MaxValue;
        if(character==PetAppearance.BorderCollie&&(use is FurnitureUse.Sleep or FurnitureUse.Rest))return furniture switch
        {FurnitureKind.PetBed=>0,FurnitureKind.Cushion=>1,FurnitureKind.Sofa=>2,FurnitureKind.HumanBed=>3,_=>4};
        if(character!=PetAppearance.Girl)return 0;
        if(use is FurnitureUse.Sleep or FurnitureUse.Rest)return furniture switch
        {FurnitureKind.HumanBed=>0,FurnitureKind.Sofa=>1,FurnitureKind.Chair=>2,FurnitureKind.Desk=>3,_=>4};
        if(use==FurnitureUse.Eat)return furniture switch
        {FurnitureKind.DiningTable=>0,FurnitureKind.Desk=>2,FurnitureKind.Chair=>4,_=>6};
        return 0;
    }

    public static string Zone(PetAppearance character,FurnitureKind kind)=>kind switch
    {
        FurnitureKind.HumanBed=>character switch{PetAppearance.Girl=>"main",PetAppearance.BorderCollie=>"dog-foot",_=>"foot"},
        FurnitureKind.Sofa=>character switch{PetAppearance.Girl=>"left",PetAppearance.BorderCollie=>"middle",_=>"right"},
        FurnitureKind.Desk or FurnitureKind.DiningTable=>character==PetAppearance.Girl?"seat":"desktop",
        FurnitureKind.Bookshelf=>character==PetAppearance.Girl?"front":"shelf",
        _=>"shared"
    };
    public static string Reservation(RoomItem item,PetAppearance character)=>item.Id+":"+Zone(character,item.Kind);

    public static FurnitureUse AvailableUses(PetAppearance character, FurnitureKind furniture)
    {
        var result = FurnitureUse.None;
        foreach (var use in Enum.GetValues<FurnitureUse>())
            if (CanUse(character, furniture, use)) result |= use;
        return result;
    }
}
