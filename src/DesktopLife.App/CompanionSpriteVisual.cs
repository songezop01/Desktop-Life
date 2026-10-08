using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;

namespace DesktopLife.App;

// Decode once per character, share frozen images between the pet and home previews.
// The generated atlas has eight isolated silhouettes; alpha components provide
// real frame bounds instead of assuming the artist's grid has pixel-perfect padding.
internal sealed class CompanionSpriteAtlas
{
    internal sealed record Frame(BitmapSource Image, byte[] Alpha, int Width, int Height);
    private static readonly Dictionary<string, CompanionSpriteAtlas?> cache = new();
    public static int LoadedCount=>cache.Count(p=>p.Value is not null);
    public Frame[] Frames { get; }
    public double Scale { get; }
    public int TransparentPixels { get; }
    private CompanionSpriteAtlas(PetAppearance kind,bool activities)
    {
        var file = activities?"girl-activities-v1.png":kind switch { PetAppearance.Girl => "girl-v1.png", PetAppearance.Cat => "orange-cat-v1.png", _ => "border-collie-v1.png" };
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        // The 256 DIP girl needs her original resolution at 200% DPI;
        // small animals retain the bounded 1024px decode.
        bitmap.DecodePixelWidth=kind==PetAppearance.Girl?1536:1024;
        bitmap.UriSource = new Uri("pack://application:,,,/Assets/Companions/" + file);
        bitmap.EndInit(); bitmap.Freeze();
        var source = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0); source.Freeze();
        var w = source.PixelWidth; var h = source.PixelHeight;
        var pixels = new byte[w * h * 4]; source.CopyPixels(pixels, w * 4, 0);
        TransparentPixels = Enumerable.Range(0, w * h).Count(i => pixels[i * 4 + 3] == 0);
        if (TransparentPixels < w * h / 5) throw new InvalidOperationException("Companion atlas lacks a transparent background.");
        var visited = new bool[w * h]; var queue = new int[w * h];
        var bounds = new List<(Int32Rect Rect, int Count)>();
        for (var i = 0; i < visited.Length; i++)
        {
            if (visited[i] || pixels[i * 4 + 3] < 24) continue;
            var start = 0; var end = 1; queue[0] = i; visited[i] = true;
            var minX = i % w; var maxX = minX; var minY = i / w; var maxY = minY;
            while (start < end)
            {
                var p = queue[start++]; var x = p % w; var y = p / w;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                if (x > 0) Add(p - 1); if (x + 1 < w) Add(p + 1);
                if (y > 0) Add(p - w); if (y + 1 < h) Add(p + w);
            }
            if (end > 1500 && maxX - minX > 50 && maxY - minY > 50)
                bounds.Add((new Int32Rect(Math.Max(0, minX - 1), Math.Max(0, minY - 1), Math.Min(w - 1, maxX + 1) - Math.Max(0, minX - 1) + 1, Math.Min(h - 1, maxY + 1) - Math.Max(0, minY - 1) + 1), end));
            void Add(int p)
            {
                if (visited[p] || pixels[p * 4 + 3] < 24) return;
                visited[p] = true; queue[end++] = p;
            }
        }
        if (bounds.Count < 8) throw new InvalidOperationException($"Companion atlas has {bounds.Count} isolated poses; expected eight.");
        var poses = bounds.OrderByDescending(b => b.Count).Take(8).OrderBy(b => b.Rect.Y + b.Rect.Height / 2).ToArray();
        var ordered = poses.Take(4).OrderBy(b => b.Rect.X).Concat(poses.Skip(4).OrderBy(b => b.Rect.X));
        Frames = ordered.Select(b =>
        {
            var cropped = new CroppedBitmap(source, b.Rect);
            var data = new byte[b.Rect.Width * b.Rect.Height * 4]; cropped.CopyPixels(data, b.Rect.Width * 4, 0);
            // Copy into a standalone frozen bitmap so crops do not retain the
            // full PNG decoder and converted atlas. Hit testing needs alpha only.
            var image=BitmapSource.Create(b.Rect.Width,b.Rect.Height,96,96,PixelFormats.Bgra32,null,data,b.Rect.Width*4);image.Freeze();
            var alpha=new byte[b.Rect.Width*b.Rect.Height];for(var i=0;i<alpha.Length;i++)alpha[i]=data[i*4+3];
            return new Frame(image, alpha, b.Rect.Width, b.Rect.Height);
        }).ToArray();
        // Calibrate the body against a real upright pose. A wide sleeping pose
        // must not shrink the standing girl down to the size of an animal.
        Scale = 144d / Frames[activities ? 6 : 0].Height;
    }
    public static CompanionSpriteAtlas? For(PetAppearance kind,bool activities=false)
    {
        var key=activities?"GirlActivities":kind.ToString();
        if (!cache.TryGetValue(key, out var atlas))
        {
            try { atlas = new(kind,activities); }
            catch (Exception ex) when (ex is System.IO.IOException or InvalidOperationException or NotSupportedException or System.IO.FileFormatException) { atlas = null; }
            cache[key] = atlas;
        }
        return atlas;
    }
}

