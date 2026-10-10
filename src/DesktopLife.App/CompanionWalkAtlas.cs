using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;

namespace DesktopLife.App;

internal sealed class WalkManifest
{
    public int Version { get; set; }
    public WalkClip[] Clips { get; set; } = [];
}
internal sealed class WalkClip
{
    public string Character { get; set; } = "";
    public string Sheet { get; set; } = "";
    public double ReferenceHeight { get; set; }
    public double DurationSeconds { get; set; }
    public double StrideDip { get; set; }
    public int[][] Frames { get; set; } = [];
}

internal sealed class CompanionWalkAtlas
{
    private static readonly Dictionary<PetAppearance, CompanionWalkAtlas?> cache = [];
    public SpriteAssetFrame[] Frames { get; }
    public double Scale { get; }
    public double StrideDip { get; }
    public int TransparentPixels { get; }
    public static int LoadedCount => cache.Count(p => p.Value is not null);
    public string Source { get; }
    private readonly (Point? Left,Point? Right)[] feet;
    private readonly bool quadruped;
    internal Point? Foot(int frame,StairFootSide side)=>side==StairFootSide.Left?feet[frame].Left:feet[frame].Right;
    private CompanionWalkAtlas(PetAppearance kind)
    {
        var manifest = SpriteAssetReader.Manifest<WalkManifest>("Companions/walk-manifest.json");
        if (manifest.Version != 1) throw new InvalidDataException("Unsupported walk animation manifest.");
        var clip = manifest.Clips.Single(c => c.Character == kind.ToString());
        if (clip.Frames.Length != 8 || clip.ReferenceHeight <= 0 || clip.StrideDip <= 0)
            throw new InvalidDataException("Walk clip must define eight frames, scale reference and stride.");
        Source = clip.Sheet; StrideDip = clip.StrideDip;
        var sheet = new SpriteAssetReader("Companions/" + clip.Sheet, kind == PetAppearance.Girl ? 1536 : 1024);
        Frames = clip.Frames.Select(sheet.Extract).ToArray(); TransparentPixels = sheet.TransparentPixels;
        quadruped=kind!=PetAppearance.Girl;
        feet=Frames.Select(frame=>CalibrateFeet(frame,quadruped)).ToArray();
        var referenceTarget = kind == PetAppearance.Girl ? 144 : 110;
        Scale = Math.Min(referenceTarget / (clip.ReferenceHeight * sheet.DecodeScale), 112d / Frames.Max(f => f.Width));
    }
    internal (Point? Left,Point? Right) RenderedFeet(int frameIndex,double drawScale,double bodyWidth,double bodyHeight,double dpiScale,bool facingRight)
    {
        // Source-pixel sole centers can move several pixels after the final WPF
        // filter, especially on a curved slipper at fractional DPI. Calibrate
        // that real output once per pose/geometry, not an ideal foot marker.
        var width=(int)Math.Ceiling(bodyWidth*dpiScale);var height=(int)Math.Ceiling(bodyHeight*dpiScale);
        if(width<1||height<1||(long)width*height>4_000_000)return(null,null);
        var frame=Frames[frameIndex];var canonical=new DrawingVisual();RenderOptions.SetBitmapScalingMode(canonical,BitmapScalingMode.HighQuality);
        using(var dc=canonical.RenderOpen())
        {
            if(!facingRight)dc.PushTransform(new ScaleTransform(-1,1,58,144));
            dc.DrawImage(frame.Image,new Rect((116-frame.Width*drawScale)/2,
                144-frame.Height*drawScale,frame.Width*drawScale,frame.Height*drawScale));
            if(!facingRight)dc.Pop();
        }
        // Use the production canonical visual -> absolute VisualBrush -> final
        // body/DPI path. Directly enlarging the source bitmap is a different WPF
        // filtering path and shifts a curved slipper's solid sole center.
        var drawing=new DrawingVisual();RenderOptions.SetBitmapScalingMode(drawing,BitmapScalingMode.HighQuality);
        using(var dc=drawing.RenderOpen())
            dc.DrawRectangle(new VisualBrush(canonical){ViewboxUnits=BrushMappingMode.Absolute,
                Viewbox=new Rect(0,0,116,144),Stretch=Stretch.Fill,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},
                null,new Rect(0,0,bodyWidth,bodyHeight));
        var image=new RenderTargetBitmap(width,height,96*dpiScale,96*dpiScale,PixelFormats.Pbgra32);image.Render(drawing);
        var pixels=new byte[width*height*4];image.CopyPixels(pixels,width*4,0);
        var alpha=new byte[width*height];for(var i=0;i<alpha.Length;i++)alpha[i]=pixels[i*4+3];
        var calibrated=CalibrateAlpha(alpha,width,height,quadruped,facingRight);
        // Keep source/anatomical phase labels when the screen order reverses.
        if(!facingRight)calibrated=(calibrated.Right,calibrated.Left);
        Point? Canonical(Point? point)=>point is {} value?new Point((value.X+.5)/dpiScale*116/bodyWidth,(value.Y+.5)/dpiScale*144/bodyHeight):null;
        return(Canonical(calibrated.Left),Canonical(calibrated.Right));
    }
    private static (Point? Left,Point? Right) CalibrateFeet(SpriteAssetFrame frame,bool quadruped)
        =>CalibrateAlpha(frame.Alpha,frame.Width,frame.Height,quadruped);
    private static (Point? Left,Point? Right) CalibrateAlpha(byte[] alpha,int width,int height,bool quadruped,bool facingRight=true)
    {
        // Use the actual solid soles/paws, excluding detached faint shadows.
        // This cached scan runs once per frame, never in the composition loop.
        var bottom=height-1;var band=Math.Max(3,(int)(height*.07));
        var columns=new List<(int X,int Y)>();
        for(var x=0;x<width;x++)
            for(var y=bottom;y>=Math.Max(0,bottom-band);y--)
                if(alpha[y*width+x]>=120){columns.Add((x,y));break;}
        if(columns.Count<4)return(null,null);
        var groups=new List<List<(int X,int Y)>>();
        foreach(var p in columns)
        {
            if(groups.Count==0||p.X-groups[^1][^1].X>2)groups.Add([]);
            groups[^1].Add(p);
        }
        var meaningful=groups.Where(g=>g.Count>=3).ToArray();
        if(meaningful.Length==0)return(null,null);
        if(quadruped)
        {
            // Canonical artwork faces right. Its extreme left cluster is a
            // hindpaw, not the other foreleg; alternating those extremes forced
            // a whole body's length between adjacent 15 DIP treads. Calibrate
            // the painted supporting forepaws. When one forepaw is occluded or
            // raised, both phase labels use the actual visible support cluster.
            // This does not assert simultaneous hindpaw contact.
            meaningful=meaningful.Where(group=>facingRight?group.Average(point=>point.X)>=width*.5:group.Average(point=>point.X)<=width*.5).ToArray();
            if(meaningful.Length==0)return(null,null);
        }
        Point Sole(List<(int X,int Y)> group)
        {
            var low=group.Max(p=>p.Y);var solid=group.Where(p=>p.Y>=low-1).ToArray();
            var selected=solid[solid.Length/2];return new(selected.X,selected.Y);
        }
        return(Sole(meaningful[0]),Sole(meaningful[^1]));
    }
    public static CompanionWalkAtlas? For(PetAppearance appearance)
    {
        if (cache.TryGetValue(appearance, out var result)) return result;
        try { result = new(appearance); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or System.IO.FileFormatException)
        { result = null; }
        cache[appearance] = result; return result;
    }
}
