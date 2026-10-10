namespace DesktopLife.App;

public partial class PetWindow
{
    private bool simulationEnabled=true;
    internal void SetSimulationEnabled(bool enabled)
    {
        simulationEnabled=enabled;
        if(!enabled){timer.Stop();SnapPresentation();return;}
        previous=clock.Elapsed.TotalSeconds;
        if(worldOwner is null||IsVisible)timer.Start();
    }
}
