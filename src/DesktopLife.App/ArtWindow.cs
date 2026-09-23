using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopLife.Core;
using DesktopLife.Windows;
namespace DesktopLife.App;
public sealed class ArtWindow : Window
{
    private readonly Canvas canvas = new();
    private readonly Canvas artLayer=new();
    private readonly List<CreativeWork> works=new();
    public IReadOnlyList<CreativeWork> Works=>works;
    private bool hideCreations;
    public ArtWindow()
    {
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=null;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=false;ShowActivated=false;Topmost=true;IsHitTestVisible=false;Content=canvas;
        SourceInitialized+=(_,_)=>DesktopInteraction.MakeClickThrough(new WindowInteropHelper(this).Handle);
        canvas.Children.Add(artLayer);
    }
    public void Update(BodyBounds bounds,IReadOnlyList<DesktopObject> objects,BodyAction action,double time,double petX,double petY)
    {
        Width=bounds.Width;Height=bounds.Height;DisplayWorkspace.Position(this,bounds.Left,bounds.Top);
        if(works.RemoveAll(w=>!w.Keep && DateTimeOffset.UtcNow-w.CreatedAt>TimeSpan.FromMinutes(2))>0)RenderWorks();
    }
    public bool Create(BodyAction action,BehaviorParameters parameters,BehaviorVariationState memory,double x,double y)
    {
        if(works.Count>=32){var first=works.FindIndex(w=>!w.Keep);if(first<0)return false;works.RemoveAt(first);}
        var drawing=action==BodyAction.DrawDoodle?ProceduralDrawing.Generate(parameters):null;
        var note=action==BodyAction.WriteNote?ComposableText.Generate(parameters,memory):null;
        var width=drawing?.Width??220;var height=drawing?.Height??130;
        var availableWidth=double.IsFinite(Width)?Width:SystemParameters.WorkArea.Width;
        var availableHeight=double.IsFinite(Height)?Height:SystemParameters.WorkArea.Height;
        var random=new Random(parameters.Seed);
        var edgeX=random.NextDouble()<.5?12:Math.Max(0,availableWidth-width-12);
        var edgeY=random.NextDouble()<.5?12:Math.Max(0,availableHeight-height-12);
        var corner=parameters.Draw.Corner;
        x=x*(1-corner)+edgeX*corner;y=y*(1-corner)+edgeY*corner;
        works.Add(new(DoodlePattern.RandomLines,note?.Text,Math.Clamp(x,0,Math.Max(0,availableWidth-width)),Math.Clamp(y,0,Math.Max(0,availableHeight-height)),parameters.Seed,DateTimeOffset.UtcNow,Drawing:drawing));
        if(note is not null)memory.Remember(new(action,note.Signature,note.Text,DateTimeOffset.UtcNow));
        RenderWorks();return true;
    }
    public void ClearWorks(){works.Clear();RenderWorks();}
    // Offscreen render of generated vectors only; no desktop/screen capture.
    public void RenderDiagnosticSheet(string path,BehaviorParameters[] parameters)
    {
        var visual=new DrawingVisual();
        using(var context=visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,750,520));
            for(var i=0;i<Math.Min(6,parameters.Length);i++)
            {
                var drawing=ProceduralDrawing.Generate(parameters[i]);var ox=i%3*250+10;var oy=i/3*260+10;
                var pen=new Pen(Brushes.Teal,drawing.StrokeWidth);
                foreach(var stroke in drawing.Strokes)
                    for(var j=1;j<stroke.Length;j++)context.DrawLine(pen,new Point(ox+stroke[j-1].X,oy+stroke[j-1].Y),new Point(ox+stroke[j].X,oy+stroke[j].Y));
                var note=ComposableText.Generate(parameters[i],new()).Text;
                var text=new FormattedText($"複雜度 {parameters[i].Draw.Complexity:F2}\n{note}",System.Globalization.CultureInfo.GetCultureInfo("zh-TW"),FlowDirection.LeftToRight,new Typeface("Microsoft JhengHei"),12,Brushes.DarkSlateGray,1){MaxTextWidth=230,MaxTextHeight=85};
                context.DrawText(text,new Point(ox,oy+165));
            }
        }
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(750,520,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file=System.IO.File.Create(path);encoder.Save(file);
    }
    public void KeepWorks(){for(var i=0;i<works.Count;i++)works[i]=works[i] with{Keep=true};RenderWorks();}
    public void ToggleWorks(){hideCreations=!hideCreations;RenderWorks();}
    public void RestoreWorks(IEnumerable<CreativeWork> saved)
    {works.Clear();works.AddRange(saved.Where(w=>Enum.IsDefined(w.Pattern)&&double.IsFinite(w.X)&&double.IsFinite(w.Y)&&w.X>=0&&w.Y>=0&&(w.Text is null||w.Text.Length<=100)).Take(32));RenderWorks();}
    private void RenderWorks()
    {
        artLayer.Children.Clear();artLayer.Visibility=hideCreations?Visibility.Hidden:Visibility.Visible;
        foreach(var work in works)
        {
            if(work.Text is { } text)
            {
                var note=new Border{Background=new SolidColorBrush(Color.FromArgb(230,255,244,190)),CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Child=new TextBlock{Text=text,Foreground=Brushes.DarkSlateGray,FontSize=16,MaxWidth=196,TextWrapping=TextWrapping.Wrap}};
                Canvas.SetLeft(note,work.X);Canvas.SetTop(note,work.Y);artLayer.Children.Add(note);
            }
            else foreach(var stroke in work.Drawing is {} generated?(IReadOnlyList<IReadOnlyList<DoodlePoint>>)generated.Strokes:DoodleGenerator.Generate(work.Pattern,work.Seed))
            {
                var line=new Polyline{Stroke=work.Drawing is {} style?new Brush[]{Brushes.Teal,Brushes.SlateBlue,Brushes.MediumVioletRed,Brushes.DarkOrange,Brushes.SeaGreen}[style.ColorIndex]:Brushes.MediumVioletRed,StrokeThickness=work.Drawing?.StrokeWidth??3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Points=new PointCollection(stroke.Select(p=>new Point(p.X+work.X,p.Y+work.Y)))};
                artLayer.Children.Add(line);
            }
        }
    }
}
