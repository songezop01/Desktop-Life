using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    private RoomWindow? requestedTeaser;
    private RoomWindow? adjustedTeaser;
    private readonly TeaserPawVisual teaserPawVisual = new();
    private readonly ObservedPlayExposure teaserPlayExposure = new();
    private static readonly Dictionary<PetAppearance,HangingToySilhouette> teaserContactSilhouettes=[];
    internal Point? TeaserRenderedPawTip => teaserPawVisual.RenderedTip;

    internal bool TryStartTeaserActivity(RoomWindow surface)
    {
        if (!IsVisible || paused || EditingRoom || Interacting || DirectCareHolding || !petGravity.Grounded || FinishingMotion || HouseMotionActive ||
            sequence is { Finished: false } || !Furniture.Contains(surface) || !CanPlayTeaser(surface) ||
            !Occupancy.Available(surface.Item.Id.ToString(), appearance)) return false;
        requestedToy = null; requestedTeaser = surface;
        if (!RequestAction(BodyAction.PlayToy, BehaviorInterruptReason.Stimulus)) { requestedTeaser = null; return false; }
        requestedTeaser = null;
        return teaserTarget == surface && sequence is { Kind: SequenceKind.Play } && AttentionTarget == surface.Item.Id.ToString();
    }

    private bool TryTeaserStance(RoomWindow target, out HangingToyStance stance)
    {
        stance = null!;
        if (target.Teaser is not { } hanging || target.Platforms.Count < 2 || !HangingToyInteraction.Allowed(appearance)) return false;
        var ball = target.TeaserPosition;
        var anchor = new RoomPoint(ball.X - hanging.X * target.SceneScale, ball.Y - hanging.Y * target.SceneScale);
        var floor = House is { } house ? house.Floors[Math.Clamp(target.Item.FloorIndex, 0, house.FloorCount - 1)].Platform :
            new RoomPlatform(Bounds().Left, Bounds().Width, Bounds().Top + Bounds().Height, Bounds().Top + Bounds().Height);
        var found = HangingToyInteraction.Plan(appearance, anchor, target.Platforms[1], floor, Bounds(), BodyWidth, BodyHeight, target.SceneScale);
        if (found is null) return false;
        stance = found; return true;
    }

    private bool TryTeaserApproach(RoomWindow target, out ObjectApproach approach)
    {
        approach = default;
        if (!TryTeaserStance(target, out var stance)) return false;
        approach = new(stance.X, stance.Y, stance.Facing, 0); return true;
    }

    private ObjectApproach TeaserApproach(RoomWindow target) => TryTeaserApproach(target, out var approach) ? approach :
        new(body.X, body.Y, facing, 0);

    private void AdjustTeaserLength(RoomWindow target)
    {
        if (!TryTeaserStance(target, out var stance)) return;
        if (adjustedTeaser != target) { RestoreTeaserLength(); adjustedTeaser = target; }
        target.Teaser!.SetLength(stance.RopeLength);
    }

    private void RestoreTeaserLength()
    {
        adjustedTeaser?.Teaser?.SetLength(HangingToy.Length);
        adjustedTeaser = null; teaserPawTarget = null;
        teaserPawVisual.SetContext(appearance, null);
    }

    private RoomPoint? TeaserContact(RoomWindow target) => HangingToyInteraction.ContactPoint(appearance,
        new(target.TeaserPosition.X, target.TeaserPosition.Y), body.X, body.Y, BodyHeight, target.SceneScale);

    private bool TeaserBallIsVisible(RoomWindow target)
    {
        if(!sprite.Ready||CompanionSpriteAtlas.For(appearance) is not {} atlas)return false;
        if(!teaserContactSilhouettes.TryGetValue(appearance,out var future))
        {
            var frame=atlas.Frames[7];var imageScale=Math.Min(atlas.Scale,Math.Min(112d/frame.Width,144d/frame.Height));
            // Cache the canonical high-quality raster, including filtered edge
            // pixels. Source alpha alone misses pixels created by downsampling.
            var drawing=new DrawingVisual();RenderOptions.SetBitmapScalingMode(drawing,BitmapScalingMode.HighQuality);
            using(var dc=drawing.RenderOpen())dc.DrawImage(frame.Image,new Rect((116-frame.Width*imageScale)/2,
                144-frame.Height*imageScale,frame.Width*imageScale,frame.Height*imageScale));
            var raster=new RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);raster.Render(drawing);
            var pixels=new byte[116*144*4];raster.CopyPixels(pixels,116*4,0);
            var alpha=new byte[116*144];for(var i=0;i<alpha.Length;i++)alpha[i]=pixels[i*4+3];
            teaserContactSilhouettes[appearance]=future=HangingToySilhouette.FromAlpha(alpha,116,144,1);
        }
        var scale=BodyHeight/CharacterGeometry.CanonicalHeight;var ball=target.TeaserPosition;
        var local=new RoomPoint((ball.X-body.X)/scale,(ball.Y-body.Y)/scale);
        var radius=HangingToyInteraction.ContactRadius*target.SceneScale/scale;
        if(!future.IsSphereClear(local,radius))return false;
        // The future play raster and both facing directions are covered above.
        // Also reject an overlap with the currently presented transition/idle pose.
        for(var y=Math.Max(0,(int)Math.Floor(local.Y-radius));y<=Math.Min(143,(int)Math.Ceiling(local.Y+radius));y++)
            for(var x=Math.Max(0,(int)Math.Floor(local.X-radius));x<=Math.Min(115,(int)Math.Ceiling(local.X+radius));x++)
                if(Math.Pow(x-local.X,2)+Math.Pow(y-local.Y,2)<=Math.Pow(radius+.5,2)&&sprite.PaintedAt(new(x,y)))return false;
        return true;
    }

    private void SetTeaserReach(RoomPoint contact, double progress)
    {
        var scale = BodyHeight / CharacterGeometry.CanonicalHeight;
        var local = new Point((contact.X - body.X) / scale, (contact.Y - body.Y) / scale);
        var ready = new Point(local.X < 58 ? 34 : 82, 128);
        teaserPawTarget = ready + (local - ready) * Math.Clamp(progress, 0, 1);
        poseAction = BodyAction.PlayToy;
    }

    private bool ApplyTeaserContact(RoomWindow target, RoomPoint? contact, double reach, double contactSeconds = 0)
    {
        if (contact is not { } point || !petGravity.Grounded || Interacting || EditingRoom || paused || !CanPlayTeaser(target) ||
            !TryTeaserStance(target, out var stance) || Math.Abs(body.Y + BodyHeight - stance.Support.Y) > target.SceneScale ||
            Math.Abs(body.X - stance.X) > Math.Max(1.5, target.SceneScale)) return false;
        SetTeaserReach(point, reach);
        if (reach < 1 || target.Teaser is not { } hanging || Math.Abs(hanging.RopeLength - hanging.TargetLength) > .25 ||
            !sprite.Ready || sprite.Visibility != Visibility.Visible || sprite.FrameIndex != 7 || sprite.PresentedFrameIndex != 7) return false;
        var scale = BodyHeight / CharacterGeometry.CanonicalHeight;
        var tip = new Point(body.X + teaserPawTarget!.Value.X * scale, body.Y + teaserPawTarget.Value.Y * scale);
        var ball = target.TeaserPosition;
        if (Math.Abs((tip - ball).Length - HangingToyInteraction.ContactRadius * target.SceneScale) > .25 * target.SceneScale) return false;
        // A swing can briefly pass behind the crouching silhouette. Wait for
        // the visible ball instead of presenting an invisible impact through a head.
        if(!TeaserBallIsVisible(target))return false;
        // The exact displayed raster paw tip reaches the real circumference before
        // the pendulum receives momentum or the sequence can earn play benefit.
        hanging.Bat(facing * 125); TeaserContactCount++;
        teaserPlayExposure.Record(appearance, TeaserContactCount, Math.Clamp(contactSeconds, 0, .1));
        return true;
    }

    private bool TickFreeTeaser(double dt, double now, double speed)
    {
        if (teaserTarget is not { } target || !CanPlayTeaser(target) || !TryTeaserApproach(target, out var approach)) return false;
        AdjustTeaserLength(target);
        MovePetToward(approach.X, approach.Y, dt, Bounds(), speed);
        facing = approach.PushX;
        if (Math.Abs(body.X - approach.X) > 2 || Math.Abs(body.Y - approach.Y) > 2 || !petGravity.Grounded)
        { poseAction = BodyAction.Walk; lastTeaserTap = Math.Max(lastTeaserTap, now - .9); return true; }
        poseAction = BodyAction.ObserveCursor;
        var since = now - lastTeaserTap;
        if (since >= .9 && TeaserContact(target) is { } contact)
        {
            SetTeaserReach(contact, Math.Clamp((since - .9) / .2, 0, 1));
            if (since >= 1.1 && ApplyTeaserContact(target, contact, 1, dt)) lastTeaserTap = now;
        }
        return true;
    }

    private void UpdateTeaserPawVisual()
    {
        if (!Character.Children.Contains(teaserPawVisual)) Character.Children.Add(teaserPawVisual);
        teaserPawVisual.SetContext(appearance, teaserTarget is not null && !paused && !EditingRoom ? teaserPawTarget : null);
    }
}

