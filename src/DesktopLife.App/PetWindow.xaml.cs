using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using DesktopLife.Core;
using DesktopLife.Windows;

namespace DesktopLife.App;
public partial class PetWindow : Window, IAnimationController
{
    private readonly DesktopBody body = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly FelineVisual feline=new(){Width=116,Height=144,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0)};
    private double previous;
    private double speechUntil;

    private PetAppearance appearance;
    public PetState EmotionalState {get;set;}=new();
    public void Say(string text,double seconds=4)
    {SpeechText.Text=text;SpeechBubble.Visibility=Visibility.Visible;speechUntil=clock.Elapsed.TotalSeconds+seconds;}
    private readonly RotateTransform headTilt=new(),earLeft=new(),earRight=new();
    private readonly TranslateTransform eyeGaze=new();
    public ToyWindow Square {get;}
    public ToyWindow Ball {get;}
    public IEnumerable<Window> Surfaces=>Furniture.Cast<Window>().Concat(new Window[]{Art,this,Square,Ball}).Concat(ExtraToys);
    private readonly SurfaceDrag drag;
    private double interactionUntil;
    public bool Interacting=>drag.Active||clock.Elapsed.TotalSeconds<interactionUntil;
    public string DragDiagnostic=>drag.Diagnostic;
    public event Action<BodyAction>? InteractionRequested;
    private double lastToyKick,lastAttention,lastShortcutPush;
    public IReadOnlyList<ShortcutTarget> Shortcuts {get;set;}=[];
    private string? selectedShortcut;
    private bool shortcutPushed;
    public event Action<string,double,double>? ShortcutPushRequested;
    public bool AllowCursorAttraction {get;set;}=true;
    private (double X,double Y)? previousCursor;
    public ArtWindow Art { get; } = new();
    private (double X,double Y)? cursor;
    private (double X,double Y) target;
    private bool paused;
    public BehaviorParameters Parameters {get;private set;}=BehaviorParameters.Default;
    public bool HasCreativeOutput {get;set;}
    public Func<BodyAction,BehaviorParameters>? ParameterFactory {get;set;}
    private double actionStarted,lastTarget;
    private Random movementRandom=new(1);
    private BodyAction? poseAction;
    private readonly System.Windows.Media.TranslateTransform poseOffset=new();
    private readonly System.Windows.Media.TranslateTransform gazeOffset=new();
    private readonly RotateTransform bodyTilt=new(), armL=new(),armR=new(),legL=new(),legR=new(),tailSwing=new();
    private readonly ScaleTransform bodyScale=new();
    public void SetAppearance(PetAppearance appearance)
    {
        this.appearance=appearance;
        GirlVisual.Visibility=appearance==PetAppearance.Girl?Visibility.Visible:Visibility.Collapsed;
        CatVisual.Visibility=appearance==PetAppearance.Cat?Visibility.Visible:Visibility.Collapsed;
        ApplyPose();
    }
    public void SetPaused(bool value)=>paused=value;
    public event Action<PetSound,double>? SoundRequested;
    public event Action<RewardButton>? Hit;
    public event Action<CareKind>? CareRequested;
    public event Action<BodyAction,double,double>? CreativeAction;
    public BodyAction CurrentAction => body.Action;
    public PetWindow()
    {
        InitializeComponent();
        Character.Children.Add(feline);
        SourceInitialized+=(_,_)=>{DesktopInteraction.MakeNonActivating(new System.Windows.Interop.WindowInteropHelper(this).Handle);ApplyPosition();};
        var group=new TransformGroup();group.Children.Add(bodyScale);group.Children.Add(bodyTilt);group.Children.Add(poseOffset);
        GirlVisual.RenderTransformOrigin=new Point(.5,.6);GirlVisual.RenderTransform=group;Face.RenderTransform=gazeOffset;CatFace.RenderTransform=gazeOffset;
        foreach(var (part,transform) in new (FrameworkElement,RotateTransform)[]{(GirlArmL,armL),(CatArmL,armL),(GirlArmR,armR),(CatArmR,armR),(GirlLegL,legL),(CatLegL,legL),(GirlLegR,legR),(CatLegR,legR)})
        {part.RenderTransformOrigin=new Point(.5,.1);part.RenderTransform=transform;}
        CatHead.RenderTransform=headTilt;headTilt.CenterX=38;headTilt.CenterY=58;
        CatEarL.RenderTransform=earLeft;earLeft.CenterX=20;earLeft.CenterY=28;
        CatEarR.RenderTransform=earRight;earRight.CenterX=56;earRight.CenterY=28;
        CatEyes.RenderTransform=eyeGaze;
        CatTail.RenderTransform=tailSwing;tailSwing.CenterX=61;tailSwing.CenterY=88;
        Square=new(false,Bounds());Ball=new(true,Bounds());
        Square.Played+=()=>{InteractionRequested?.Invoke(BodyAction.PseudoPushIcon);RoomChanged?.Invoke();};
        Ball.Played+=()=>{requestedToy=Ball;InteractionRequested?.Invoke(BodyAction.PlayToy);RoomChanged?.Invoke();};
        drag=new(this,Character,held=>{if(held){CancelRoute();sequence?.Interrupt(BehaviorInterruptReason.Safety);}else interactionUntil=clock.Elapsed.TotalSeconds+2;},
            (x,y)=>{body.Place(x,y,Bounds());ApplyPosition();},
            moved=>{if(!moved){interactionUntil=0;Hit?.Invoke(RewardButton.Left);}else {petGravity.VX=drag!.VelocityX*.4;petGravity.VY=drag.VelocityY;body.Action=BodyAction.Fall;ApplyPose();}});
        var menu=new ContextMenu();
        foreach(var (label,kind) in new[]{("餵飯飯",CareKind.Feed),("摸摸頭",CareKind.Pet),("一起玩球",CareKind.Play),("梳理毛毛",CareKind.Groom),("哄牠睡覺",CareKind.Rest)})
        {var item=new MenuItem{Header=label};item.Click+=(_,_)=>CareRequested?.Invoke(kind);menu.Items.Add(item);}
        Character.ContextMenu=menu;
        ResetPosition();
        timer.Tick += Tick;
        IsVisibleChanged += (_,_) => timer.Interval=TimeSpan.FromMilliseconds(IsVisible?33:500);
        Loaded += (_, _) => {timer.Start();};
        Closed += (_, _) => {timer.Stop();Art.Close();Square.Close();Ball.Close();foreach(var f in Furniture)f.Close();foreach(var t in ExtraToys)t.Close();};
    }
    private static BodyBounds Bounds()
    {
        return DisplayWorkspace.Bounds;
    }
    public void ResetPosition() { homeTarget=null;feline.Clip=null;sequence=null;queuedAction=null;requestedCare=null;activeCare=null;attentionPoint=null;CancelRoute();body.Reset(Bounds());petGravity.VX=petGravity.VY=0; ApplyPosition(); }
    public void SetAction(BodyAction action)
    {
        if(Interacting)return;
        if(FinishingMotion){queuedAction=action;return;}
        CancelRoute();
        if(appearance==PetAppearance.Cat)
        {
            if(action is BodyAction.Greet or BodyAction.Eat)SoundRequested?.Invoke(PetSound.Meow,1);
        }
        body.Action = action;HasCreativeOutput=false;shortcutPushed=false;
        Parameters=ParameterFactory?.Invoke(action)??Parameters;Parameters.Validate();
        movementRandom=new(Parameters.Seed);actionStarted=clock.Elapsed.TotalSeconds;poseAction=null;
        teaserTarget=null;teaserPawTarget=null;
        if(action==BodyAction.PlayToy)
        {
            playTarget=AllToys.Where(t=>t!=Square).OrderBy(t=>Math.Abs(t.Model.X-body.X)).FirstOrDefault();
            if(++playSession%2==1)teaserTarget=Furniture.Where(f=>f.Teaser is not null).OrderBy(f=>Math.Abs(f.Left-body.X)).FirstOrDefault();
            lastTeaserTap=clock.Elapsed.TotalSeconds;
        }
        ChooseTarget();
        BeginSequence(action);
        selectedShortcut=Shortcuts.OrderBy(i=>Math.Abs(body.X-i.X)+Math.Abs(body.Y-i.Y)).FirstOrDefault()?.Id;
        ApplyPose();
        if(action is BodyAction.DrawDoodle or BodyAction.WriteNote)CreativeAction?.Invoke(action,body.X-Bounds().Left+80,body.Y-Bounds().Top-60);
    }
    public Affordances SenseAffordances()
    {
        var physical=DesktopInteraction.Cursor();
        if(physical is { } p){var local=DisplayWorkspace.FromPixels(new Point(p.X,p.Y));cursor=(local.X,local.Y);}else cursor=null;
        return new(cursor is { } c && Math.Abs(c.X-body.X)<500&&Math.Abs(c.Y-body.Y)<500,true,true,true,Shortcuts.Count>0);
    }
    public void Play(BodyAction action) => SetAction(action);
    private void Tick(object? sender, EventArgs e)
    {
        var now = clock.Elapsed.TotalSeconds;
        var dt = Math.Min(now - previous, 0.1);
        previous = now;
        if(!IsVisible)return;
        RecordStressFrame();
        var interval=sequence?.Phase==BehaviorPhase.Sleep&&AllToys.All(t=>t.Model.IsResting)&&Furniture.All(f=>f.Teaser is null||f.Teaser.IsResting)?100:33;
        if(timer.Interval.TotalMilliseconds!=interval)timer.Interval=TimeSpan.FromMilliseconds(interval);
        if(queuedAction is {} queued&&!FinishingMotion&&!SequenceCommitted&&!Interacting){queuedAction=null;SetAction(queued);}
        foreach(var furniture in Furniture)furniture.StepTeaser(dt);
        var platforms=RoomPlatforms();var toys=AllToys.ToArray();
        foreach(var toy in toys){toy.Platforms=platforms;toy.Step(dt);}
        for(var i=0;i<toys.Length;i++)for(var j=i+1;j<toys.Length;j++)InteractiveToy.Collide(toys[i].Model,toys[j].Model);
        if(EditingRoom)
        {
            StepPetGravity(dt);sequence?.Step(dt,new(Grounded:petGravity.Grounded));
            if(sequence?.Finished==true)EndSequence();poseAction=BodyAction.ObserveCursor;ApplyPosition();ApplyPose();return;
        }
        if(paused){StepPetGravity(dt);ApplyPosition();ApplyPose();return;}
        if(Interacting)
        {
            StepPetGravity(dt);ApplyPosition();
            ActionLabel.Text=drag.Active&&drag.Dragged?"被提起了！":"陪你一下";
            if(drag.Active&&drag.Dragged){bodyTilt.Angle=Math.Sin(now*8)*8;poseOffset.Y=-5;}
            if(drag.Active&&drag.Dragged)poseAction=BodyAction.Fall;
            ApplyPose();
            return;
        }
        SenseAffordances();
        if(AllowCursorAttraction && body.Action is not (BodyAction.Sleep or BodyAction.RestInCorner) && cursor is {} pointer && previousCursor is {} old)
        {
            var distance=Math.Sqrt(Math.Pow(pointer.X-body.X-58,2)+Math.Pow(pointer.Y-body.Y-72,2));
            var moved=Math.Abs(pointer.X-old.X)+Math.Abs(pointer.Y-old.Y)>3;
            if(moved&&distance<360&&now-lastAttention>8+12*(1-Parameters.Social.Affinity)&&!Parameters.Social.AvoidBriefly)
            {lastAttention=now;InteractionRequested?.Invoke(distance<100?BodyAction.ObserveCursor:BodyAction.ChaseCursor);}
        }
        previousCursor=cursor;
        TickToyAttention(dt);
        var beforeX=body.X;
        var elapsed=now-actionStarted;var movement=Parameters.Movement;poseAction=null;teaserPawTarget=null;sequencePoseAge=null;navigationStepped=false;
        var bounds=Bounds();var items=new[]{new DesktopObject("square","Icon",Square.Model.X,Square.Model.Y),new DesktopObject("ball","Toy",Ball.Model.X,Ball.Model.Y)};
        var recovering=StepNavigationRecovery(dt);
        var sequenced=recovering||TickSequence(dt);
        if(!sequenced)switch(body.Action)
        {
            case BodyAction.Walk:case BodyAction.Wander:case BodyAction.Explore:
                if(BehaviorMotion.Pausing(movement,elapsed)){poseAction=BodyAction.Idle;break;}
                if(now-lastTarget>2&&(Math.Abs(body.X-target.X)+Math.Abs(body.Y-target.Y)<15 || movementRandom.NextDouble()<movement.DirectionChange*dt*.3))ChooseTarget();
                MovePetToward(target.X+Math.Sin(elapsed)*movement.PathCurvature*30,target.Y+Math.Cos(elapsed)*movement.PathCurvature*20,dt,bounds,movement.Speed*(body.Action==BodyAction.Wander?.7:1));break;
            case BodyAction.ObserveCursor:case BodyAction.ChaseCursor:
                if(cursor is {} socialCursor)
                {
                    var destination=BehaviorMotion.Social(Parameters,elapsed,socialCursor.X,socialCursor.Y,body.X,body.Y);
                    if(!destination.Waiting)MovePetToward(destination.X,destination.Y,dt,bounds,movement.Speed);
                    else poseAction=destination.Sit?BodyAction.Sit:BodyAction.ObserveCursor;
                }
                break;
            case BodyAction.AvoidCursor when cursor is { } c:MovePetToward(body.X+(body.X-c.X)*2,body.Y+(body.Y-c.Y)*2,dt,bounds,movement.Speed);break;
            case BodyAction.ObserveDesktopIcon:case BodyAction.PseudoPushIcon:
                var squareApproach=ToyApproach(Square);
                MovePetToward(squareApproach.X,squareApproach.Y,dt,bounds,movement.Speed);break;
            case BodyAction.PlayToy:
                if(PlayTeaser(dt,now,movement.Speed))break;
                var ballApproach=ToyApproach(playTarget??Ball);
                MovePetToward(ballApproach.X,ballApproach.Y,dt,bounds,movement.Speed);break;
            case BodyAction.ObserveShortcut:case BodyAction.PushShortcut:
                var shortcut=Shortcuts.FirstOrDefault(i=>i.Id==selectedShortcut);
                if(shortcut is not null)
                {
                    var approach=ObjectContact.Approach(shortcut.X,shortcut.Y,48,bounds);
                    MovePetToward(approach.X,approach.Y,dt,bounds,movement.Speed);
                    if(body.Action==BodyAction.PushShortcut&&!shortcutPushed&&ObjectContact.Reached(body.X,body.Y,approach)&&now-lastShortcutPush>8)
                    {shortcutPushed=true;lastShortcutPush=now;ShortcutPushRequested?.Invoke(shortcut.Id,shortcut.X+approach.PushX*96,shortcut.Y+approach.PushY*96);}
                }
                break;
            case BodyAction.RestInCorner:case BodyAction.Hide:MovePetToward(bounds.Left+8,bounds.Top+bounds.Height-DesktopBody.Height,dt,bounds,movement.Speed);break;
        }
        var activeToy=!sequenced?(body.Action==BodyAction.PseudoPushIcon?Square:body.Action==BodyAction.PlayToy&&teaserTarget is null?playTarget??Ball:null):null;
        if(activeToy is not null && !activeToy.Model.Held && now-lastToyKick>.6)
        {
            var contact=ToyApproach(activeToy);
            if(TouchingToy(activeToy))
            {var kick=ToyContactPhysics.Push(activeToy.Model.X,bounds,contact.PushX,Parameters.Play.Force);activeToy.Model.Kick(kick.X,kick.Y);lastToyKick=now;facing=contact.PushX;poseAction=BodyAction.BatToy;}
        }
        if(activeToy is not null && !TouchingToy(activeToy))poseAction=BodyAction.Walk;
        else if(activeToy is not null)poseAction=BodyAction.BatToy;
        // Finish in-flight navigation even when a wander pause or social wait stops requesting movement.
        if(!recovering&&!navigationStepped&&FinishingMotion&&routeGoal is {} goal)Navigate(goal.X,goal.Y,dt,bounds,movement.Speed);
        var attempting=MovementPhase is NavigationPhase.Approaching or NavigationPhase.Unreachable;
        if(navigationProgress.Stalled(body.X,body.Y,dt,attempting))RecoverNavigation();
        StepPetGravity(dt);
        Art.Update(bounds,items,body.Action,now,body.X,body.Y);
        if(Math.Abs(body.X-beforeX)>.05)facing=body.X>beforeX?1:-1;
        ApplyPosition();
        ApplyPose();
    }
    private void ChooseTarget()
    {
        var bounds=Bounds();var m=Parameters.Movement;
        var angle=movementRandom.NextDouble()*Math.PI*2;
        var tx=body.X+Math.Cos(angle)*m.Distance;var ty=body.Y;
        if(movementRandom.NextDouble()<m.ObjectInterest*.4){tx=Ball.Model.X-70;ty=Ball.Model.Y-100;}
        var edgeX=body.X<bounds.Left+bounds.Width/2?bounds.Left:bounds.Left+bounds.Width-DesktopBody.Width;
        var edgeY=body.Y<bounds.Top+bounds.Height/2?bounds.Top:bounds.Top+bounds.Height-DesktopBody.Height;
        target=(Math.Clamp(tx*(1-m.PreferredRegion)+edgeX*m.PreferredRegion,bounds.Left,bounds.Left+Math.Max(0,bounds.Width-DesktopBody.Width)),
            Math.Clamp(ty*(1-m.PreferredRegion)+edgeY*m.PreferredRegion,bounds.Top,bounds.Top+Math.Max(0,bounds.Height-DesktopBody.Height)));
        if(Furniture.Count>0&&movementRandom.NextDouble()<.45)
        {
            var reachable=RoomPlatforms().Where(p=>RoomNavigation.Plan(RoomPlatforms(),bounds,body.X+58,body.Y+144,p.X+p.Width/2,p.HeightAt(p.X+p.Width/2)) is not null).ToArray();
            if(reachable.Length>0){var platform=reachable[movementRandom.Next(reachable.Length)];target=(platform.X+platform.Width/2-58,platform.HeightAt(platform.X+platform.Width/2)-144);}
        }
        lastTarget=clock.Elapsed.TotalSeconds;
    }
    private void ApplyPosition() { DisplayWorkspace.Position(this,body.X,body.Y); }
    private double facing=1;
    private void ApplyPose(double? sampleTime=null)
    {
        ActionLabel.Text = UiText.Label(body.Action);
        if(clock.Elapsed.TotalSeconds>speechUntil)SpeechBubble.Visibility=Visibility.Collapsed;
        var t=sampleTime??clock.Elapsed.TotalSeconds;
        var pose=PetPose.At(poseAction??body.Action,(sampleTime??clock.Elapsed.TotalSeconds)*Math.Clamp(Parameters.Movement.Speed/80,.5,1.8));
        var blink=t%4.7<.16;
        Face.Text=pose.Sleeping||blink?"－ ᴗ －":body.Action==BodyAction.Nuzzle?"˘ ᴗ ˘":EmotionalState.Hunger>70?"• ﹏ •":"• ᴗ •";
        var happy=body.Action is BodyAction.Nuzzle or BodyAction.Groom;
        CatEyes.Visibility=blink||happy?Visibility.Collapsed:Visibility.Visible;
        CatClosedEyes.Visibility=blink||happy?Visibility.Visible:Visibility.Collapsed;
        eyeGaze.X=cursor is {} eye?Math.Clamp((eye.X-body.X-58)/100,-2,2):0;
        eyeGaze.Y=cursor is {} eyeY?Math.Clamp((eyeY.Y-body.Y-55)/150,-1,1):0;
        headTilt.Angle=body.Action==BodyAction.Eat?Math.Sin(t*7)*7+8:body.Action==BodyAction.Groom?Math.Sin(t*6)*8-12:body.Action==BodyAction.ObserveCursor?Math.Sin(t)*6:Math.Sin(t*1.3)*1.5;
        earLeft.Angle=Math.Sin(t*2)*3+(t%7<.5?Math.Sin(t*25)*12:0);
        earRight.Angle=-Math.Sin(t*2)*3;
        GroomComb.Visibility=ShowComb?Visibility.Visible:Visibility.Collapsed;
        PettingHand.Visibility=ShowHand?Visibility.Visible:Visibility.Collapsed;
        Canvas.SetLeft(PettingHand,40+Math.Sin(t*4)*8);Canvas.SetTop(PettingHand,20+Math.Cos(t*4)*2);
        Canvas.SetTop(GroomComb,67+Math.Sin(t*6)*6);
        ToyPaw.Visibility=poseAction==BodyAction.BatToy||body.Action==BodyAction.BatToy?Visibility.Visible:Visibility.Collapsed;
        ToyPaw.RenderTransform=new ScaleTransform(facing,1,58,0);
        FoodBowl.Visibility=body.Action==BodyAction.Eat?Visibility.Visible:Visibility.Collapsed;
        Affection.Visibility=body.Action is BodyAction.Nuzzle or BodyAction.Greet?Visibility.Visible:Visibility.Collapsed;
        Canvas.SetTop(Affection,25-Math.Sin(t*2)*5);Affection.Opacity=.6+.4*Math.Abs(Math.Sin(t*2));
        SleepingCat.Visibility=pose.Sleeping&&appearance==PetAppearance.Cat?Visibility.Visible:Visibility.Collapsed;
        CatVisual.Visibility=!pose.Sleeping&&appearance==PetAppearance.Cat?Visibility.Visible:Visibility.Collapsed;
        CatFace.Text=pose.Sleeping?"－ᴥ－":"• ᴥ •";
        gazeOffset.X=body.Action==BodyAction.ObserveCursor&&cursor is {} c?Math.Sign(c.X-body.X)*3:0;
        Character.Opacity=body.Action==BodyAction.Hide?.7:1;
        Character.IsHitTestVisible=true;
        ActionLabel.Opacity=Character.Opacity;
        var curled=pose.Sleeping&&appearance==PetAppearance.Cat;
        var blend=sampleTime is null?.24:1;
        poseOffset.Y+=( (curled?Math.Sin(t*2)*.7:pose.Bob)-poseOffset.Y)*blend;
        bodyTilt.Angle+=((curled?0:pose.Tilt)-bodyTilt.Angle)*blend;
        bodyScale.ScaleY+=((curled?1+Math.Sin(t*2)*.015:pose.ScaleY)-bodyScale.ScaleY)*blend;
        bodyScale.ScaleX=(pose.Sleeping&&!curled?.8:1)*(appearance==PetAppearance.Cat?facing:1);
        armL.Angle=pose.Arm;armR.Angle=-pose.Arm;legL.Angle=pose.Leg;legR.Angle=-pose.Leg;tailSwing.Angle=pose.Tail;
        SleepMark.Visibility=pose.Sleeping?Visibility.Visible:Visibility.Collapsed;
        SleepMark.Opacity=.5+.5*Math.Abs(Math.Sin(clock.Elapsed.TotalSeconds));
        Pencil.Visibility=body.Action is BodyAction.DrawDoodle or BodyAction.WriteNote?Visibility.Visible:Visibility.Collapsed;
        feline.Visibility=appearance==PetAppearance.Cat?Visibility.Visible:Visibility.Collapsed;
        if(appearance==PetAppearance.Cat)
        {
            CatVisual.Visibility=SleepingCat.Visibility=Visibility.Collapsed;
            bodyTilt.Angle=0;bodyScale.ScaleX=bodyScale.ScaleY=1;poseOffset.Y=0;
            feline.Personality=BehaviorPersonality;
            feline.Pose(poseAction??body.Action,t,facing,attentionPoint is {} focus?Math.Clamp((focus.X-body.X-58)/100,-1.5,1.5):cursor is {} look?Math.Clamp((look.X-body.X-58)/150,-1.5,1.5):0,teaserPawTarget,sampleTime??sequencePoseAge??clock.Elapsed.TotalSeconds-actionStarted,sequence?.Phase is BehaviorPhase.Prepare or BehaviorPhase.Crouch?NavigationPhase.Crouching:MovementPhase,sequence?.Phase);
            UpdateHomeExpression();
            ToyPaw.Visibility=Visibility.Collapsed;
            Canvas.SetLeft(PettingHand,40);Canvas.SetTop(PettingHand,48+Math.Sin(t*4)*2);
        }
    }

    private void OnPetMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Routed only from painted character geometry, never a global mouse hook.
        var button = e.ChangedButton switch {  _ => (RewardButton?)null };
        if (button is { } rewardButton) Hit?.Invoke(rewardButton);
        e.Handled = button is not null;
    }
    public void SmokeClickAt(Point point, MouseButton button)
    {
        // Synthetic routed events must not inherit the user's unrelated physical cursor location.
        drag.WithDiagnosticPointer(new Point(Left+point.X,Top+point.Y),()=>
        {
            if (InputHitTest(point) is UIElement element)
                element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,button) { RoutedEvent=Mouse.MouseDownEvent });
            if(InputHitTest(point) is UIElement released)released.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,button){RoutedEvent=Mouse.MouseUpEvent});
        });
    }
    // Render only our generated vector artwork for diagnostics, never capture the desktop.
    public void RenderDiagnosticArt(string path)
    {
        SpeechBubble.Visibility=Visibility.Collapsed;
        var drawing=new DrawingVisual();
        using(var context=drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,1044,288));
            var actions=new[]{BodyAction.Idle,BodyAction.Walk,BodyAction.ChaseCursor,BodyAction.Sleep,BodyAction.Stretch,BodyAction.Eat,BodyAction.Groom,BodyAction.Nuzzle,BodyAction.Greet};
            foreach(var appearance in Enum.GetValues<PetAppearance>())
            {
                SetAppearance(appearance);
                for(var i=0;i<actions.Length;i++)
                {
                    body.Action=actions[i];ApplyPose(.35);UpdateLayout();
                    var frame=new System.Windows.Media.Imaging.RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);
                    frame.Render(Surface);context.DrawImage(frame,new Rect(i*116,(int)appearance*144,116,144));
                }
            }
        }
        var sheet=new System.Windows.Media.Imaging.RenderTargetBitmap(1044,288,96,96,PixelFormats.Pbgra32);sheet.Render(drawing);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(sheet));
        using var file=System.IO.File.Create(path);encoder.Save(file);
        SetAppearance(PetAppearance.Girl);SetAction(BodyAction.Idle);
    }
}
