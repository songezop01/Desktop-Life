using System.Windows;
namespace DesktopLife.App;

public partial class MainWindow
{
    private void SuspendCharacterPresence(Window surface)
    {
        if(surface==Pet)
        {
            runningAction?.Stop();runningAction=null;careUntil=0;Learning.Trace.Clear();
            lastLearningTick=lifeClock.Elapsed.TotalSeconds;Pet.SuspendPresence();
        }
        else if(surface is PetWindow character&&secondaryCharacters.FirstOrDefault(resident=>resident.Window==character) is {} runtime)
            runtime.SuspendPresence();
        else surface.Hide();
    }
}