/// <summary>A small illustrated forepaw layer; the complete character stays in its atlas.</summary>
internal sealed class TeaserPawVisual : FrameworkElement
{
    private BitmapSource? paw;
    private PetAppearance character;
    private Point? tip;
    internal Point? RenderedTip { get; private set; }
    internal BitmapSource? PawAsset => paw;
    internal TeaserPawVisual() { Width = 116; Height = 144; IsHitTestVisible = false; RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality); }
    internal void SetContext(PetAppearance kind, Point? contact)
    {
        if (paw is null || character != kind)
        {
            character = kind; paw = null;
            if (HangingToyInteraction.Allowed(kind) && CompanionSpriteAtlas.For(kind)?.Frames[0] is { } source)
            {
                // The lowest front white paw, including its painted toe lines.
                // The diagnostic exports this exact crop for visual review.
                var rect = new Int32Rect((int)(source.Width * .70), (int)(source.Height * .91),
                    Math.Max(1, (int)(source.Width * .105)), Math.Max(1, (int)(source.Height * .085)));
                var crop = new CroppedBitmap(source.Image, rect); crop.Freeze(); paw = crop;
            }
        }
        tip = contact; RenderedTip = null;
        Visibility = contact is not null && paw is not null ? Visibility.Visible : Visibility.Collapsed;
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext drawing)
    {
        if (tip is not { } end || paw is null) { RenderedTip = null; return; }
        var shoulder = new Point(end.X < 58 ? 43 : 73, 113);
        var vector = end - shoulder;
        if (vector.Length > 52 || end.X is < 1 or > 115 || end.Y is < 76 or > 140) { RenderedTip = null; return; }
        var imageBrush = new ImageBrush(paw) { Stretch = Stretch.Fill }; imageBrush.Freeze();
        // Only a narrow limb is textured. No second body, face or tail is drawn.
        var pen = new Pen(imageBrush, character == PetAppearance.Cat ? 8 : 9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        drawing.DrawLine(pen, shoulder, end);
        var pawBounds = new Rect(end.X - 8, end.Y - 5, 16, 10);
        drawing.PushClip(new EllipseGeometry(pawBounds)); drawing.DrawImage(paw, pawBounds); drawing.Pop();
        RenderedTip = end;
    }
}
