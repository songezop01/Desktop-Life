namespace DesktopLife.Core;

public enum RoomActivity { Feed, HairCare, Draw, Write, Computer, Lego, Read, Rest }
public static class RoomActivityPolicy
{
    // A furniture sprite may be dragged above or below its floor. Standing beside
    // it still requires that floor's physical support, not the image's bottom edge.
    public static double StandingFeet(RoomItem item,HouseLayout? house,BodyBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(item);
        if(house is null)return bounds.Top+bounds.Height;
        if(item.FloorIndex<0||item.FloorIndex>=house.FloorCount)throw new ArgumentOutOfRangeException(nameof(item));
        return house.Floors[item.FloorIndex].Y;
    }
    public static FurnitureUse Use(RoomActivity activity)=>activity switch
    {RoomActivity.Feed=>FurnitureUse.Eat,RoomActivity.HairCare=>FurnitureUse.HairCare,RoomActivity.Draw=>FurnitureUse.Draw,RoomActivity.Write=>FurnitureUse.Write,RoomActivity.Computer=>FurnitureUse.Compute,RoomActivity.Lego=>FurnitureUse.Build,RoomActivity.Read=>FurnitureUse.Read,_=>FurnitureUse.Rest};
    public static bool Allowed(PetAppearance character,RoomActivity activity)=>Enum.IsDefined(activity)&&character switch
    {
        PetAppearance.Girl=>true,
        PetAppearance.Cat=>activity==RoomActivity.Feed,
        PetAppearance.BorderCollie=>activity is RoomActivity.Feed or RoomActivity.Rest,
        _=>false
    };
    public static int Rank(PetAppearance character,RoomItem item,RoomActivity activity,IReadOnlyList<RoomItem> room)
    {
        var priority=FurnitureCompatibility.Priority(character,item.Kind,Use(activity));
        if(priority==int.MaxValue)return priority;
        if(character==PetAppearance.Girl&&activity==RoomActivity.Feed&&item.Kind is FurnitureKind.DiningTable or FurnitureKind.Desk)
            return priority+(NearbyChair(item,room) is null?1:0);
        return priority;
    }
    public static RoomItem? NearbyChair(RoomItem item,IReadOnlyList<RoomItem> room)=>room
        .Where(c=>c.Kind==FurnitureKind.Chair&&c.FloorIndex==item.FloorIndex&&Math.Abs(c.X-item.X)<260&&Math.Abs(c.Y-item.Y)<180)
        .OrderBy(c=>Math.Abs(c.X-item.X)+Math.Abs(c.Y-item.Y)).FirstOrDefault();
}
