using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;

namespace DesktopLife.App;
public partial class MainWindow
{
    private void ChooseCompanionCard(object sender,System.Windows.Input.MouseButtonEventArgs e)
    {
        if(sender is Border {Tag:string value}&&Enum.TryParse<PetAppearance>(value,out var kind))
        {Appearances.SelectedItem=kind;e.Handled=true;}
    }
    private void InitializeIllustrations()
    {
        foreach(var (host,kind) in new[]{(CatPortraitHost,PetAppearance.Cat),(GirlPortraitHost,PetAppearance.Girl),(DogPortraitHost,PetAppearance.BorderCollie)})
        {
            var visual=new CompanionSpriteVisual{IsHitTestVisible=false};visual.SetCharacter(kind);visual.Pose(BodyAction.Idle,0,1);
            host.Child=new Viewbox{Stretch=Stretch.Uniform,Child=visual};host.Visibility=Visibility.Visible;
        }
    }
    public void SmokeIllustrations(string root)
    {
        var kinds=Enum.GetValues<PetAppearance>();
        if(kinds.Any(k=>CompanionSpriteAtlas.For(k) is null||CompanionWalkAtlas.For(k) is null)
            ||CompanionSpriteAtlas.For(PetAppearance.Girl,true) is null||FurnitureArt.LoadedCount!=Enum.GetValues<FurnitureKind>().Length)
            throw new InvalidOperationException("Illustrated companion atlases did not load.");
        var actions=new[]{BodyAction.Idle,BodyAction.Walk,BodyAction.ChaseCursor,BodyAction.Sleep,BodyAction.Stretch,BodyAction.Eat,BodyAction.Groom,BodyAction.Greet};
        var diagnostics=new List<object>();var sheet=new DrawingVisual();
        using(var dc=sheet.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(255,248,240)),null,new Rect(0,0,928,432));
            foreach(var kind in kinds)
            {
                var visual=new CompanionSpriteVisual();visual.SetCharacter(kind);diagnostics.Add(visual.AssetDiagnostic);
                visual.Measure(new Size(116,144));visual.Arrange(new Rect(0,0,116,144));
                var renderedPoses=new HashSet<string>();
                for(var i=0;i<actions.Length;i++)
                {
                    for(var warm=0;warm<5;warm++)visual.Pose(actions[i],.3+i*.6+warm*.1,1,phase:BehaviorPhase.Work);
                    visual.UpdateLayout();
                    // Verify genuine alpha at all supported preview scales.
                    foreach(var scale in new[]{1d,1.25,1.5,2})
                    {
                        var bitmap=new RenderTargetBitmap((int)(116*scale),(int)(144*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);
                        var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
                        if(scale==1)renderedPoses.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels)));
                        if(pixels[3]!=0||pixels[((bitmap.PixelHeight-1)*bitmap.PixelWidth)*4+3]!=0)
                            throw new InvalidOperationException($"Illustrated transparent corner captures the desktop: {kind}/{actions[i]}/{scale}");
                        if(!Enumerable.Range(0,pixels.Length/4).Any(p=>pixels[p*4+3]>50))throw new InvalidOperationException("Empty illustrated pose.");
                        var start=(bitmap.PixelHeight-2)*bitmap.PixelWidth;
                        if(!Enumerable.Range(start,bitmap.PixelWidth*2).Any(p=>pixels[p*4+3]>24))throw new InvalidOperationException($"Illustrated pose floats above support: {kind}/{actions[i]}/{scale}");
                    }
                    var frame=new RenderTargetBitmap(232,288,192,192,PixelFormats.Pbgra32);frame.Render(visual);
                    dc.DrawImage(frame,new Rect(i*116,(int)kind*144,116,144));
                }
                if(renderedPoses.Count<6)throw new InvalidOperationException($"Illustrated poses do not visibly change: {kind}/{renderedPoses.Count}");
                if(visual.InputHitTest(new Point(0,140)) is not null)throw new InvalidOperationException("Illustrated transparent padding intercepts clicks.");
            }
        }
        var image=new RenderTargetBitmap(1856,864,192,192,PixelFormats.Pbgra32);image.Render(sheet);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(root,"illustrated-companions.png")))encoder.Save(file);
        File.WriteAllText(Path.Combine(root,"illustration-assets.json"),JsonSerializer.Serialize(diagnostics,new JsonSerializerOptions{WriteIndented=true}));
        SmokeWalkCycles(root);
        IllustrationAnimationChecks.VerifyTransitions(root);
        SmokeFurnitureArt(root);
    }
    private static void SmokeWalkCycles(string root)
    {
        var sheet=new DrawingVisual();var evidence=new List<object>();
        using(var drawing=sheet.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.FloralWhite,null,new Rect(0,0,928,432));
            foreach(var kind in Enum.GetValues<PetAppearance>())
            {
                var visual=new CompanionSpriteVisual();visual.SetCharacter(kind);
                visual.Measure(new Size(116,144));visual.Arrange(new Rect(0,0,116,144));
                for(var warm=0;warm<5;warm++)visual.Pose(BodyAction.Walk,warm*.1,1);
                var indices=new HashSet<int>();var hashes=new HashSet<string>();
                for(var i=0;i<8;i++)
                {
                    if(i>0)visual.AdvanceWalk(visual.WalkStrideDip/8);
                    visual.Pose(BodyAction.Walk,.5+.1*i,1);visual.UpdateLayout();indices.Add(visual.FrameIndex);
                    var frame=new RenderTargetBitmap(232,288,192,192,PixelFormats.Pbgra32);frame.Render(visual);
                    var pixels=new byte[232*288*4];frame.CopyPixels(pixels,232*4,0);
                    hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels)));
                    if(!Enumerable.Range(232*286,232*2).Any(p=>pixels[p*4+3]>24))
                        throw new InvalidOperationException($"Walking frame lost ground contact: {kind}/{i}");
                    if(visual.InputHitTest(new Point(0,140)) is not null)
                        throw new InvalidOperationException($"Walk frame padding intercepts clicks: {kind}/{i}");
                    drawing.DrawImage(frame,new Rect(i*116,(int)kind*144,116,144));
                }
                if(indices.Count!=8||hashes.Count!=8)throw new InvalidOperationException($"Walk cycle needs eight distinct rendered frames: {kind}/{indices.Count}/{hashes.Count}");
                var stopped=visual.FrameIndex;visual.Pose(BodyAction.Walk,20,1);
                if(visual.FrameIndex!=stopped)throw new InvalidOperationException("Walk phase advanced without movement.");
                evidence.Add(new{Character=kind.ToString(),Indices=indices.Order().ToArray(),DistinctFrames=hashes.Count,GroundContact=true,StationaryPhaseStable=true,visual.AssetDiagnostic});
            }
        }
        SaveArtImage(sheet,1856,864,Path.Combine(root,"walk-cycles.png"));
        File.WriteAllText(Path.Combine(root,"walk-cycle-checks.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void SmokeFurnitureArt(string root)
    {
        var sheet=new DrawingVisual();var evidence=new List<object>();var kinds=Enum.GetValues<FurnitureKind>();
        using(var drawing=sheet.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.FloralWhite,null,new Rect(0,0,1200,960));
            for(var i=0;i<kinds.Length;i++)
            {
                var kind=kinds[i];var size=RoomWindow.Size(kind);var x=(i%5)*240d;var y=(i/5)*240d;
                var scale=Math.Min(220/size.Width,208/size.Height);
                drawing.PushTransform(new TranslateTransform(x+10,y+10));drawing.PushTransform(new ScaleTransform(scale,scale));
                if(!FurnitureArt.Draw(drawing,kind,size.Width,size.Height))throw new InvalidOperationException("Furniture art unavailable: "+kind);
                var platforms=FurnitureArt.Platforms(kind,size.Width,size.Height);
                foreach(var p in platforms)
                {
                    if(p.Width<=0||p.X<0||p.X+p.Width>size.Width+.5||p.Y<0||p.Y>size.Height||p.EndY<0||p.EndY>size.Height)
                        throw new InvalidOperationException("Furniture support outside artwork: "+kind);
                    drawing.DrawLine(new Pen(Brushes.MediumVioletRed,1/scale),new Point(p.X,p.Y),new Point(p.X+p.Width,p.EndY));
                }
                drawing.Pop();drawing.Pop();
                var room=new RoomWindow(new(Guid.NewGuid(),kind,DisplayWorkspace.Bounds.Left+20,DisplayWorkspace.Bounds.Top+20));
                var before=room.Platforms.ToArray();room.SetSceneScale(.5);var after=room.Platforms.ToArray();
                if(before.Length!=after.Length||after.Where((p,n)=>Math.Abs(p.Width-before[n].Width*.5)>.001).Any())
                    throw new InvalidOperationException("Furniture platform scale drift: "+kind);
                room.Show();room.UpdateLayout();
                var content=(FrameworkElement)room.Content;
                var origin=content.TranslatePoint(new Point(0,0),room);
                var farCorner=content.TranslatePoint(new Point(size.Width,size.Height),room);
                var pixelTolerance=1/DisplayWorkspace.Active.Scale+.001;
                if(Math.Abs(origin.X)>pixelTolerance||Math.Abs(origin.Y)>pixelTolerance||Math.Abs(farCorner.X-size.Width*.5)>pixelTolerance||Math.Abs(farCorner.Y-size.Height*.5)>pixelTolerance)
                    throw new InvalidOperationException($"Furniture canvas moves or clips under scale: {kind}/{origin}/{farCorner}");
                if(kind is FurnitureKind.Desk or FurnitureKind.Sofa or FurnitureKind.Bookshelf)
                {
                    var nativeFrame=new RenderTargetBitmap((int)Math.Ceiling(size.Width),(int)Math.Ceiling(size.Height),192,192,PixelFormats.Pbgra32);nativeFrame.Render(room);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(nativeFrame));using(var stream=File.Create(Path.Combine(root,$"furniture-half-{kind}.png")))encoder.Save(stream);
                }
                room.Close();evidence.Add(new{Kind=kind.ToString(),size.Width,size.Height,Supports=platforms,SceneScaleHalf=true,CanvasOrigin=new{origin.X,origin.Y},CanvasBottomRight=new{farCorner.X,farCorner.Y}});
            }
        }
        SaveArtImage(sheet,2400,1920,Path.Combine(root,"furniture-supports.png"));
        File.WriteAllText(Path.Combine(root,"furniture-assets.json"),JsonSerializer.Serialize(new{Art=FurnitureArt.Diagnostic,Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void SaveArtImage(Visual drawing,int width,int height,string path)
    {
        var bitmap=new RenderTargetBitmap(width,height,192,192,PixelFormats.Pbgra32);bitmap.Render(drawing);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);
    }
}
