using DesktopLife.App;
using DesktopLife.Core;
using Xunit;

namespace DesktopLife.Tests;

public class NavigationIntentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WaitingBetweenApproachFramesDoesNotBecomeANavigationFailure(bool shelfDestination)
    {
        var bounds=new BodyBounds(0,0,1535.6,816);
        var shelf=new RoomPlatform(211,140,788.8,788.8);
        var pet=new PetWindow(bounds,[shelf],422.467172204581,816);
        var goalX=shelfDestination?223:900;
        var goalFeet=shelfDestination?shelf.Y:816;
        for(var i=0;i<10;i++)pet.MoveTo(goalX,goalFeet);
        Assert.Equal(NavigationPhase.Approaching,pet.MovementPhase);
        var before=pet.Position;
        for(var i=0;i<70;i++)pet.WaitWithoutMovement();
        Assert.Equal(before,pet.Position);
        Assert.Equal(0,pet.NavigationFailures);
        for(var i=0;i<1600;i++)
        {
            pet.MoveTo(goalX,goalFeet);
            if(pet.MovementPhase==NavigationPhase.Idle&&Math.Abs(pet.Position.X-goalX)<3)break;
        }
        Assert.Equal(NavigationPhase.Idle,pet.MovementPhase);
        Assert.InRange(Math.Abs(pet.Position.X-goalX),0,3);
        Assert.InRange(Math.Abs(pet.Position.Y+144-goalFeet),0,4);
        Assert.Equal(0,pet.NavigationFailures);
    }

    [Fact]
    public void RequestedMovementThatCannotAdvanceStillTriggersRecovery()
    {
        var bounds=new BodyBounds(0,0,1535.6,816);
        var pet=new PetWindow(bounds,[],400,816);
        for(var i=0;i<32&&pet.MovementPhase!=NavigationPhase.Recovering;i++)
            pet.MoveTo(900,816,.1,0);
        Assert.Equal(NavigationPhase.Recovering,pet.MovementPhase);
        Assert.Equal(1,pet.NavigationFailureReasons.GetValueOrDefault("no-progress"));
    }
}
