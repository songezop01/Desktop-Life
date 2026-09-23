using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class AffordanceTests
{
    [Fact] public void CursorActionsRequireNearbyCursor(){Assert.False(AffordanceProvider.Allows(BodyAction.ChaseCursor,new(false,true,true,true)));Assert.True(AffordanceProvider.Allows(BodyAction.ChaseCursor,new(true,false,false,false)));}
    [Fact] public void ObjectActionsRequireObjects(){Assert.False(AffordanceProvider.Allows(BodyAction.PseudoPushIcon,new(true,false,true,true)));Assert.False(AffordanceProvider.Allows(BodyAction.PlayToy,new(true,true,false,true)));}
    [Fact] public void VirtualObjectsAreExplicitlyVirtual(){var objects=new VirtualDesktopObjectProvider().GetObjects(new(-1920,0,1920,1000));Assert.All(objects,o=>Assert.StartsWith("virtual-",o.Id));}
    [Fact] public void ChaseCannotLeaveBounds(){var b=new DesktopBody();var bounds=new BodyBounds(-1920,0,1920,1000);b.Reset(bounds);for(var i=0;i<1000;i++)b.MoveToward(10000,-10000,.1,bounds);Assert.InRange(b.X,-1920,-116);Assert.InRange(b.Y,0,856);}
}
