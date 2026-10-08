namespace DesktopLife.Core;

/// <summary>Owns every persisted mutable collection. Capture on the state-owning thread; write on a worker.</summary>
public sealed class FrozenOrganismSnapshot
{
    private readonly OrganismSnapshot snapshot;
    private FrozenOrganismSnapshot(OrganismSnapshot snapshot)=>this.snapshot=snapshot;

    public static FrozenOrganismSnapshot Capture(OrganismSnapshot value)=>new(value with
    {
        Learning=Copy(value.Learning),
        OtherCharacter=Copy(value.OtherCharacter),
        AdditionalCharacter=Copy(value.AdditionalCharacter)
    });

    // The private graph is never returned to callers or attached to live character state.
    public void SaveTo(OrganismStore store)=>store.Save(snapshot);
    public void ExportTo(string path)=>OrganismStore.WriteSnapshot(snapshot,path);

    private static CharacterProfile? Copy(CharacterProfile? value)=>value is null?null:value with{Learning=Copy(value.Learning)};
    private static LearningState Copy(LearningState value)=>new()
    {
        SchemaVersion=value.SchemaVersion,
        ActionPreference=new(value.ActionPreference),CategoryPreference=new(value.CategoryPreference),ContextAssociation=new(value.ContextAssociation),
        FlyWeights=value.FlyWeights?.Select(row=>(double[])row.Clone()).ToArray(),
        ConnectomeFingerprint=value.ConnectomeFingerprint,ConnectomeDeltas=value.ConnectomeDeltas is {} deltas?(double[])deltas.Clone():null,
        PositiveRewards=value.PositiveRewards,Punishments=value.Punishments,
        Artworks=value.Artworks.Select(work=>work with{Drawing=work.Drawing is {} drawing?drawing with{Strokes=drawing.Strokes.Select(stroke=>(DoodlePoint[])stroke.Clone()).ToArray()}:null}).ToList(),
        LocationHabits=value.LocationHabits.ToList(),
        Adaptation=new(){Version=value.Adaptation.Version,LastMinute=value.Adaptation.LastMinute,Traits=value.Adaptation.Traits.ToDictionary(pair=>pair.Key,pair=>new TraitEvidence{Offset=pair.Value.Offset,Evidence=pair.Value.Evidence,LastEventMinute=pair.Value.LastEventMinute})},
        Transitions=new(){Pairs=value.Transitions.Pairs.ToList()},
        Milestones=new(){Seen=value.Milestones.Seen.ToList(),LastMinute=value.Milestones.LastMinute,LastReason=value.Milestones.LastReason},
        Room=new(){FloorCount=value.Room.FloorCount,Items=value.Room.Items.ToList(),Ball=value.Room.Ball,Square=value.Room.Square,WorkArea=value.Room.WorkArea},
        Companion=new(){Name=value.Companion.Name,Bond=value.Companion.Bond,CareCount=value.Companion.CareCount,Memories=value.Companion.Memories.ToList(),LastCare=new(value.Companion.LastCare)},
        Variation=new(){DrawStylePreference=new(value.Variation.DrawStylePreference),TextStylePreference=new(value.Variation.TextStylePreference),MovementStylePreference=new(value.Variation.MovementStylePreference),ShapePreference=new(value.Variation.ShapePreference),RecentOutputHistory=value.Variation.RecentOutputHistory.ToList()}
    };
}
