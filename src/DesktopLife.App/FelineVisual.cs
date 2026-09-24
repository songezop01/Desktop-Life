using System.Windows;
using System.Windows.Media;
using DesktopLife.Core;

namespace DesktopLife.App;

/// <summary>A small articulated tabby. All grounded poses share the same support line.</summary>
public sealed class FelineVisual : FrameworkElement
{
    // Physics measures the window from its top to this support plane. Keep planted
    // paws on this line; breathing and head movement must not lift the whole cat.
    public const double SupportBaseline = 144;
    public Point? RenderedPawTip { get; private set; }

    private BodyAction action;
    public PersonalityProfile Personality { get; set; } = new();
    private readonly MicroBehavior micro = new(Random.Shared.Next());
    private MicroPose microPose;
    private BehaviorPhase? behaviorPhase;
    private double time, direction = 1, gaze;
    private Point? requestedPaw;
    private PoseShape shape;
    private bool hasPose;
    private double turnProgress=1,turnFrom=1,turnTo=1,actionAge,backBlend;
    private NavigationPhase movement;
    private bool WashingFace=>action==BodyAction.Groom&&actionAge%4 is >1.35 and <3.3;
    private bool SideLying=>action==BodyAction.Sleep&&actionAge%55<18;
    public bool IsTurning=>turnProgress<1;
    private readonly List<HitRegion> hit = [];
    private Matrix hitTransform = Matrix.Identity;
    private readonly record struct HitRegion(Geometry Geometry, Matrix Transform, Pen? Pen);
    private readonly record struct PoseShape(double BodyX, double BodyY, double BodyHeight,
        double HeadX, double HeadY, double HeadAngle);

    private static readonly Brush Fur = Gradient("#F8D39A", "#DF9A53", new(0.2, 0), new(.75, 1));
    private static readonly Brush HeadFur = Gradient("#FFE0A6", "#EBA85E", new(.3, 0), new(.8, 1));
    private static readonly Brush Cream = Gradient("#FFFAE9", "#EED7B6", new(.4, 0), new(.6, 1));
    private static readonly Brush ShadedFur = Solid("#CE8B4E"), Stripe = Solid("#AA693B");
    private static readonly Brush SoftStripe = Solid("#BF8046"), Outline = Solid("#A97149");
    private static readonly Brush Eye = Gradient("#B8B875", "#668F72", new(0, 0), new(0, 1));
    private static readonly Brush Dark = Solid("#443E35"), Pink = Solid("#D6998D");
    private static readonly Brush Nose = Solid("#B77973"), FurLight = Solid("#FFE4B6");
    private static readonly Brush EarShadow = Solid("#B87864"), Whisker = Solid("#CDBB99");
    private static readonly Dictionary<(Brush, double), Pen> Pens = [];

