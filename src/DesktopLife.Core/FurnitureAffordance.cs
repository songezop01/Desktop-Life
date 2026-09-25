namespace DesktopLife.Core;

[Flags]
public enum FurnitureUse { None=0, Platform=1, Rest=2, Sleep=4, Hide=8, Scratch=16, Observe=32, Play=64, Social=128, Stretch=256 }
public sealed record FurnitureContext(RoomItem Item, FurnitureUse Uses, IReadOnlyList<RoomPlatform> Platforms, bool Stable=true)
{
    public bool Offers(FurnitureUse use)=>Stable&&(Uses&use)==use;
}
public static class FurnitureAffordance
{
    public static FurnitureUse For(FurnitureKind kind)=>kind switch
    {
        FurnitureKind.CatTree=>FurnitureUse.Platform|FurnitureUse.Observe|FurnitureUse.Rest|FurnitureUse.Play,
        FurnitureKind.Bookshelf=>FurnitureUse.Platform|FurnitureUse.Observe|FurnitureUse.Rest,
        FurnitureKind.Desk=>FurnitureUse.Platform|FurnitureUse.Observe,
        FurnitureKind.Slide=>FurnitureUse.Platform|FurnitureUse.Play,
        FurnitureKind.Cushion or FurnitureKind.PetBed=>FurnitureUse.Rest|FurnitureUse.Sleep|FurnitureUse.Social,
        FurnitureKind.Box=>FurnitureUse.Hide|FurnitureUse.Rest|FurnitureUse.Play,
        FurnitureKind.Scratcher=>FurnitureUse.Scratch|FurnitureUse.Stretch,
        FurnitureKind.Yarn or FurnitureKind.BellBall or FurnitureKind.ToyMouse=>FurnitureUse.Play,
        _=>FurnitureUse.None
    };
}
