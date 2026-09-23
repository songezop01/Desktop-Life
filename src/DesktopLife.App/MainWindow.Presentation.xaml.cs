using DesktopLife.Core;
using System.Windows.Controls;
namespace DesktopLife.App;
public partial class MainWindow
{
    private bool presentationReady;
    private void InitializePresentation()
    {
        Priorities.ItemsSource=Enum.GetValues<DisplayPriority>();Priorities.SelectedItem=settings.DisplayPriority;
        Appearances.ItemsSource=Enum.GetValues<PetAppearance>();Appearances.SelectedItem=settings.PetAppearance;
        Pet.SetAppearance(settings.PetAppearance);presentationReady=true;
    }
    private void ChangePresentation(object sender,SelectionChangedEventArgs e)
    {
        if(!presentationReady)return;
        settings=settings with {DisplayPriority=(DisplayPriority)Priorities.SelectedItem,PetAppearance=(PetAppearance)Appearances.SelectedItem};
        settingsStore.Save(settings);Pet.SetAppearance(settings.PetAppearance);UpdatePetVisibility();
    }
    public async Task SmokePresentation()
    {
        foreach(var appearance in Enum.GetValues<PetAppearance>())
        {
            Appearances.SelectedItem=appearance;Pet.SetAction(BodyAction.Idle);Pet.UpdateLayout();
            if(Pet.InputHitTest(new System.Windows.Point(60,112)) is null)throw new Exception("Appearance hitbox missing.");
            foreach(var action in Enum.GetValues<BodyAction>())Pet.SetAction(action);
        }
        foreach(var priority in Enum.GetValues<DisplayPriority>())
        {
            Priorities.SelectedItem=priority;
            if(Pet.Topmost!=(priority!=DisplayPriority.Desktop)||Pet.Art.Topmost!=Pet.Topmost)throw new Exception("Overlay priorities differ.");
            var saved=settingsStore.Load();
            if(saved.DisplayPriority!=priority || saved.PetAppearance!=PetAppearance.Cat)throw new Exception("Presentation settings not persisted.");
        }
        Priorities.SelectedItem=DisplayPriority.Highest;
        userHidden=true;UpdatePetVisibility();
        if(Pet.IsVisible||Pet.Art.IsVisible)throw new Exception("Manual hide did not hide all surfaces.");
        userHidden=false;UpdatePetVisibility();
        if(!Pet.IsVisible||!Pet.Art.IsVisible)throw new Exception("Highest priority did not show all surfaces.");
        SmokeSurfaceOrder();
        var probe=new System.Windows.Window{Title="桌寵顯示層級測試",Width=300,Height=200,ShowInTaskbar=false};
        try
        {
            probe.Show();var probeHandle=new System.Windows.Interop.WindowInteropHelper(probe).Handle;probe.Activate();probe.WindowState=System.Windows.WindowState.Maximized;probe.UpdateLayout();await Task.Delay(150);probe.Activate();
            if(!DesktopLife.Windows.FullscreenDetector.Inspect(probeHandle).Maximized)throw new Exception("Native maximized detection failed.");
            Priorities.SelectedItem=DisplayPriority.Medium;UpdatePetVisibility(DesktopLife.Windows.FullscreenDetector.Inspect(probeHandle));
            if(Pet.IsVisible||Pet.Art.IsVisible)throw new Exception("Maximized application did not hide overlays.");
            probe.WindowState=System.Windows.WindowState.Normal;probe.UpdateLayout();await Task.Delay(150);probe.Activate();UpdatePetVisibility(DesktopLife.Windows.FullscreenDetector.Inspect(probeHandle));
            if(Pet.Surfaces.Any(w=>!w.IsVisible))throw new Exception("Restored window required a desktop click.");
            probe.WindowState=System.Windows.WindowState.Normal;probe.WindowStyle=System.Windows.WindowStyle.None;
            probe.ResizeMode=System.Windows.ResizeMode.NoResize;probe.Left=0;probe.Top=0;
            probe.Width=System.Windows.SystemParameters.PrimaryScreenWidth;probe.Height=System.Windows.SystemParameters.PrimaryScreenHeight;
            probe.UpdateLayout();await Task.Delay(150);probe.Activate();
            if(!DesktopLife.Windows.FullscreenDetector.Inspect(probeHandle).Fullscreen)throw new Exception("Native fullscreen detection failed.");
            Priorities.SelectedItem=DisplayPriority.High;UpdatePetVisibility(DesktopLife.Windows.FullscreenDetector.Inspect(probeHandle));
            if(Pet.IsVisible||Pet.Art.IsVisible)throw new Exception("Fullscreen application did not hide overlays.");
            Priorities.SelectedItem=DisplayPriority.Highest;
            if(!Pet.IsVisible||!Pet.Art.IsVisible)throw new Exception("Highest priority failed above borderless fullscreen.");
            Priorities.SelectedItem=DisplayPriority.Desktop;await Task.Delay(150);UpdatePetVisibility(DesktopLife.Windows.FullscreenDetector.Inspect(probeHandle));
            if(Pet.Surfaces.Any(w=>!w.IsVisible||w.Topmost||!DesktopLife.Windows.FullscreenDetector.IsBelow(new System.Windows.Interop.WindowInteropHelper(w).Handle,probeHandle)))throw new Exception("Desktop surfaces not below the application: "+string.Join(";",Pet.Surfaces.Select(w=>$"{w.Title} visible={w.IsVisible} top={w.Topmost} below={DesktopLife.Windows.FullscreenDetector.IsBelow(new System.Windows.Interop.WindowInteropHelper(w).Handle,probeHandle)}")));
            SmokeSurfaceOrder();
            probe.WindowState=System.Windows.WindowState.Minimized;UpdatePetVisibility();
            if(Pet.Surfaces.Any(w=>!w.IsVisible))throw new Exception("Minimizing required a desktop click.");
        }
        finally {probe.Close();Priorities.SelectedItem=DisplayPriority.Highest;}
        Pet.SetAction(BodyAction.Idle);
    }
    private void SmokeSurfaceOrder()
    {
        var state=new ForegroundState(false,false,true);
        UpdatePetVisibility(state);
        var ordered=SurfaceBackToFront();
        static nint Handle(System.Windows.Window window)=>new System.Windows.Interop.WindowInteropHelper(window).Handle;
        for(var i=1;i<ordered.Length;i++)
            if(!DesktopLife.Windows.FullscreenDetector.IsBelow(Handle(ordered[i-1]),Handle(ordered[i])))
                throw new Exception($"Room depth order reversed: {ordered[i-1].Title} / {ordered[i].Title}.");
        var changes=DesktopLife.Windows.DesktopInteraction.ZOrderChanges;
        for(var i=0;i<5;i++)UpdatePetVisibility(state);
        if(DesktopLife.Windows.DesktopInteraction.ZOrderChanges!=changes)throw new Exception("Stable room repeatedly changed native z-order.");
        // Exercise both directions; previously PlaceAbove duplicated PlaceBehind.
        var first=Handle(ordered[0]);var second=Handle(ordered[1]);
        DesktopLife.Windows.DesktopInteraction.PlaceBehind(second,first);
        if(!DesktopLife.Windows.FullscreenDetector.IsBelow(second,first))throw new Exception("PlaceBehind did not place the surface behind.");
        DesktopLife.Windows.DesktopInteraction.PlaceAbove(second,first);
        if(!DesktopLife.Windows.FullscreenDetector.IsBelow(first,second))throw new Exception("PlaceAbove did not place the surface above.");
        UpdatePetVisibility(state);
    }
}
