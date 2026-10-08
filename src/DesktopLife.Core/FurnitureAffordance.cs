namespace DesktopLife.Core;

[Flags]
public enum FurnitureUse { None=0, Platform=1, Rest=2, Sleep=4, Hide=8, Scratch=16, Observe=32, Play=64, Social=128, Stretch=256, Eat=512, HairCare=1024, Draw=2048, Write=4096, Compute=8192, Build=16384, Read=32768 }
public sealed record FurnitureContext(RoomItem Item, FurnitureUse Uses, IReadOnlyList<RoomPlatform> Platforms, bool Stable=true)
{
    public bool Offers(FurnitureUse use)=>Stable&&(Uses&use)==use;
}
public static class FurnitureAffordance
{
    public static FurnitureUse For(FurnitureKind kind)=>kind switch
    {
        FurnitureKind.CatTree=>FurnitureUse.Platform|FurnitureUse.Observe|FurnitureUse.Rest|FurnitureUse.Play,
        FurnitureKind.Bookshelf=>FurnitureUse.Platform|FurnitureUse.Observe|FurnitureUse.Rest|FurnitureUse.Sleep|FurnitureUse.Read,
        FurnitureKind.Desk=>FurnitureUse.Platform|FurnitureUse.Observe|FurnitureUse.Rest|FurnitureUse.Eat|FurnitureUse.HairCare|FurnitureUse.Draw|FurnitureUse.Write|FurnitureUse.Read|FurnitureUse.Build,
        FurnitureKind.HumanBed or FurnitureKind.Sofa=>FurnitureUse.Platform|FurnitureUse.Observe|FurnitureUse.Rest|FurnitureUse.Sleep|FurnitureUse.Read,
        FurnitureKind.Chair=>FurnitureUse.Platform|FurnitureUse.Observe|FurnitureUse.Rest|FurnitureUse.HairCare|FurnitureUse.Eat|FurnitureUse.Read,
        FurnitureKind.DiningTable=>FurnitureUse.Platform|FurnitureUse.Observe|FurnitureUse.Eat,
        FurnitureKind.Computer=>FurnitureUse.Compute|FurnitureUse.Observe,
        FurnitureKind.DrawingBook=>FurnitureUse.Draw|FurnitureUse.Write|FurnitureUse.Observe,
        FurnitureKind.LegoBox=>FurnitureUse.Build|FurnitureUse.Observe|FurnitureUse.Play,
        FurnitureKind.CatBowl=>FurnitureUse.Eat,
        FurnitureKind.Slide=>FurnitureUse.Platform|FurnitureUse.Play,
        FurnitureKind.Cushion or FurnitureKind.PetBed=>FurnitureUse.Rest|FurnitureUse.Sleep|FurnitureUse.Social,
        FurnitureKind.Box=>FurnitureUse.Hide|FurnitureUse.Rest|FurnitureUse.Play,
        FurnitureKind.Scratcher=>FurnitureUse.Scratch|FurnitureUse.Stretch,
        FurnitureKind.Yarn or FurnitureKind.BellBall or FurnitureKind.ToyMouse=>FurnitureUse.Play,
        _=>FurnitureUse.None
    };
}
