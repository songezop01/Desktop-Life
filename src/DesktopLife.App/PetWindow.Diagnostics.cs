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
        }
        finally
        {
            teaserTarget=null;teaserPawTarget=null;poseAction=null;
            SetAppearance(oldAppearance);body.Action=BodyAction.Idle;ResetPosition();timer.Start();
        }
    }
    private void RenderMotionDiagnostics(string path)
    {
        var poses=new[]{(BodyAction.Idle,0d,"站立"),(BodyAction.Idle,10d,"背面"),(BodyAction.Sit,.4,"蹲坐"),(BodyAction.Sleep,5d,"側躺"),(BodyAction.Groom,.7,"舔爪"),(BodyAction.Groom,2.1,"洗臉"),(BodyAction.Stretch,1.7,"伸展前肢"),(BodyAction.Stretch,3.6,"伸展後肢")};
        var drawing=new DrawingVisual();
        using(var dc=drawing.RenderOpen())
        {
            dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,928,180));
            for(var i=0;i<poses.Length;i++)
            {
                var pose=poses[i];feline.Pose(pose.Item1,.7,1,0,null,pose.Item2);UpdateLayout();
                var frame=new RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);frame.Render(feline);
                dc.DrawImage(frame,new Rect(i*116,10,116,144));
                dc.DrawText(new FormattedText(pose.Item3,System.Globalization.CultureInfo.GetCultureInfo("zh-TW"),FlowDirection.LeftToRight,new Typeface("Microsoft JhengHei"),12,Brushes.DarkSlateGray,1),new Point(i*116+20,160));
                var scaled=new RenderTargetBitmap(174,216,144,144,PixelFormats.Pbgra32);scaled.Render(feline);
                var pixels=new byte[174*216*4];scaled.CopyPixels(pixels,174*4,0);
                if(!Enumerable.Range(214*174,348).Any(p=>pixels[p*4+3]>50))throw new Exception("Scaled cat lost its grounded contact pixels.");
            }
        }
        var sheet=new RenderTargetBitmap(928,180,96,96,PixelFormats.Pbgra32);sheet.Render(drawing);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(sheet));using var file=File.Create(path);encoder.Save(file);
    }
}
