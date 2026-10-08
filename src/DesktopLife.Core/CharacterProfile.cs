namespace DesktopLife.Core;

public sealed record CharacterProfile
{
    public PetAppearance Kind {get;init;}
    public PetSnapshot Pet {get;init;}=new();
    public PersonalityProfile Personality {get;init;}=new();
    public LearningState Learning {get;init;}=new();
    public RoomPoint? Position {get;init;}
    public void Validate()
    {
        if(!Enum.IsDefined(Kind)||Pet is null||Personality is null||Learning is null)throw new InvalidDataException("角色資料無效。");
        Pet.Validate();Personality.Validate();Learning.Validate();
        if(Position is {} p&&(!double.IsFinite(p.X)||!double.IsFinite(p.Y)))throw new InvalidDataException("角色位置無效。");
    }
    public static string DefaultName(PetAppearance kind)=>kind switch
    {
        PetAppearance.Girl=>"女孩",
        PetAppearance.Cat=>"栗子",
        PetAppearance.BorderCollie=>"邊牧",
        _=>throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static CharacterProfile Create(PetAppearance kind,DateTimeOffset now)
    {
        if(!Enum.IsDefined(kind))throw new ArgumentOutOfRangeException(nameof(kind));
        return new()
        {
            Kind=kind,
            Pet=new(){LastSaveTime=now},
            Personality=kind switch
            {
                PetAppearance.Girl=>new(){Creativity=.75,Playfulness=.35,Social=.5},
                PetAppearance.BorderCollie=>new(){Creativity=0,Curiosity=.72,Playfulness=.8,Social=.8,Independence=.3,Laziness=.2},
                _=>new()
            },
            Learning=new(){Companion=new(){Name=DefaultName(kind)}}
        };
    }

    public static CharacterProfile CreateOther(PetAppearance original,DateTimeOffset now)
    {
        if(!Enum.IsDefined(original))throw new ArgumentOutOfRangeException(nameof(original));
        return Create(original==PetAppearance.Cat?PetAppearance.Girl:PetAppearance.Cat,now);
    }
}
