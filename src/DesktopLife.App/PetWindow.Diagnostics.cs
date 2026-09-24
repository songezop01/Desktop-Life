using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;
namespace DesktopLife.App;

public partial class PetWindow
{
    public void SmokeFurnitureInteractions(string path)
    {
        var tree=Furniture.First(f=>f.Teaser is not null);
        var bounds=Bounds();var oldAppearance=appearance;
        timer.Stop();
        try
        {
            SetAppearance(PetAppearance.Cat);
            body.Place(tree.Left+25,tree.Top-144-70,bounds);
            petGravity.VX=petGravity.VY=0;
            body.Action=BodyAction.Fall;poseAction=BodyAction.Fall;
            for(var i=0;i<300;i++)StepPetGravity(.016);
            if(!petGravity.Grounded||Math.Abs(body.Y+DesktopBody.Height-tree.Platforms[0].Y)>.01)
                throw new Exception("Cat did not land flush with cat-tree upper shelf.");
            if(poseAction==BodyAction.Fall||body.Action==BodyAction.Fall)
                throw new Exception("Landing retained the airborne pose.");
            var actions=new[]{BodyAction.Idle,BodyAction.Walk,BodyAction.Stretch,BodyAction.Sleep,BodyAction.Eat};
            var sheet=new DrawingVisual();
            using(var dc=sheet.RenderOpen())
            {
                dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,1000,400));
                for(var i=0;i<actions.Length;i++)
                {
                    body.Action=actions[i];poseAction=null;ApplyPose(.7);ApplyPosition();UpdateLayout();
                    var frame=new RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);frame.Render(feline);
                    var bytes=new byte[116*144*4];frame.CopyPixels(bytes,116*4,0);
                    if(!Enumerable.Range(142*116,2*116).Any(p=>bytes[p*4+3]>50))
                        throw new Exception($"Visible cat paws/body float above support in {actions[i]} pose.");
                    tree.UpdateLayout();
                    dc.DrawRectangle(new VisualBrush((Visual)tree.Content),null,new Rect(i*200+10,160,180,230));
                    dc.DrawImage(frame,new Rect(i*200+35,31,116,144)); // 31 + 144 = shelf Y 175.
                }
            }
            var image=new RenderTargetBitmap(1000,400,96,96,PixelFormats.Pbgra32);image.Render(sheet);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using(var file=File.Create(path))encoder.Save(file);

            // Exercise the real approach/jump/reach/contact code, not just the pendulum model.
            body.Place(tree.Left+25,bounds.Top+bounds.Height-144,bounds);
            petGravity.VX=petGravity.VY=0;StepPetGravity(.016);lastJump=-10;
            teaserTarget=tree;body.Action=BodyAction.PlayToy;lastTeaserTap=clock.Elapsed.TotalSeconds;
            var count=TeaserContactCount;
            for(var i=0;i<900;i++)
            {
                poseAction=null;teaserPawTarget=null;tree.StepTeaser(.016);
                PlayTeaser(.016,clock.Elapsed.TotalSeconds+i*.016,85);StepPetGravity(.016);
                if(TeaserContactCount>count)
                {
                    ApplyPose(.7);UpdateLayout();
                    var frame=new RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);frame.Render(feline);
                    if(teaserPawTarget is not {} target||feline.RenderedPawTip is not {} tip||(target-tip).Length>2)
                        throw new Exception("Teaser impulse had no matching visible paw contact.");
                    break;
                }
            }
            if(TeaserContactCount==count||Math.Abs(tree.Teaser!.AngularVelocity)<.2)
                throw new Exception("Cat did not reach and bat the hanging teaser from the lower shelf.");
            Square.SmokeRotatedBounds(Path.Combine(Path.GetDirectoryName(path)!,"square-rotation-check.png"));
            RenderMotionDiagnostics(Path.Combine(Path.GetDirectoryName(path)!,"cat-motion-check.png"));
            SmokePlaySequence();
            SmokeSleepSequence();
            SmokeCareSequences();
            SmokeDeskNavigation();
        }
        finally
        {
            teaserTarget=null;teaserPawTarget=null;poseAction=null;
            SetAppearance(oldAppearance);body.Action=BodyAction.Idle;ResetPosition();timer.Start();
        }
    }
    private void SmokePlaySequence()
    {
        var saved=Furniture.ToArray();Furniture.Clear();
        try
        {
            ResetPosition();var bounds=Bounds();body.Place(bounds.Left+200,bounds.Top+bounds.Height-144,bounds);StepPetGravity(.016);
            Ball.Model.Place(body.X+210,bounds.Top+bounds.Height-40,bounds);
            requestedToy=Ball;SetAction(BodyAction.PlayToy);
            var phases=new HashSet<BehaviorPhase>();var contacts=0;
            for(var i=0;i<1500&&sequence is not null;i++)
            {
                poseAction=null;teaserPawTarget=null;
                if(sequence is {} s)phases.Add(s.Phase);
                TickSequence(.016);StepPetGravity(.016);ApplyPose(i*.016);UpdateLayout();
                if(phaseContact)
                {
                    contacts++;
                    var frame=new RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);frame.Render(feline);
                    if(teaserPawTarget is not {} target||feline.RenderedPawTip is not {} tip||(target-tip).Length>2)throw new Exception("Play sequence impulse did not match the visible paw.");
                }
                Ball.Step(.016);
            }
            if(contacts==0||!phases.Contains(BehaviorPhase.Watch)||!phases.Contains(BehaviorPhase.Crouch)||!phases.Contains(BehaviorPhase.Recover)||sequence is not null)
                throw new Exception($"Play sequence failed: contacts={contacts}, phases={string.Join(',',phases)}, active={sequence?.Phase}, body={body.X},{body.Y}, ball={Ball.Model.X},{Ball.Model.Y}, navigation={MovementPhase}");
        }
        finally{Furniture.AddRange(saved);sequence=null;ResetPosition();}
    }
    private void RenderMotionDiagnostics(string path)
    {
        var poses=new[]{(BodyAction.Idle,0d,"站立"),(BodyAction.Idle,10d,"背面"),(BodyAction.Sit,.4,"蹲坐"),(BodyAction.Sleep,5d,"側躺"),(BodyAction.Groom,.7,"舔爪"),(BodyAction.Groom,2.1,"洗臉"),(BodyAction.Stretch,1.7,"伸展前肢"),(BodyAction.Stretch,3.6,"伸展後肢"),(BodyAction.Walk,.8,"行走"),(BodyAction.ObserveCursor,2d,"觀察"),(BodyAction.BatToy,1d,"撥球"),(BodyAction.Sleep,26d,"蜷睡"),(BodyAction.Nuzzle,2d,"蹭蹭"),(BodyAction.Greet,1d,"招呼"),(BodyAction.Sit,2d,"坐好"),(BodyAction.Eat,2d,"吃飯")};
        var drawing=new DrawingVisual();
        using(var dc=drawing.RenderOpen())
        {
            dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,928,360));
            for(var i=0;i<poses.Length;i++)
            {
                var pose=poses[i];feline.Pose(pose.Item1,.7,1,0,null,pose.Item2);UpdateLayout();
                var frame=new RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);frame.Render(feline);
                dc.DrawImage(frame,new Rect(i%8*116,10+i/8*180,116,144));
                dc.DrawText(new FormattedText(pose.Item3,System.Globalization.CultureInfo.GetCultureInfo("zh-TW"),FlowDirection.LeftToRight,new Typeface("Microsoft JhengHei"),12,Brushes.DarkSlateGray,1),new Point(i%8*116+20,160+i/8*180));
                var scaled=new RenderTargetBitmap(174,216,144,144,PixelFormats.Pbgra32);scaled.Render(feline);
                var pixels=new byte[174*216*4];scaled.CopyPixels(pixels,174*4,0);
                if(!Enumerable.Range(214*174,348).Any(p=>pixels[p*4+3]>50))throw new Exception("Scaled cat lost its grounded contact pixels.");
            }
        }
        var sheet=new RenderTargetBitmap(928,360,96,96,PixelFormats.Pbgra32);sheet.Render(drawing);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(sheet));using var file=File.Create(path);encoder.Save(file);
    }
    private void SmokeSleepSequence()
    {
        var furniture=Furniture.ToArray();Furniture.Clear();var oldState=EmotionalState;
        try
        {
            ResetPosition();StepPetGravity(.1);EmotionalState=new(){Energy=80,Fatigue=10};
            SetAction(BodyAction.Sleep);var phases=new HashSet<BehaviorPhase>();
            for(var i=0;i<6500&&sequence is not null;i++)
            {if(sequence is {} s)phases.Add(s.Phase);TickSequence(.016);StepPetGravity(.016);}
            foreach(var phase in new[]{BehaviorPhase.Search,BehaviorPhase.Inspect,BehaviorPhase.Sit,BehaviorPhase.LieDown,BehaviorPhase.Curl,BehaviorPhase.Sleep,BehaviorPhase.Wake,BehaviorPhase.Stretch})
                if(!phases.Contains(phase))throw new Exception("Sleep sequence skipped "+phase);
            if(sequence is not null)throw new Exception("Sleep sequence did not resume.");
        }
        finally{Furniture.AddRange(furniture);EmotionalState=oldState;ResetPosition();}
    }
    private void SmokeCareSequences()
    {
        foreach(var manual in new[]{false,true})
        {
            ResetPosition();StepPetGravity(.1);
            if(manual)BeginCare(CareKind.Groom,BodyAction.Groom);else SetAction(BodyAction.Groom);
            var sawComb=false;var phases=new HashSet<BehaviorPhase>();
            for(var i=0;i<1200&&sequence is not null;i++)
            {phases.Add(sequence!.Phase);TickSequence(.016);StepPetGravity(.016);sawComb|=ShowComb;if(!manual&&ShowComb)throw new Exception("Autonomous grooming showed a human comb.");}
            if(manual&&!sawComb||!manual&&!phases.Contains(BehaviorPhase.WashFace)||sequence is not null)throw new Exception("Groom choreography failed.");
        }
        ResetPosition();StepPetGravity(.1);BeginCare(CareKind.Pet,BodyAction.Nuzzle);
        if(ShowHand)throw new Exception("Petting hand appeared before noticing the player.");
        for(var i=0;i<100;i++)TickSequence(.016);
        if(!ShowHand)throw new Exception("Accepted petting did not show the hand.");
        ResetPosition();
    }
    private void SmokeDeskNavigation()
    {
        var existing=Furniture.ToArray();Furniture.Clear();var b=Bounds();
        var desk=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.Desk,b.Left+b.Width-230,b.Top+b.Height-160));Furniture.Add(desk);desk.Show();
        try
        {
            ResetPosition();body.Place(desk.Left+70,b.Top+b.Height-144,b);StepPetGravity(.016);body.Action=BodyAction.Walk;
            var phases=new HashSet<NavigationPhase>();
            void Travel(double x,double feet)
            {
                for(var i=0;i<1600;i++)
                {
                    var previousX=body.X;var previousY=body.Y;
                    Navigate(x,feet-144,.016,b,85);StepPetGravity(.016);phases.Add(MovementPhase);
                    if(Math.Abs(body.X-previousX)>12||Math.Abs(body.Y-previousY)>12)throw new Exception("Navigation teleported during desk traversal.");
                    if(Math.Abs(body.X-x)<3&&Math.Abs(body.Y+144-feet)<3&&!FinishingMotion)return;
                }
                throw new Exception($"Desk traversal stuck: {MovementPhase}, position {body.X},{body.Y}, target {x},{feet}");
            }
            Travel(desk.Left+70,desk.Platforms[0].Y);
            foreach(var phase in new[]{NavigationPhase.Orienting,NavigationPhase.Crouching,NavigationPhase.Airborne,NavigationPhase.Landing})
                if(!phases.Contains(phase))throw new Exception("Jump skipped "+phase);
            Travel(desk.Left-140,b.Top+b.Height);
            // Explicit failed-target recovery must visibly walk out from under a right-wall desk.
            CancelRoute();body.Place(desk.Left+70,b.Top+b.Height-144,b);StepPetGravity(.016);RecoverNavigation();
            for(var i=0;i<400&&MovementPhase==NavigationPhase.Recovering;i++){StepNavigationRecovery(.016);StepPetGravity(.016);}
            if(body.X+116>=desk.Left||MovementPhase!=NavigationPhase.Idle)throw new Exception("Desk recovery did not exit the covered area.");
            // Removing a platform in flight must recover by gravity, never snap to its old height.
            body.Place(desk.Left+70,b.Top+b.Height-144,b);StepPetGravity(.016);CancelRoute();
            for(var i=0;i<100&&MovementPhase!=NavigationPhase.Airborne;i++){Navigate(desk.Left+70,desk.Platforms[0].Y-144,.016,b,85);StepPetGravity(.016);}
            Furniture.Clear();Navigate(desk.Left+70,desk.Top+22-144,.016,b,85);
            if(MovementPhase!=NavigationPhase.Recovering)throw new Exception("Lost jump platform did not recover.");
            for(var i=0;i<450;i++){StepNavigationRecovery(.016);StepPetGravity(.016);}
            if(!petGravity.Grounded||Math.Abs(body.Y+144-b.Top-b.Height)>1)throw new Exception("Lost platform recovery did not safely land.");
        }
        finally{Furniture.Clear();desk.Close();Furniture.AddRange(existing);ResetPosition();}
    }
}
