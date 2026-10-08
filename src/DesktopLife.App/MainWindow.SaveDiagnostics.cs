using System.IO;
using System.Windows.Threading;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    public void FreezeRestartVerification()
    {
        verificationFrozen=true;
        lifeTimer.Stop();learningTimer.Stop();visibilityTimer.Stop();SetPaused(true);Hide();
        Pet.SetSimulationEnabled(false);
        foreach(var window in characterWindows)window.SetSimulationEnabled(false);
    }
    public async Task SmokeBackgroundSaving(string root)
    {
        var isolatedRoot=Path.Combine(root,"background-saving");var store=new OrganismStore(isolatedRoot);
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var hold=0;var fail=0;var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var isolated=new MainWindow(isolatedRoot,new(){CloseBehavior=CloseBehavior.Exit},snapshot=>
        {
            if(Volatile.Read(ref hold)!=0){entered.Set();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new IOException("Diagnostic writer timed out");}
            if(Volatile.Read(ref fail)!=0)throw new IOException("Diagnostic save failure");
            snapshot.SaveTo(store);
        });
        isolated.Closed+=(_,_)=>closed.TrySetResult();
        try
        {
            isolated.Show();isolated.Hide();
            if(!await isolated.SavePetStateAsync())throw new Exception("Diagnostic initial save failed");
            Volatile.Write(ref hold,1);
            var pending=isolated.SavePetStateAsync();
            if(!await Task.Run(()=>entered.Wait(TimeSpan.FromSeconds(5))))throw new Exception("Background writer did not start");
            var dispatcherRan=false;
            await Dispatcher.InvokeAsync(()=>dispatcherRan=true,DispatcherPriority.Background);
            if(!dispatcherRan||pending.IsCompleted)throw new Exception("Saving blocked the dispatcher or acknowledged before durable write");
            release.Set();Volatile.Write(ref hold,0);
            if(!await pending)throw new Exception("Background save did not finish");
            var preserved=File.ReadAllText(store.PathName);
            Volatile.Write(ref fail,1);isolated.RequestExit();
            for(var i=0;i<100&&isolated.saveClosing;i++)await Task.Delay(20);
            if(closed.Task.IsCompleted||isolated.saveClosing||!isolated.IsEnabled||!isolated.IsVisible)throw new Exception("Failed save did not retain a usable window");
            if(File.ReadAllText(store.PathName)!=preserved)throw new Exception("Failed save changed the previous valid file");
            Volatile.Write(ref fail,0);entered.Reset();release.Reset();Volatile.Write(ref hold,1);isolated.RequestExit();
            if(!await Task.Run(()=>entered.Wait(TimeSpan.FromSeconds(5))))throw new Exception("Final background writer did not start");
            var trayChangedState=false;isolated.InvokeTrayAction(()=>trayChangedState=true);
            if(trayChangedState||isolated.tray?.ContextMenuStrip?.Enabled!=false)throw new Exception("Tray still permitted changes after final snapshot");
            release.Set();Volatile.Write(ref hold,0);
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var saved=store.Load();saved.Validate();
            if(saved.OtherCharacter is null||saved.AdditionalCharacter is null)throw new Exception("Final save lost a resident");
            File.WriteAllText(Path.Combine(root,"background-save-check.json"),System.Text.Json.JsonSerializer.Serialize(new{UiResponsiveDuringBlockedWrite=true,FailureRetainedWindowAndPreviousSave=true,RetryExitedAfterDurableSave=true,Characters=3,Metrics=isolated.saveQueue.CaptureMetrics()}));
        }
        finally
        {
            release.Set();Volatile.Write(ref fail,0);Volatile.Write(ref hold,0);
            if(!closed.Task.IsCompleted){isolated.RequestExit();await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));}
        }
    }
}
