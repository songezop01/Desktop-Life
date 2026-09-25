using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class HomeAffordanceTests
{
    [Theory]
    [InlineData(FurnitureKind.Box,FurnitureUse.Hide|FurnitureUse.Rest|FurnitureUse.Play)]
    [InlineData(FurnitureKind.Scratcher,FurnitureUse.Scratch|FurnitureUse.Stretch)]
    [InlineData(FurnitureKind.PetBed,FurnitureUse.Rest|FurnitureUse.Sleep|FurnitureUse.Social)]
    [InlineData(FurnitureKind.Desk,FurnitureUse.Platform|FurnitureUse.Observe)]
    [InlineData(FurnitureKind.CatTree,FurnitureUse.Platform|FurnitureUse.Observe|FurnitureUse.Rest|FurnitureUse.Play)]
    public void SemanticMapping(FurnitureKind kind,FurnitureUse uses)=>Assert.Equal(uses,FurnitureAffordance.For(kind));
    [Fact] public void MovingFurnitureDoesNotOfferStableTargets()
    {var context=new FurnitureContext(new(Guid.NewGuid(),FurnitureKind.Box,0,0),FurnitureAffordance.For(FurnitureKind.Box),[],false);Assert.False(context.Offers(FurnitureUse.Hide));}
    [Fact] public void IdentitySurvivesMoveAndCopyGetsOwnId()
    {var item=new RoomItem(Guid.NewGuid(),FurnitureKind.Box,0,0);Assert.Equal(item.Id,(item with{X=20}).Id);Assert.NotEqual(item.Id,(item with{Id=Guid.NewGuid()}).Id);}
}
