using System.Globalization;
using System.Windows.Data;
namespace DesktopLife.App;
public sealed class UiText : IValueConverter
{
    private static readonly Dictionary<string,string> Labels=new()
    {
        ["BellBall"]="鈴鐺球",["ToyMouse"]="玩具老鼠",["CatTree"]="貓跳台",["Slide"]="溜滑梯",["Desk"]="書桌",["Bookshelf"]="書架",["Cushion"]="睡墊",["Yarn"]="毛線球",["Fall"]="落地中",["BatToy"]="伸爪撥球",["Tray"]="進入後台托盤（持續陪伴）",["Exit"]="保存並結束程式",["Highest"]="最高：全螢幕仍顯示",["High"]="高：全螢幕時隱藏",["Medium"]="中：全螢幕與最大化時隱藏",["Desktop"]="低：桌布層級（位於應用程式下方）",
        ["Girl"]="女孩",["Cat"]="橘貓",["Utility"]="效用決策",["FlyInspired"]="果蠅啟發神經網路",["RealConnectomeExperimental"]="真實神經連接（實驗）",["Hybrid"]="混合模式",
        ["Eat"]="吃飯飯",["Groom"]="梳理毛毛",["Nuzzle"]="蹭蹭你",["Greet"]="歡迎回來",["Idle"]="發呆",["Walk"]="走路",["Wander"]="散步",["Sit"]="坐下",["Sleep"]="睡覺",["Stretch"]="伸懶腰",["ObserveCursor"]="觀察游標",["ChaseCursor"]="奔跑追游標",["AvoidCursor"]="奔跑躲游標",["ObserveDesktopIcon"]="觀察玩具方塊",["PseudoPushIcon"]="推動玩具方塊",["RestInCorner"]="角落休息",["PlayToy"]="玩球",["DrawDoodle"]="畫畫",["WriteNote"]="寫便條",["Hide"]="躲在角落",["Explore"]="探索",
        ["Basic"]="基本",["Social"]="社交",["Create"]="創作",["Play"]="玩耍",["Rest"]="休息",["Mischief"]="惡作劇",["Unknown"]="未知",["QuietDesktop"]="安靜桌面",["ActiveDesktop"]="活躍桌面",["UserNearby"]="使用者在附近",["Left"]="左鍵",["Right"]="右鍵",["Middle"]="中鍵",["Approach"]="接近",["Avoid"]="迴避",["Interact"]="互動"
        ,["ObserveShortcut"]="觀察快捷圖示",["PushShortcut"]="推動快捷圖示",["Line"]="線條",["Arc"]="弧線",["Circle"]="圓形",["Ellipse"]="橢圓",["Spiral"]="螺旋",["Polygon"]="多邊形",["Dot"]="圓點",["Eye"]="眼睛",["Mouth"]="嘴巴",["Heart"]="愛心",["Star"]="星星",
        ["Company"]="陪伴",["Complain"]="碎念",["Share"]="分享",["Invite"]="邀請",["Reflect"]="自言自語",["Happy"]="開心",["Bored"]="無聊",["Missing"]="想念",["Excited"]="興奮",["Shy"]="含蓄",["Upset"]="悶悶的",
        ["Observe"]="保持距離觀察",["Follow"]="跟隨一小段",["Orbit"]="繞著游標",["SitNearby"]="坐在附近",["Peek"]="躲一下再探頭",["Wait"]="靠近等待",
        ["Symmetry"]="對稱",["Complexity"]="複雜",["Shape"]="幾何",["Density"]="密度",["Size"]="大小",["Corner"]="角落",["Length"]="長度",["Direct"]="直接",["Playful"]="俏皮",["Emotional"]="情緒",["Speed"]="速度",["Exploration"]="探索",["CursorAffinity"]="親近"
    };
    public static string Label(object value)=>Labels.GetValueOrDefault(value.ToString()??"",value.ToString()??"");
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>Label(value);
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