    public void Pose(BodyAction value, double seconds, double facing, double look, Point? pawTarget = null,double? actionSeconds=null,NavigationPhase movementPhase=NavigationPhase.Idle,BehaviorPhase? phase=null)
    {
        var elapsed = seconds - time;
        behaviorPhase=phase;
        microPose=micro.Step(Math.Clamp(elapsed,0,.1),Personality,value==BodyAction.Sleep,pawTarget is not null || movementPhase!=NavigationPhase.Idle || value is BodyAction.Groom or BodyAction.Stretch);
        actionAge=actionSeconds??(action==value?Math.Max(0,actionAge+elapsed):0);
        action = value; time = seconds;movement=movementPhase;
        var desired=facing<0?-1d:1d;
        if(!hasPose||elapsed<=0||elapsed>.3){turnFrom=turnTo=direction=desired;turnProgress=1;}
        else
        {
            if(desired!=turnTo){turnFrom=direction;turnTo=desired;turnProgress=0;}
            turnProgress=Math.Min(1,turnProgress+Math.Max(0,elapsed)/.48);
            direction=turnProgress<.5?turnFrom:turnTo;
        }
        var turnBack=turnProgress<1?Math.Sin(turnProgress*Math.PI):0;
        var away=action is BodyAction.Idle or BodyAction.ObserveCursor?Math.Clamp(Math.Min(actionAge%22-8,13-actionAge%22),0,1):0;
        backBlend=Math.Max(turnBack,away);
        gaze = Math.Clamp(look * direction, -1.5, 1.5);
        requestedPaw = pawTarget is { } point
            ? new Point(direction < 0 ? 116 - point.X : point.X, point.Y) : null;
        var bob = IsWalking ? -Math.Abs(Math.Sin(time * GaitSpeed)) * (IsRunning ? 2.5 : .9) : Math.Sin(time * 1.9) * .35;
        var target = IsSeated
            ? new PoseShape(47, 119 + bob, 24, 68, 87 + bob, 0)
            : new PoseShape(49, 113 + bob, 18, 83, 96 + bob, 0);
        target = action switch
        {
            BodyAction.Stretch => new(43, 107, 20, 86, 119, 10),
            BodyAction.Eat => new(49, 114, 18, 84, 119 + Math.Sin(time * 6) * 1.3, 12),
            BodyAction.Groom => target with { HeadX = 72, HeadY = 99 + Math.Sin(time * 4) * 2, HeadAngle = 20 },
            BodyAction.Nuzzle => new(50, 114, 20, 80 + Math.Sin(time * 2.6) * 2, 99, -12),
            BodyAction.ObserveCursor or BodyAction.ObserveDesktopIcon or BodyAction.ObserveShortcut
                => target with { HeadAngle = gaze * 4 + Math.Sin(time * .8) * 1.5 },
            BodyAction.Fall => new(49, 105, 20, 82, 91, -5),
            _ => target
        };
        if(action==BodyAction.Sit&&actionAge<.8)
        {var sit=Math.Clamp(actionAge/.8,0,1);target=new(49-2*sit,113+6*sit,18+6*sit,83-15*sit,96-9*sit,0);}
        if(action==BodyAction.Stretch)
        {
            var stretch=actionAge%6;
            if(stretch<.65)target=new(47,122,16,78,105,3);
            else if(stretch>2.9)target=new(47,113,19,80,99,stretch<4.3?-6:0);
        }
        if(movement is NavigationPhase.Crouching or NavigationPhase.Landing)
            target=new(49,123,15,82,107,4);
        if(behaviorPhase==BehaviorPhase.GroomBody)target=target with{HeadX=51,HeadY=109,HeadAngle=-22};
        target=target with{BodyX=target.BodyX+microPose.Weight,HeadAngle=target.HeadAngle+microPose.Head};
        target=target with{HeadX=Lerp(target.HeadX,58,backBlend*.5)};
        // Blend the torso/head, not planted feet. Equal/earlier time means a deterministic
        // diagnostic sample and snaps directly to its pose, independent of previous poses.
        var blend = !hasPose || elapsed <= 0 || elapsed > .3 ? 1 : 1 - Math.Exp(-elapsed * 16);
        shape = new(Lerp(shape.BodyX, target.BodyX, blend), Lerp(shape.BodyY, target.BodyY, blend),
            Lerp(shape.BodyHeight, target.BodyHeight, blend), Lerp(shape.HeadX, target.HeadX, blend),
            Lerp(shape.HeadY, target.HeadY, blend), Lerp(shape.HeadAngle, target.HeadAngle, blend));
        hasPose = true; InvalidateVisual();
    }

