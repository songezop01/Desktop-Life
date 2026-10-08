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
    private readonly ArtworkVectorLayer vectors=new();
    private readonly List<CreativeWork> works=new();
    public IReadOnlyList<CreativeWork> Works=>works;
    private bool hideCreations;
    private readonly Dictionary<(BodyAction Action,PetAppearance Character),DateTimeOffset> lastCreation=[];
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
    public bool Create(BodyAction action,BehaviorParameters parameters,BehaviorVariationState memory,double x,double y,PetAppearance character=PetAppearance.Girl)
    {
        if(!CharacterCapability.Allows(character,action)||action is not (BodyAction.DrawDoodle or BodyAction.WriteNote))return false;
        if(lastCreation.TryGetValue((action,character),out var last)&&DateTimeOffset.UtcNow-last<TimeSpan.FromSeconds(action==BodyAction.WriteNote?90:45))return false;
        if(works.Count>=32){var first=works.FindIndex(w=>!w.Keep);if(first<0)return false;works.RemoveAt(first);}
        var template=GirlDoodleGenerator.Select(parameters,memory);
        var drawing=action==BodyAction.DrawDoodle?GirlDoodleGenerator.Generate(template,parameters.Seed,parameters.Drives.Creativity,parameters.Drives.Calmness):null;
        var note=action==BodyAction.WriteNote?ComposableText.GenerateGirl(parameters,memory):null;
        var width=drawing?.Width??220;var height=drawing?.Height??130;
        var availableWidth=double.IsFinite(Width)?Width:SystemParameters.WorkArea.Width;
        var availableHeight=double.IsFinite(Height)?Height:SystemParameters.WorkArea.Height;
        var random=new Random(parameters.Seed);
        var edgeX=random.NextDouble()<.5?12:Math.Max(0,availableWidth-width-12);
        var edgeY=random.NextDouble()<.5?12:Math.Max(0,availableHeight-height-12);
        var corner=parameters.Draw.Corner;
        x=x*(1-corner)+edgeX*corner;y=y*(1-corner)+edgeY*corner;
        works.Add(new(DoodlePattern.RandomLines,note?.Text,Math.Clamp(x,0,Math.Max(0,availableWidth-width)),Math.Clamp(y,0,Math.Max(0,availableHeight-height)),parameters.Seed,DateTimeOffset.UtcNow,Drawing:drawing,DoodleTemplateId:drawing is not null?template.ToString():null));
        if(drawing is not null)memory.Remember(new(action,"doodle:"+template,null,DateTimeOffset.UtcNow));
        if(note is not null)memory.Remember(new(action,note.Signature,note.Text,DateTimeOffset.UtcNow));
        lastCreation[(action,character)]=DateTimeOffset.UtcNow;RenderWorks();return true;
    }
    public void ClearWorks(){works.Clear();RenderWorks();}
    public void ResetCreationCooldownsForSmoke()=>lastCreation.Clear();
    internal static void VerifyVectorRendering(string path)
    {
        var window=new ArtWindow();
        try
        {
            var drawing=new GeneratedDrawing([[new(10,10),new(60,60)]],80,80,3,0);
            window.RestoreWorks(Enumerable.Range(0,32).Select(i=>new CreativeWork(DoodlePattern.RandomLines,null,12,12,i,DateTimeOffset.UtcNow,Drawing:drawing)));
            window.canvas.Measure(new Size(128,128));window.canvas.Arrange(new Rect(0,0,128,128));window.canvas.UpdateLayout();
            System.Windows.Media.Imaging.RenderTargetBitmap Render()
            {
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(128,128,96,96,PixelFormats.Pbgra32);
                bitmap.Render(window.canvas);return bitmap;
            }
            byte[] Pixels(System.Windows.Media.Imaging.RenderTargetBitmap bitmap)
            {var bytes=new byte[128*128*4];bitmap.CopyPixels(bytes,128*4,0);return bytes;}
            var image=Render();var pixels=Pixels(image);var middle=(40*128+40)*4;
            if(pixels[middle+3]<240||pixels[middle+1]<100||pixels[middle+2]>20||window.artLayer.Children.Count!=1||!window.vectors.IsFrozen)
                throw new InvalidOperationException("Frozen artwork layer failed its actual canvas pixel/layout check.");
            window.KeepWorks();window.canvas.UpdateLayout();
            if(!pixels.SequenceEqual(Pixels(Render())))throw new InvalidOperationException("Keeping artwork changed its rendering.");
            window.ToggleWorks();window.canvas.UpdateLayout();
            if(Pixels(Render()).Any(value=>value!=0))throw new InvalidOperationException("Hidden artwork remained visible.");
            window.ToggleWorks();window.ClearWorks();window.canvas.UpdateLayout();
            if(Pixels(Render()).Any(value=>value!=0))throw new InvalidOperationException("Cleared artwork remained visible.");
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using var file=System.IO.File.Create(path);encoder.Save(file);
        }
        finally{window.Close();}
    }
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
        artLayer.Children.Clear();artLayer.Children.Add(vectors);artLayer.Visibility=hideCreations?Visibility.Hidden:Visibility.Visible;
        var drawing=new DrawingGroup();
        using var context=drawing.Open();
        foreach(var work in works)
        {
            if(work.Text is { } text)
            {
                var note=new Border{Background=new SolidColorBrush(Color.FromArgb(230,255,244,190)),CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Child=new TextBlock{Text=text,Foreground=Brushes.DarkSlateGray,FontSize=16,MaxWidth=196,TextWrapping=TextWrapping.Wrap}};
                Canvas.SetLeft(note,work.X);Canvas.SetTop(note,work.Y);artLayer.Children.Add(note);
            }
            else
            {
                var brush=work.Drawing is {} style?new Brush[]{Brushes.Teal,Brushes.SlateBlue,Brushes.MediumVioletRed,Brushes.DarkOrange,Brushes.SeaGreen}[style.ColorIndex]:Brushes.MediumVioletRed;
                var pen=new Pen(brush,work.Drawing?.StrokeWidth??3){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();
                foreach(var stroke in work.Drawing is {} generated?(IReadOnlyList<IReadOnlyList<DoodlePoint>>)generated.Strokes:DoodleGenerator.Generate(work.Pattern,work.Seed))
                {
                    if(stroke.Count<2)continue;
                    var geometry=new StreamGeometry();
                    using(var path=geometry.Open())
                    {
                        path.BeginFigure(new(stroke[0].X+work.X,stroke[0].Y+work.Y),false,false);
                        for(var i=1;i<stroke.Count;i++)path.LineTo(new(stroke[i].X+work.X,stroke[i].Y+work.Y),true,false);
                    }
                    geometry.Freeze();context.DrawGeometry(null,pen,geometry);
                }
            }
        }
        context.Close();drawing.Freeze();vectors.SetDrawing(drawing);
    }

    // One frozen vector drawing replaces hundreds of layout/shape objects.
    private sealed class ArtworkVectorLayer:FrameworkElement
    {
        private DrawingGroup? drawing;
        public bool IsFrozen=>drawing?.IsFrozen==true;
        public void SetDrawing(DrawingGroup value){drawing=value;InvalidateVisual();}
        protected override void OnRender(DrawingContext context){if(drawing is not null)context.DrawDrawing(drawing);}
    }
}
