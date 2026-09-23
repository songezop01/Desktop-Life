using DesktopLife.Core;
using DesktopLife.Windows;
using System.Windows;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace DesktopLife.App;
public partial class MainWindow
{
    private Forms.NotifyIcon? tray;
    private readonly DispatcherTimer visibilityTimer=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private bool userHidden,aiPaused;
    private void InitializeTray()
    {
        var menu=new Forms.ContextMenuStrip();
        void Add(string title,Action action)=>menu.Items.Add(title,null,(_,_)=>Dispatcher.Invoke(action));
        Add("顯示角色",()=>{userHidden=false;UpdatePetVisibility();});
        Add("隱藏角色",()=>{userHidden=true;UpdatePetVisibility();});
        Add("暫停",()=>SetPaused(true));Add("繼續",()=>SetPaused(false));
        Add("控制台",()=>{Show();WindowState=WindowState.Normal;Activate();});
        Add("清除作品",()=>{Pet.Art.ClearWorks();SavePetState();});
        Add("重設位置",()=>Pet.ResetPosition());
        Add("恢復桌面圖示排列",()=>RestoreDesktopIcons(this,new RoutedEventArgs()));
        Add("餵飯",()=>Care(CareKind.Feed));Add("摸摸",()=>Care(CareKind.Pet));Add("陪玩",()=>Care(CareKind.Play));
        var layers=new Forms.ToolStripMenuItem("顯示層級");
        foreach(var priority in Enum.GetValues<DisplayPriority>())layers.DropDownItems.Add(UiText.Label(priority),null,(_,_)=>Dispatcher.Invoke(()=>Priorities.SelectedItem=priority));
        menu.Items.Add(layers);
        var looks=new Forms.ToolStripMenuItem("桌寵外觀");
        foreach(var appearance in Enum.GetValues<PetAppearance>())looks.DropDownItems.Add(UiText.Label(appearance),null,(_,_)=>Dispatcher.Invoke(()=>Appearances.SelectedItem=appearance));
        menu.Items.Add(looks);Add("結束",()=>RequestExit());
        tray=new Forms.NotifyIcon{Text="Desktop Life",Icon=System.Drawing.SystemIcons.Application,ContextMenuStrip=menu,Visible=true};
        tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{Show();WindowState=WindowState.Normal;Activate();});
        visibilityTimer.Tick+=(_,_)=>{UpdatePetVisibility();};visibilityTimer.Start();UpdatePetVisibility();
        StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized)Hide();};
        Closed+=(_,_)=>{visibilityTimer.Stop();tray.Visible=false;tray.Dispose();menu.Dispose();};
    }
    private void SetPaused(bool paused)
    {
        aiPaused=paused;Learning.Trace.Clear();Pet.SetPaused(paused);
        if(!paused){lastLearningTick=lifeClock.Elapsed.TotalSeconds;runningAction=null;}
        HitStatus.Text=paused?"自主行為已暫停；生理與感測持續更新，暫不接受獎勵。":"自主行為已恢復。";
    }
    private void UpdatePetVisibility(ForegroundState? diagnosticState=null)
    {
        var visible=DisplayPolicy.Visible(settings.DisplayPriority,diagnosticState??FullscreenDetector.Read(DisplayWorkspace.Active.Handle),userHidden);
        foreach(var surface in Pet.Surfaces)
        {
            var topmost=settings.DisplayPriority!=DisplayPriority.Desktop;
            if(surface.Topmost!=topmost)surface.Topmost=topmost;
            if(visible)
            {
                if(!surface.IsVisible)surface.Show();
            }
            else if(surface.IsVisible)surface.Hide();
        }
        if(visible)
        {
            var ordered=SurfaceBackToFront();
            static nint Handle(Window surface)=>new System.Windows.Interop.WindowInteropHelper(surface).Handle;
            // Anchor the whole group once. Anchoring every window separately
            // reverses the group on every visibility tick and causes flicker.
            if(settings.DisplayPriority==DisplayPriority.Desktop && !FullscreenDetector.PlaceAtDesktopLevel(Handle(ordered[0])))
            {
                foreach(var surface in ordered)surface.Hide();
                return;
            }
            for(var i=1;i<ordered.Length;i++)DesktopInteraction.PlaceAbove(Handle(ordered[i]),Handle(ordered[i-1]));
        }
        if(!visible)Learning.Trace.Clear();
    }

    private Window[] SurfaceBackToFront()
    {
        var actors=new[]{(Window)Pet}.Concat(Pet.AllToys).ToArray();
        static double Feet(Window surface)=>surface is ToyWindow toy?toy.Model.Y+InteractiveToy.Size:surface.Top+surface.Height;
        static double Center(Window surface)=>surface is ToyWindow toy?toy.Model.X+InteractiveToy.Size/2:surface.Left+surface.Width/2;
        double Depth(Window surface)
        {
            var depth=Feet(surface);
            if(surface is RoomWindow furniture)
            {
                foreach(var actor in actors)
                {
                    var feet=Feet(actor);var center=Center(actor);
                    if(furniture.Platforms.Any(p=>center>=p.X&&center<=p.X+p.Width&&Math.Abs(feet-p.HeightAt(center))<=6))
                        depth=Math.Min(depth,feet-.5);
                }
            }
            return depth;
        }
        return new[]{(Window)Pet.Art}.Concat(Pet.Furniture.Cast<Window>().Concat(actors).OrderBy(Depth)).ToArray();
    }

    private void PauseAi(object sender,RoutedEventArgs e)=>SetPaused(!aiPaused);
}