    private bool IsRunning => action is BodyAction.ChaseCursor or BodyAction.AvoidCursor;
    private bool IsWalking => IsRunning || action is BodyAction.Walk or BodyAction.Wander or BodyAction.Explore;
    private bool IsPlaying => action is BodyAction.PlayToy or BodyAction.BatToy or BodyAction.PseudoPushIcon or BodyAction.PushShortcut;
    private bool IsSeated => !IsWalking && !IsPlaying && action is not (BodyAction.Stretch or BodyAction.Eat or BodyAction.Nuzzle or BodyAction.Fall);
    private double GaitSpeed => IsRunning ? 12.8 : 6.4;
    private static double Lerp(double from, double to, double amount) => from + (to - from) * amount;

    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters)
    {
        var point = parameters.HitPoint;
        if (direction < 0) point.X = 116 - point.X;
        foreach (var region in hit)
        {
            var local = point;
            var inverse = region.Transform;
            if (!inverse.IsIdentity && inverse.HasInverse) { inverse.Invert(); local = inverse.Transform(point); }
            if (region.Geometry.FillContains(local) || (region.Pen is { } pen && region.Geometry.StrokeContains(pen, local)))
                return new PointHitTestResult(this, parameters.HitPoint);
        }
        return null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        hit.Clear(); hitTransform = Matrix.Identity; RenderedPawTip = null;
        if (direction < 0) dc.PushTransform(new ScaleTransform(-1, 1, 58, 0));
        if (SideLying) DrawSideLying(dc);
        else if (action == BodyAction.Sleep) DrawSleeping(dc);
        else if(backBlend>.82)DrawBack(dc);
        else
        {
            DrawTail(dc);
            DrawLeg(dc, front: false, far: true);
            DrawLeg(dc, front: true, far: true);
            DrawTorso(dc);
            DrawLeg(dc, front: false, far: false);
            DrawLeg(dc, front: true, far: false);
            DrawHead(dc);
            if(WashingFace)DrawLeg(dc,front:true,far:false);
        }
        if (direction < 0) dc.Pop();
    }

    private void DrawTail(DrawingContext dc)
    {
        var wag = Math.Sin(time * (IsPlaying ? 4.4 : 1.7));
        var tipX = 12 + wag * (IsPlaying ? 7 : 4);
        var tipY = (action == BodyAction.Stretch ? 82 : 79) + Math.Sin(time * 2.1) * 3;
        if (action == BodyAction.Fall) { tipX = 9; tipY = 95; }
        if (IsRunning) { tipX = 6; tipY = 109 + Math.Sin(time * 4.5) * 5; }
        var start = new Point(24, shape.BodyY + 6);
        var c1 = new Point(4, shape.BodyY + 18);
        var c2 = new Point(2, tipY - 7);
        var end = new Point(tipX, tipY);
        Curve(dc, start, c1, c2, end, PenFor(ShadedFur, 9), true);
        Curve(dc, new(24, shape.BodyY + 5), new(6, shape.BodyY + 14), new(3, tipY - 5), end, PenFor(Fur, 6));
        // Rings follow the curve tangent rather than floating over a swaying tail.
        foreach (var t in new[] { .28, .51, .76 })
        {
            var p = Cubic(start, c1, c2, end, t);
            var next = Cubic(start, c1, c2, end, t + .025);
            var tangent = next - p;
            if (tangent.Length < .001) continue;
            tangent.Normalize();
            var normal = new Vector(-tangent.Y, tangent.X) * 3.3;
            Line(dc, p - normal, p + normal, SoftStripe, 2.4);
        }
    }

    private void DrawTorso(DrawingContext dc)
    {
        var x = shape.BodyX; var y = shape.BodyY;
        Ellipse(dc, x, y, IsSeated ? 25 : 34, shape.BodyHeight, Fur, true, Outline, .7);
        if (IsSeated)
        {
            Ellipse(dc, 63, 116, 15, 25, HeadFur, true);
            Shape(dc, c =>
            {
                c.BeginFigure(new(53, 103), true, true);
                c.BezierTo(new(55, 112), new(50, 119), new(57, 131), true, false);
                c.LineTo(new(60, 127), true, false); c.LineTo(new(64, 133), true, false);
                c.BezierTo(new(72, 125), new(76, 111), new(74, 99), true, false);
            }, Cream);
            Curve(dc, new(24, 130), new(23, 116), new(39, 111), new(43, 125), PenFor(SoftStripe, 1.2));
        }
        else
        {
            Shape(dc, c =>
            {
                c.BeginFigure(new(34, y + 10), true, true);
                c.BezierTo(new(44, y + 19), new(66, y + 21), new(78, y + 9), true, false);
                c.BezierTo(new(68, y + 5), new(57, y + 11), new(34, y + 10), true, false);
            }, Cream);
            Ellipse(dc, 74, y - 1, 12, 15, HeadFur);
        }
        for (var i = 0; i < 4; i++)
        {
            var sx = 29 + i * 8;
            var sy = y - shape.BodyHeight + 4 + Math.Abs(i - 1.5) * .8;
            Curve(dc, new(sx, sy), new(sx - 2, sy + 3), new(sx - 3, sy + 6), new(sx - 1, sy + 9), PenFor(SoftStripe, i % 2 == 0 ? 2.5 : 2));
        }
        Line(dc, new(x - 16, y - 8), new(x - 20, y - 11), FurLight, .8);
        Line(dc, new(x - 13, y - 12), new(x - 16, y - 15), FurLight, .8);
        Line(dc, new(x - 19, y + 2), new(x - 23, y + 4), FurLight, .8);
        Line(dc, new(x - 12, y + 10), new(x - 16, y + 12), FurLight, .8);
    }

    private void DrawLeg(DrawingContext dc, bool front, bool far)
    {
        var seated = IsSeated;
        var rootX = front ? (seated ? 66.0 : 76.0) : (seated ? 37.0 : 30.0);
        if (far) rootX -= 8;
        var rootY = front ? shape.BodyY : shape.BodyY + 2;
        double footX = rootX, footY = SupportBaseline - 3.5;
        var phase = time * GaitSpeed + (front ? 0 : Math.PI * .65) + (far ? Math.PI : 0);
        if (IsWalking)
        {
            var cycle = phase / (Math.PI * 2) % 1;
            if (cycle < 0) cycle++;
            const double stance = .64;
            var stride = IsRunning ? 23 : 13;
            // Feet stay planted through most of the cycle, then lift for a shorter swing.
            if (cycle < stance) footX += stride * (.5 - cycle / stance);
            else
            {
                var swing = (cycle - stance) / (1 - stance);
                footX += stride * (-.5 + swing);
                footY -= Math.Sin(swing * Math.PI) * (IsRunning ? 11 : 7);
            }
        }
        if (action == BodyAction.Stretch)
        {
            if(actionAge%6 is >=.65 and <=2.9){footX = front ? (far ? 87 : 99) : (far ? 22 : 32);if(front)rootY+=8;}
            else if(actionAge%6 is >2.9 and <4.3&&!front&&!far){footX=13;footY=134;}
        }
        if (action == BodyAction.Fall) { footX += front ? 5 : -3; footY = 124 + (far ? -4 : 0); }
        if (!far && front && IsPlaying)
        {
            if (requestedPaw is { } target)
            {
                footX = Math.Clamp(target.X, 6, 110); footY = Math.Clamp(target.Y, 72, 140);
                var reach = new Vector(footX - rootX, footY - rootY);
                if (reach.Length > 58) { reach.Normalize(); footX = rootX + reach.X * 58; footY = rootY + reach.Y * 58; }
            }
            else
            {
                var tap = .5 - .5 * Math.Cos(time * 7);
                footX = 88 + tap * 15; footY = 135 - Math.Sin(tap * Math.PI) * 17;
            }
            RenderedPawTip = new(direction < 0 ? 116 - footX : footX, footY);
        }
        if (!far && front && action == BodyAction.Groom)
        {
            if(WashingFace){footX=shape.HeadX+8+Math.Sin(time*6)*4;footY=shape.HeadY-3+Math.Sin(time*6)*6;}
            else{footX=83;footY=115+Math.Sin(time*4)*2;}
        }
        if (seated && !front)
        {
            Ellipse(dc, footX + 1, SupportBaseline - 4, 10, 4, far ? ShadedFur : Cream, true);
            return;
        }
        var kneeX = front ? Lerp(rootX, footX, .4) - 1 : rootX + 7;
        var kneeY = Lerp(rootY, footY, .52);
        var limb = far ? ShadedFur : Fur;
        Curve(dc, new(rootX, rootY), new(kneeX, kneeY), new(footX - 1, footY - 7), new(footX, footY - 1), PenFor(limb, front ? 8 : 10), true);
        if (!far) Line(dc, new(footX - 3, footY - 9), new(footX + 2, footY - 8), SoftStripe, 1.8);
        Ellipse(dc, footX, footY, 6.6, 3.5, far ? HeadFur : Cream, true);
        if (!far)
        {
            Line(dc, new(footX + 1.8, footY + .8), new(footX + 2, footY + 2.4), Outline, .55);
            Line(dc, new(footX + 4, footY + .5), new(footX + 4.2, footY + 1.8), Outline, .55);
        }
    }

    private void DrawHead(DrawingContext dc)
    {
        var hx = shape.HeadX; var hy = shape.HeadY;
        var rotation = new TransformGroup();
        rotation.Children.Add(new ScaleTransform(1-backBlend*.3,1,hx,hy));
        rotation.Children.Add(new RotateTransform(shape.HeadAngle,hx,hy));
        dc.PushTransform(rotation); hitTransform = rotation.Value;
        var alert = IsPlaying || action is BodyAction.ObserveCursor or BodyAction.ChaseCursor or BodyAction.Greet;
        var flattened = action is BodyAction.AvoidCursor or BodyAction.Fall;
        var earFlick = microPose.Ear;
        DrawEar(dc, hx - 14, hy - 13, -12 + gaze * 6 + earFlick + (flattened ? -28 : 0));
        DrawEar(dc, hx + 14, hy - 13, 13 + gaze * 8 - earFlick * .55 + (flattened ? 28 : 0));
        Shape(dc, c =>
        {
            c.BeginFigure(new(hx - 20, hy - 7), true, true);
            c.BezierTo(new(hx - 18, hy - 22), new(hx + 15, hy - 23), new(hx + 20, hy - 6), true, false);
            c.LineTo(new(hx + 23, hy + 5), true, false); c.LineTo(new(hx + 19, hy + 5), true, false);
            c.LineTo(new(hx + 22, hy + 10), true, false); c.LineTo(new(hx + 17, hy + 9), true, false);
            c.BezierTo(new(hx + 12, hy + 24), new(hx - 12, hy + 23), new(hx - 19, hy + 10), true, false);
            c.LineTo(new(hx - 24, hy + 11), true, false); c.LineTo(new(hx - 21, hy + 5), true, false);
            c.LineTo(new(hx - 25, hy + 4), true, false);
            c.BezierTo(new(hx - 22, hy + 1), new(hx - 22, hy - 4), new(hx - 20, hy - 7), true, false);
        }, HeadFur, PenFor(Outline, .65), true);
        Shape(dc, c =>
        {
            c.BeginFigure(new(hx - 3, hy + 2), true, true);
            c.BezierTo(new(hx - 11, hy + 5), new(hx - 19, hy + 8), new(hx - 12, hy + 15), true, false);
            c.BezierTo(new(hx - 6, hy + 23), new(hx + 11, hy + 21), new(hx + 16, hy + 12), true, false);
            c.BezierTo(new(hx + 18, hy + 7), new(hx + 7, hy + 6), new(hx + 4, hy + 2), true, false);
        }, Cream);
        Curve(dc, new(hx - 9, hy - 16), new(hx - 6, hy - 14), new(hx - 7, hy - 10), new(hx - 4, hy - 9), PenFor(Stripe, 2));
        Curve(dc, new(hx - 1, hy - 19), new(hx + 1, hy - 16), new(hx - 1, hy - 12), new(hx + 1, hy - 11), PenFor(Stripe, 2));
        Curve(dc, new(hx + 9, hy - 16), new(hx + 6, hy - 14), new(hx + 7, hy - 10), new(hx + 5, hy - 9), PenFor(Stripe, 2));
        Line(dc, new(hx - 21, hy + 2), new(hx - 16, hy + 4), SoftStripe, 1.8);
        Line(dc, new(hx + 21, hy + 2), new(hx + 16, hy + 4), SoftStripe, 1.8);
        var blink = microPose.Blink;
        var relaxed = action is BodyAction.Nuzzle or BodyAction.Groom or BodyAction.RestInCorner;
        var lid = relaxed ? .42 + Math.Sin(time * 1.6) * .12 : blink;
        if (action == BodyAction.Nuzzle) lid = Math.Max(lid, .87);
        var pupil = alert ? 3.4 + Math.Sin(time * 1.1) * .2 : 1.65 + Math.Sin(time * .8) * .35;
        if (flattened) pupil = 3.9;
        DrawEye(dc, hx - 8.5, hy - .5, lid, pupil, gaze);
        DrawEye(dc, hx + 11, hy - .5, lid, pupil, gaze);
        Ellipse(dc, hx - 5, hy + 10, 6, 4.7, Cream);
        Ellipse(dc, hx + 7, hy + 10, 6, 4.7, Cream);
        Shape(dc, c =>
        {
            c.BeginFigure(new(hx - 2.7, hy + 7), true, true);
            c.QuadraticBezierTo(new(hx + 1, hy + 5), new(hx + 4.7, hy + 7), true, false);
            c.QuadraticBezierTo(new(hx + 4, hy + 9), new(hx + 1, hy + 10.3), true, false);
        }, Nose);
        Line(dc, new(hx + 1, hy + 10), new(hx + 1, hy + 12), Dark, .7);
        Curve(dc, new(hx - 4, hy + 12), new(hx - 2, hy + 14), new(hx, hy + 14), new(hx + 1, hy + 12), PenFor(Dark, .75));
        Curve(dc, new(hx + 1, hy + 12), new(hx + 3, hy + 14), new(hx + 5, hy + 14), new(hx + 7, hy + 12), PenFor(Dark, .75));
        if (action == BodyAction.Greet && Math.Sin(time * 3) > .45)
        {
            Ellipse(dc, hx + 1, hy + 14.4, 3.4, 2.8, Dark);
            Ellipse(dc, hx + 1, hy + 16, 2.2, 1, Pink);
        }
        if ((action==BodyAction.Eat||action==BodyAction.Groom&&!WashingFace) && Math.Sin(time * 7) > .2)
            Ellipse(dc, hx + 2.5, hy + 15.7, 2, 2.4, Pink);
        for (var i = 0; i < 3; i++)
        {
            var spread = (i - 1) * 3.5;
            Line(dc, new(hx - 10, hy + 9 + i * 1.6), new(Math.Max(1, hx - 30), hy + 10 + spread), Whisker, .65);
            Line(dc, new(hx + 12, hy + 9 + i * 1.6), new(Math.Min(114, hx + 30), hy + 10 + spread), Whisker, .65);
        }
        Line(dc, new(hx - 16, hy + 13), new(hx - 13, hy + 15), FurLight, .7);
        Line(dc, new(hx + 15, hy + 13), new(hx + 12, hy + 16), FurLight, .7);
        dc.Pop(); hitTransform = Matrix.Identity;
    }

    private void DrawEar(DrawingContext dc, double x, double y, double angle)
    {
        var rotation = new RotateTransform(angle, x, y);
        dc.PushTransform(rotation);
        var old = hitTransform;
        var combined = rotation.Value; combined.Append(old); hitTransform = combined;
        Shape(dc, c =>
        {
            c.BeginFigure(new(x - 9, y + 4), true, true);
            c.QuadraticBezierTo(new(x - 11, y - 11), new(x - 6, y - 23), true, false);
            c.QuadraticBezierTo(new(x + 4, y - 18), new(x + 10, y + 2), true, false);
        }, HeadFur, PenFor(Outline, .7), true);
        Shape(dc, c =>
        {
            c.BeginFigure(new(x - 5, y - 15), true, true);
            c.LineTo(new(x - 5, y), true, false);
            c.QuadraticBezierTo(new(x + 1, y - 3), new(x + 5, y), true, false);
        }, Pink);
        Line(dc, new(x - 4, y - 12), new(x - 3, y - 5), EarShadow, .65);
        Line(dc, new(x - 6, y - 1), new(x - 1, y - 5), Cream, 1);
        Line(dc, new(x - 4, y + 1), new(x + 2, y - 4), Cream, .8);
        dc.Pop(); hitTransform = old;
    }

    private void DrawEye(DrawingContext dc, double x, double y, double lid, double pupil, double look)
    {
        var open = Math.Clamp(1 - lid, .03, 1);
        var height = 5.5 * open;
        if (open < .18)
        {
            Curve(dc, new(x - 5.8, y), new(x - 2, y + 2.3), new(x + 2, y + 2.3), new(x + 5.8, y - .5), PenFor(Dark, 1));
            return;
        }
        var eyeShape = GeometryFor(c =>
        {
            c.BeginFigure(new(x - 6.4, y), true, true);
            c.BezierTo(new(x - 4.3, y - height), new(x + 3.2, y - height - 1), new(x + 6, y - .3), true, false);
            c.BezierTo(new(x + 3.9, y + height), new(x - 3.9, y + height), new(x - 6.4, y), true, false);
        });
        dc.DrawGeometry(Eye, PenFor(Dark, .85), eyeShape);
        dc.PushClip(eyeShape);
        Ellipse(dc, x + look, y + .4, pupil, 6, Dark);
        Ellipse(dc, x - 1.4 + look * .4, y - 1.5, 1.4, 1.6, Brushes.White);
        Ellipse(dc, x + 2.1, y + 2, .65, .7, FurLight);
        dc.Pop();
        Curve(dc, new(x - 6.4, y - .5), new(x - 3, y - height - 1), new(x + 3, y - height - 1), new(x + 6, y - .5), PenFor(Dark, 1));
    }

    private void DrawSleeping(DrawingContext dc)
    {
        var breath = Math.Sin(time * 1.8) * .65;
        Ellipse(dc, 55, 122 - breath / 2, 38, 22 + breath / 2, Fur, true, Outline, .7);
        Ellipse(dc, 70, 133, 24, 10, Cream, true);
        for (var i = 0; i < 4; i++)
            Curve(dc, new(31 + i * 10, 104 - breath), new(28 + i * 10, 107), new(30 + i * 10, 111), new(28 + i * 10, 115), PenFor(SoftStripe, 2.4));
        Shape(dc, c =>
        {
            c.BeginFigure(new(59, 117), true, true);
            c.LineTo(new(56, 99), true, false); c.LineTo(new(70, 108), true, false);
            c.LineTo(new(88, 105), true, false); c.LineTo(new(96, 99), true, false);
            c.LineTo(new(95, 121), true, false);
        }, HeadFur, PenFor(Outline, .7), true);
        Ellipse(dc, 78, 120, 22, 17, HeadFur, true);
        Ellipse(dc, 78, 128, 15, 8, Cream);
        Curve(dc, new(63, 119), new(66, 123), new(70, 123), new(74, 120), PenFor(Dark, 1.1));
        Curve(dc, new(81, 120), new(85, 123), new(89, 122), new(92, 118), PenFor(Dark, 1.1));
        Ellipse(dc, 79, 126, 2.8, 1.8, Nose);
        Line(dc, new(75, 107), new(75, 113), Stripe, 1.7);
        Line(dc, new(82, 107), new(81, 113), Stripe, 1.7);
        Line(dc, new(68, 128), new(53, 126), Whisker, .7);
        Line(dc, new(88, 128), new(101, 125), Whisker, .7);
        Curve(dc, new(23, 122), new(17, 140), new(65, 146), new(86, 135), PenFor(ShadedFur, 9), true);
        Curve(dc, new(24, 121), new(19, 138), new(65, 143), new(86, 134), PenFor(Fur, 6));
        Line(dc, new(38, 138), new(40, 143), SoftStripe, 2.7);
        Line(dc, new(55, 138), new(56, 143), SoftStripe, 2.7);
        Ellipse(dc, 83, 138.5, 8, 4.2, Cream, true);
    }
    private void DrawBack(DrawingContext dc)
    {
        Curve(dc,new(57,126),new(85,143),new(103,95),new(91,82),PenFor(Fur,8),true);
        Ellipse(dc,57,117,25,26,Fur,true,Outline,.7);
        for(var i=0;i<4;i++)Curve(dc,new(44,102+i*7),new(51,108+i*7),new(63,108+i*7),new(70,102+i*7),PenFor(SoftStripe,2.2));
        DrawEar(dc,43,76,-17);DrawEar(dc,72,76,17);
        Ellipse(dc,58,88,23,22,HeadFur,true,Outline,.7);
        for(var i=0;i<3;i++)Curve(dc,new(51+i*7,69),new(49+i*7,76),new(52+i*6,79),new(50+i*7,85),PenFor(SoftStripe,2));
        Line(dc,new(38,94),new(34,97),FurLight,1);Line(dc,new(76,94),new(81,97),FurLight,1);
        Ellipse(dc,40,140,9,4,Cream,true);Ellipse(dc,76,140,9,4,Cream,true);
    }
    private void DrawSideLying(DrawingContext dc)
    {
        var breath=Math.Sin(time*1.8)*.6;
        Curve(dc,new(29,123),new(1,115),new(14,145),new(39,139),PenFor(Fur,8),true);
        Ellipse(dc,55,126-breath/2,36,18+breath/2,Fur,true,Outline,.7);
        Ellipse(dc,60,134,26,9,Cream,true);
        for(var i=0;i<4;i++)Curve(dc,new(31+i*9,111),new(30+i*9,117),new(35+i*9,121),new(33+i*9,124),PenFor(SoftStripe,2));
        DrawEar(dc,76,114,-38);DrawEar(dc,97,110,12);
        Ellipse(dc,87,122,20,16,HeadFur,true,Outline,.7);Ellipse(dc,90,131,14,8,Cream);
        Curve(dc,new(76,122),new(80,125),new(84,125),new(87,122),PenFor(Dark,1));
        Curve(dc,new(93,120),new(96,122),new(100,122),new(103,119),PenFor(Dark,1));
        Ellipse(dc,92,129,2.8,1.7,Nose);
        Ellipse(dc,37,140,10,4,Cream,true);Ellipse(dc,69,140,11,4,Cream,true);
        Line(dc,new(100,131),new(114,127),Whisker,.7);Line(dc,new(100,133),new(114,133),Whisker,.7);
    }

    private void Ellipse(DrawingContext dc, double x, double y, double rx, double ry, Brush fill,
        bool target = false, Brush? outline = null, double width = 1)
    {
        var geometry = new EllipseGeometry(new(x, y), rx, ry);
        var pen = outline is null ? null : PenFor(outline, width);
        dc.DrawGeometry(fill, pen, geometry);
        if (target) hit.Add(new(geometry, hitTransform, pen));
    }
    private void Shape(DrawingContext dc, Action<StreamGeometryContext> draw, Brush? fill, Pen? pen = null, bool target = false)
    {
        var geometry = GeometryFor(draw);
        dc.DrawGeometry(fill, pen, geometry);
        if (target) hit.Add(new(geometry, hitTransform, pen));
    }
    private void Curve(DrawingContext dc, Point start, Point c1, Point c2, Point end, Pen pen, bool target = false)
        => Shape(dc, c => { c.BeginFigure(start, false, false); c.BezierTo(c1, c2, end, true, false); }, null, pen, target);
    private static void Line(DrawingContext dc, Point start, Point end, Brush brush, double width)
        => dc.DrawLine(PenFor(brush, width), start, end);
    private static StreamGeometry GeometryFor(Action<StreamGeometryContext> draw)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open()) draw(context);
        geometry.Freeze(); return geometry;
    }
    private static Pen PenFor(Brush brush, double width)
    {
        if (Pens.TryGetValue((brush, width), out var pen)) return pen;
        pen = new(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze(); Pens[(brush, width)] = pen; return pen;
    }
    private static Brush Solid(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); brush.Freeze(); return brush;
    }
    private static Brush Gradient(string first, string second, Point start, Point end)
    {
        var brush = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(first),
            (Color)ColorConverter.ConvertFromString(second), start, end); brush.Freeze(); return brush;
    }
    private static Point Cubic(Point a, Point b, Point c, Point d, double t)
    {
        var s = 1 - t;
        return new(s * s * s * a.X + 3 * s * s * t * b.X + 3 * s * t * t * c.X + t * t * t * d.X,
            s * s * s * a.Y + 3 * s * s * t * b.Y + 3 * s * t * t * c.Y + t * t * t * d.Y);
    }
}
