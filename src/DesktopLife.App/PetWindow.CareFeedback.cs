using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    private ToolTip? careFeedback;
    private DispatcherTimer? careFeedbackTimer;
    internal bool CareFeedbackVisible=>careFeedback?.IsOpen==true;
    internal string CareFeedbackText {get;private set;}="";

    // Application feedback is separate from speech: animals still do not speak.
    public void ShowCareFeedback(string message)
    {
        CareFeedbackText=message;
        if(!IsVisible)return;
        if(careFeedback is null)
        {
            careFeedback=new ToolTip
            {
                PlacementTarget=Character,Placement=PlacementMode.Top,StaysOpen=true,
                Background=new SolidColorBrush(Color.FromRgb(255,249,242)),
                Foreground=new SolidColorBrush(Color.FromRgb(100,77,76)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(228,201,197)),
                BorderThickness=new Thickness(1),Padding=new Thickness(12,8,12,8),
                FontSize=14,MaxWidth=270,IsHitTestVisible=false
            };
            careFeedbackTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(6)};
            careFeedbackTimer.Tick+=(_,_)=>DismissCareFeedback();
            IsVisibleChanged+=(_,_)=>{if(!IsVisible)DismissCareFeedback();};
            Closed+=(_,_)=>DismissCareFeedback();
        }
        careFeedback.Content=new TextBlock{Text="照顧提示 · "+message,TextWrapping=TextWrapping.Wrap,MaxWidth=240};
        careFeedback.IsOpen=true;
        careFeedbackTimer!.Stop();careFeedbackTimer.Start();
    }
    internal void DismissCareFeedback()
    {careFeedbackTimer?.Stop();if(careFeedback is not null)careFeedback.IsOpen=false;}

    internal string? CareUnavailableReason(CareKind kind)
    {
        if(EditingRoom)return "請先結束佈置，再與角色互動。";
        if(Interacting)return "請先放下角色，等牠站穩再試一次。";
        // Do not separate a care marker from its action in the generic motion queue.
        // Safety recovery and stair exit retain ownership until genuinely complete.
        if(FinishingMotion)return "正在走樓梯或落地，站穩後就能互動。";
        if(kind==CareKind.Play&&appearance!=PetAppearance.Girl&&AvailableCareToy() is null)
            return "玩具正在使用中；放下球或等夥伴玩完再試。";
        return null;
    }
    private ToyWindow? AvailableCareToy()=>AllToys
        .Where(toy=>toy!=Square&&CanPlayToy(toy)&&!toy.Model.Held&&Occupancy.Available(ToyId(toy),appearance))
        .OrderBy(toy=>Math.Abs(toy.Model.X-body.X)).FirstOrDefault();

    private void RefreshCareMenuLabels()
    {
        if(Character.ContextMenu is not {} menu)return;
        foreach(var item in menu.Items.OfType<MenuItem>())
            if(item.Tag is CareKind kind)item.Header=appearance==PetAppearance.Girl?kind switch
            {CareKind.Feed=>"用餐",CareKind.Pet=>"陪伴互動",CareKind.Play=>"房間活動",CareKind.Groom=>"整理頭髮",_=>"安排休息"}:kind switch
            {CareKind.Feed=>"餵飯飯",CareKind.Pet=>"摸摸頭",CareKind.Play=>"一起玩球",CareKind.Groom=>"梳理毛毛",_=>"哄牠睡覺"};
    }
}
