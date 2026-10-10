using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using DesktopLife.Core;
using DesktopLife.Windows;

namespace DesktopLife.App;

public sealed partial class RoomWindow
{
    public bool IsCombTool=>Item.Kind==FurnitureKind.Comb;
    internal bool CombHeld {get;private set;}
    internal Guid CombDragId {get;private set;}
    private Point combOrigin;
    private bool combWasTopmost;
    internal Point CombTip=>new(combOrigin.X+Width*.29,combOrigin.Y+Height-2*SceneScale);
    internal nint NativeHandle=>new WindowInteropHelper(this).Handle;
    internal void CancelCombDrag(){if(IsCombTool&&CombHeld)drag?.Cancel();}
    private void SetCombHeld(bool held)
    {
        if(!IsCombTool||editing)return;
        CombHeld=held;
        if(held){CombDragId=Guid.NewGuid();combWasTopmost=Topmost;Topmost=true;combOrigin=new(Item.X,Item.Y);}
        else {Topmost=combWasTopmost;combOrigin=new(Item.X,Item.Y);DisplayWorkspace.Position(this,Item.X,Item.Y);}
    }
    private void MoveComb(double x,double y)
    {
        if(!IsCombTool||!CombHeld)return;
        var b=DisplayWorkspace.Bounds;
        combOrigin=new(Math.Clamp(x,b.Left,b.Left+Math.Max(0,b.Width-Width)),Math.Clamp(y,b.Top,b.Top+Math.Max(0,b.Height-Height)));
        // The held tool is temporary. The saved furniture remains at its stand.
        DisplayWorkspace.Position(this,combOrigin.X,combOrigin.Y);
    }
    internal void BeginCombDiagnostic()
    {
        UpdateLayout();var p=new Point(120*.9,40*.55);
        var hit=canvas.InputHitTest(p) as UIElement??throw new Exception("Comb painted handle cannot receive input.");
        drag.WithDiagnosticPointer(new(Item.X+p.X*SceneScale,Item.Y+p.Y*SceneScale),()=>hit.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Mouse.MouseDownEvent}));
        if(!CombHeld)throw new Exception("Actual comb drag did not capture.");
    }
    internal void MoveCombDiagnostic(Point tip)
    {
        var origin=new Point(tip.X-Width*.29,tip.Y-Height+2*SceneScale);
        var pointer=new Point(origin.X+drag.PressPoint.X,origin.Y+drag.PressPoint.Y);
        drag.WithDiagnosticPointer(pointer,()=>canvas.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseMoveEvent}));
    }
    internal void ReleaseCombDiagnostic()=>drag.Cancel();
}
