using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    private void ObserveNavigationProgress(double dt)
    {
        // A route can remain ready while a walk pauses, a social response waits,
        // or a sequence inspects its destination. Only requested movement can stall.
        var attempting=navigationStepped&&MovementPhase==NavigationPhase.Approaching;
        if(navigationProgress.Stalled(body.X,body.Y,dt,attempting))RecoverNavigation();
    }
}
