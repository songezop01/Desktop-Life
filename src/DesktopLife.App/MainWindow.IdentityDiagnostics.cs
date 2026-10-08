using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    public void SmokeIdentity(string root)
    {
        Pet.SmokeCapabilities();
        foreach(var scale in new[]{1d,1.5,2})
        {
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,760,800));
                foreach(var template in Enum.GetValues<DoodleTemplate>())
                {
                    var index=(int)template;var x=index%4*190+15;var y=index/4*200+8;
                    var drawing=GirlDoodleGenerator.Generate(template,80+index,.8);
                    foreach(var stroke in drawing.Strokes)for(var i=1;i<stroke.Length;i++)dc.DrawLine(new Pen(Brushes.Teal,drawing.StrokeWidth),new Point(x+stroke[i-1].X,y+stroke[i-1].Y),new Point(x+stroke[i].X,y+stroke[i].Y));
                    dc.DrawText(new FormattedText(GirlDoodleGenerator.Label(template),System.Globalization.CultureInfo.GetCultureInfo("zh-TW"),FlowDirection.LeftToRight,new Typeface("Microsoft JhengHei"),14,Brushes.DarkSlateGray,scale),new Point(x+60,y+170));
                }
            }
            var bitmap=new RenderTargetBitmap((int)(760*scale),(int)(800*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(root,$"girl-doodles-{scale*100:0}.png"));encoder.Save(file);
        }
    }
}
public partial class PetWindow
{
    public void SmokeCapabilities()
    {
        var previous=appearance;ResetPosition();
        try
        {
            SetAppearance(PetAppearance.Cat);var works=Art.Works.Count;SetAction(BodyAction.DrawDoodle);SetAction(BodyAction.WriteNote);Say("不得出現的人類語句");
            if(CurrentAction is BodyAction.DrawDoodle or BodyAction.WriteNote||Art.Works.Count!=works||SpeechBubble.Visibility==Visibility.Visible)throw new Exception("Cat capability boundary failed.");
            if(Art.Create(BodyAction.DrawDoodle,Parameters,new(),0,0,PetAppearance.Cat))throw new Exception("Cat creative dispatch bypass.");
            SetAppearance(PetAppearance.Girl);SetAction(BodyAction.Groom);ApplyPose(2);if(feline.Visibility==Visibility.Visible||ToyPaw.Visibility==Visibility.Visible)throw new Exception("Girl displayed feline expression.");
            Say("你好。");if(SpeechBubble.Visibility!=Visibility.Visible)throw new Exception("Girl lost speech.");
            SetAction(BodyAction.DrawDoodle);SetAppearance(PetAppearance.Cat);if(CurrentAction==BodyAction.DrawDoodle||SpeechBubble.Visibility==Visibility.Visible)throw new Exception("Appearance switch leaked creation or speech.");
        }
        finally{SetAppearance(previous);ResetPosition();}
    }
}