public sealed class CompanionSpriteVisual : FrameworkElement
{
    private CompanionSpriteAtlas? atlas;
    private CompanionSpriteAtlas? basicAtlas,activityAtlas;
    private CompanionWalkAtlas? walkAtlas;
    private CompanionTransitionAtlas? transitionAtlas;
    private int? endpoint;
    private double transitionRemaining;
    private bool transitionReversed;
    private int TransitionFrame=>transitionAtlas is null?0:Math.Clamp((int)Math.Floor((1-transitionRemaining/transitionAtlas.DurationSeconds)*4+.000001),0,3);
    private bool TransitionActive=>transitionAtlas is not null&&transitionRemaining>.000001;
    public bool IsStandingUp=>TransitionActive&&transitionReversed;
    public double TransitionRemainingSeconds=>transitionRemaining;
    private bool walking;
    private double scale;
    private PetAppearance kind;
    private int frame;
    private double facing = 1, facingTarget = 1, breath = 1, breathTarget = 1;
    private double lastPoseTime = double.NaN;
    private double poseTransition;
    private bool renderingSubscribed;
    private TimeSpan lastRender;
    private Rect drawn;
    private readonly ScaleTransform poseTransform=new(1,1,58,144);
    public bool Ready => atlas is not null;
    public int FrameIndex => frame;
    public int PresentedFrameIndex=>TransitionActive?(transitionReversed?3-TransitionFrame:TransitionFrame):!walking&&endpoint is {} stable?stable:frame;
    public Rect DrawnBounds=>drawn;
    public double WalkStrideDip => walkAtlas?.StrideDip??34;
    public static int DecodedAtlasCount => CompanionSpriteAtlas.LoadedCount+CompanionWalkAtlas.LoadedCount+CompanionTransitionAtlas.LoadedCount;
    public CompanionSpriteVisual()
    {
        Width = CharacterGeometry.CanonicalWidth; Height = CharacterGeometry.CanonicalHeight;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        IsVisibleChanged+=(_,_)=>UpdateRenderingSubscription();
        Loaded+=(_,_)=>UpdateRenderingSubscription();
        Unloaded+=(_,_)=>StopRendering();
    }
    private double walkDistance;
    public void AdvanceWalk(double distanceInCanonicalDip)
    {
        if(double.IsFinite(distanceInCanonicalDip)&&Math.Abs(distanceInCanonicalDip)<80)
            walkDistance+=Math.Abs(distanceInCanonicalDip);
    }
    public void SetCharacter(PetAppearance appearance)
    {
        kind=appearance; basicAtlas=CompanionSpriteAtlas.For(appearance);
        activityAtlas=appearance==PetAppearance.Girl?CompanionSpriteAtlas.For(appearance,true):null;
        walkAtlas=CompanionWalkAtlas.For(appearance);transitionAtlas=CompanionTransitionAtlas.For(appearance);atlas=basicAtlas;
        scale=basicAtlas?.Scale??1;frame=0;walking=false;walkDistance=0;
        endpoint=transitionAtlas is null?null:appearance==PetAppearance.Girl?0:3;transitionRemaining=0;lastPoseTime=double.NaN;
        if(endpoint is {} initial)scale=initial==0?transitionAtlas!.StandScale:transitionAtlas!.SitScale;
        UpdateBounds();InvalidateVisual();
    }
    public void Pose(BodyAction action, double time, double direction, RoomActivity? activity = null, BehaviorPhase? phase = null)
    {
        var previousAtlas=atlas;var previousFrame=frame;var wasWalking=walking;
        facingTarget = direction < 0 ? -1 : 1;
        var elapsed=double.IsNaN(lastPoseTime)?0:Math.Clamp(time-lastPoseTime,0,.1);lastPoseTime=time;
        transitionRemaining=Math.Max(0,transitionRemaining-elapsed);
        atlas=basicAtlas;
        if (kind == PetAppearance.Girl)
            frame = action switch
            {
                BodyAction.Sleep => 4,
                BodyAction.Eat when phase is BehaviorPhase.Work or BehaviorPhase.Pause => 5,
                BodyAction.DrawDoodle or BodyAction.WriteNote => 6,
                BodyAction.Walk or BodyAction.Wander or BodyAction.Explore or BodyAction.ChaseCursor or BodyAction.AvoidCursor => 1 + (int)(time * 5) % 2,
                BodyAction.Greet or BodyAction.Nuzzle => 7,
                BodyAction.Sit or BodyAction.Groom or BodyAction.RestInCorner or BodyAction.Hide => 3,
                _ => 0
            };
        else
            frame = action switch
            {
                BodyAction.Sleep => 4,
                BodyAction.Eat => 5,
                BodyAction.Groom => 6,
                BodyAction.Stretch or BodyAction.PlayToy or BodyAction.BatToy => 7,
                BodyAction.Walk or BodyAction.Wander or BodyAction.Explore or BodyAction.ChaseCursor or BodyAction.AvoidCursor => 2 + (int)(time * 5) % 2,
                BodyAction.Fall => 1,
                _ => 0
            };
        if (kind == PetAppearance.Girl && activity is not null && phase is not (BehaviorPhase.Approach or BehaviorPhase.Notice or BehaviorPhase.Stand))
        {
            frame = activity switch { RoomActivity.Feed when phase is BehaviorPhase.Work or BehaviorPhase.Pause=>5,RoomActivity.Draw or RoomActivity.Write=>6,_=>3 };
            if(activityAtlas is not null&&activity is not (RoomActivity.Feed or RoomActivity.Draw))
            {
                atlas=activityAtlas;
                frame=activity switch {RoomActivity.Computer=>0,RoomActivity.Read=>1,RoomActivity.Lego=>2,RoomActivity.HairCare=>3,RoomActivity.Write=>4,_=>5};
            }
        }
        else if(kind==PetAppearance.Girl&&activityAtlas is not null&&action==BodyAction.Stretch){atlas=activityAtlas;frame=6;}
        if(kind==PetAppearance.Girl&&phase==BehaviorPhase.Stand&&activity is not null){atlas=basicAtlas;frame=0;}
        walking=walkAtlas is not null && (action is BodyAction.Walk or BodyAction.Wander or BodyAction.Explore or BodyAction.ChaseCursor or BodyAction.AvoidCursor)
            && (activity is null || phase is BehaviorPhase.Approach or BehaviorPhase.Notice or BehaviorPhase.Stand);
        if(walking)frame=(int)Math.Floor(walkDistance/walkAtlas!.StrideDip*walkAtlas.Frames.Length+.000001)%walkAtlas.Frames.Length;
        int? desiredEndpoint=null;
        if(transitionAtlas is not null)
        {
            if(walking)desiredEndpoint=0;
            else if(ReferenceEquals(atlas,basicAtlas))
            {
                if(kind==PetAppearance.Girl&&frame is 0 or 3)desiredEndpoint=frame;
                else if(kind!=PetAppearance.Girl&&frame==0)desiredEndpoint=3;
                else if(kind!=PetAppearance.Girl&&frame==1)desiredEndpoint=0;
            }
        }
        if(desiredEndpoint!=endpoint)
        {
            if(desiredEndpoint is {} next&&endpoint is {} previous&&next!=previous)
            {transitionReversed=next==0;transitionRemaining=transitionAtlas!.DurationSeconds;}
            else transitionRemaining=0;
            endpoint=desiredEndpoint;
        }
        scale=walking?walkAtlas!.Scale:atlas?.Scale??1;
        if(TransitionActive)
        {
            var seatedProgress=(transitionReversed?3-TransitionFrame:TransitionFrame)/3d;
            scale=transitionAtlas!.StandScale+(transitionAtlas.SitScale-transitionAtlas.StandScale)*seatedProgress;
        }
        else if(!walking&&endpoint is {} stableEndpoint)
            scale=stableEndpoint==0?transitionAtlas!.StandScale:transitionAtlas!.SitScale;
        if(!walking&&(wasWalking||previousFrame!=frame||!ReferenceEquals(previousAtlas,atlas)))poseTransition=.18;
        poseTransition=Math.Max(0,poseTransition-elapsed);
        breathTarget = 1 + Math.Sin(time * (!walking&&frame == 4 ? 1.5 : 2)) * .006;
        if(!renderingSubscribed)UpdateInterpolation(elapsed>0?elapsed:.033);
        UpdateBounds();
        InvalidateVisual();
    }
    private void UpdateRenderingSubscription()
    {
        if(IsVisible&&IsLoaded&&!renderingSubscribed){CompositionTarget.Rendering+=OnRendering;renderingSubscribed=true;lastRender=default;}
        else if(!IsVisible)StopRendering();
    }
    private void StopRendering(){if(renderingSubscribed)CompositionTarget.Rendering-=OnRendering;renderingSubscribed=false;lastRender=default;}
    private void OnRendering(object? sender,EventArgs e)
    {
        if(e is not RenderingEventArgs args)return;
        var elapsed=lastRender==default?.016:Math.Clamp((args.RenderingTime-lastRender).TotalSeconds,0,.05);lastRender=args.RenderingTime;
        var oldFacing=facing;var oldBreath=breath;UpdateInterpolation(elapsed);
        if(Math.Abs(facing-oldFacing)>.00001||Math.Abs(breath-oldBreath)>.00001)InvalidateVisual();
    }
    private void UpdateInterpolation(double elapsed)
    {
        var ease=1-Math.Exp(-elapsed*18);facing+=(facingTarget-facing)*ease;breath+=(breathTarget-breath)*ease;
        poseTransform.ScaleX=facing;
        // A brief ease around the grounded pivot softens pose changes without
        // drawing two overlapping bodies or moving a planted foot off support.
        poseTransform.ScaleY=breath*(1-.025*Math.Sin(Math.PI*poseTransition/.18));
    }
    private (BitmapSource Image,byte[] Alpha,int Width,int Height)? CurrentPose
    {
        get
        {
            if(TransitionActive){var f=transitionReversed?3-TransitionFrame:TransitionFrame;var p=transitionAtlas!.Frames[f];return(p.Image,p.Alpha,p.Width,p.Height);}
            if(!walking&&endpoint is {} stable){var p=transitionAtlas!.Frames[stable];return(p.Image,p.Alpha,p.Width,p.Height);}
            if(walking&&walkAtlas is not null){var p=walkAtlas.Frames[frame];return(p.Image,p.Alpha,p.Width,p.Height);}
            if(atlas is null)return null;var pose=atlas.Frames[frame];return(pose.Image,pose.Alpha,pose.Width,pose.Height);
        }
    }
    private void UpdateBounds()
    {
        if(CurrentPose is not {} pose)return;
        scale=Math.Min(scale,Math.Min(112d/pose.Width,144d/pose.Height));
        drawn=new Rect((116-pose.Width*scale)/2,144-pose.Height*scale,pose.Width*scale,pose.Height*scale);
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (CurrentPose is not {} pose)
        {
            dc.DrawText(new FormattedText("🐾", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Emoji"), 48, Brushes.RosyBrown, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(28, 85));
            return;
        }
        // Ground is always y=144. Breathing scales around the support, never bobs the feet.
        drawn = new Rect((116 - pose.Width * scale) / 2, 144 - pose.Height * scale, pose.Width * scale, pose.Height * scale);
        dc.PushTransform(poseTransform); dc.DrawImage(pose.Image, drawn); dc.Pop();
    }
    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters)
    {
        if (CurrentPose is not {} pose) return base.HitTestCore(parameters);
        if(Math.Abs(poseTransform.ScaleX)<.02)return null;
        var p = parameters.HitPoint;
        var x = 58+(p.X-58)/poseTransform.ScaleX; var y = 144+(p.Y-144)/poseTransform.ScaleY;
        if (!drawn.Contains(new Point(x, y))) return null;
        var px = Math.Clamp((int)((x - drawn.X) / scale), 0, pose.Width - 1);
        var py = Math.Clamp((int)((y - drawn.Y) / scale), 0, pose.Height - 1);
        return pose.Alpha[py * pose.Width + px] >= 24 ? new PointHitTestResult(this, p) : null;
    }
    public object AssetDiagnostic => new
    {
        Character=kind.ToString(),Ready,ActivityAtlasReady=activityAtlas is not null,WalkAtlasReady=walkAtlas is not null,
        TransitionAtlasReady=transitionAtlas is not null,TransitionFrames=transitionAtlas?.Frames.Length??0,TransitionActive,IsStandingUp,TransitionRemainingSeconds,
        Atlas=TransitionActive||(!walking&&endpoint is not null)?transitionAtlas?.Source:walking?walkAtlas?.Source:ReferenceEquals(atlas,activityAtlas)?"girl-activities":kind.ToString(),
        Frames=walking?walkAtlas?.Frames.Select(f=>new{f.Width,f.Height}).ToArray():atlas?.Frames.Select(f=>new{f.Width,f.Height}).ToArray(),
        TransparentPixels=walking?walkAtlas?.TransparentPixels:atlas?.TransparentPixels,EffectiveScale=scale,
        WalkFrames=walkAtlas?.Frames.Length??0,WalkDistance=walkDistance,GroundPivot=new{X=58,Y=144},FrameIndex=frame,
        TurnInterpolation=true,PoseTransitionSeconds=.18,RenderCallbackAttached=renderingSubscribed
    };
}
