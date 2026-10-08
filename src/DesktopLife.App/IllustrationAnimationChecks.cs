using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;

namespace DesktopLife.App;

public static class IllustrationAnimationChecks
{
    public static object[] VerifyTransitions(string root)
    {
        var evidence=new List<object>();var sheet=new DrawingVisual();
        using(var drawing=sheet.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.FloralWhite,null,new Rect(0,0,928,432));
            foreach(var kind in Enum.GetValues<PetAppearance>())
            {
                var sprite=new CompanionSpriteVisual();sprite.SetCharacter(kind);
                sprite.Measure(new Size(116,144));sprite.Arrange(new Rect(0,0,116,144));
                var stand=kind==PetAppearance.Girl?BodyAction.Idle:BodyAction.Fall;
                for(var warm=0;warm<5;warm++)sprite.Pose(stand,warm*.1,1);
                var forward=new List<int>();var reverse=new List<int>();
                for(var i=0;i<4;i++)
                {
                    sprite.Pose(BodyAction.Sit,1+i*.08,1);forward.Add(sprite.PresentedFrameIndex);
                    DrawFrame(sprite,drawing,i,(int)kind);
                }
                if(!forward.SequenceEqual(new[]{0,1,2,3}))throw new InvalidOperationException($"Sit runtime does not use all four frames: {kind}/{string.Join(',',forward)}");
                var seated=sprite.DrawnBounds;sprite.Pose(BodyAction.Sit,1.4,1);
                if(sprite.PresentedFrameIndex!=3||sprite.DrawnBounds!=seated)throw new InvalidOperationException("Sitting endpoint changes body size: "+kind);
                for(var i=0;i<4;i++)
                {
                    sprite.Pose(stand,2+i*.08,1);reverse.Add(sprite.PresentedFrameIndex);
                    if(!sprite.IsStandingUp)throw new InvalidOperationException("Stand transition not marked active: "+kind);
                    DrawFrame(sprite,drawing,4+i,(int)kind);
                }
                if(!reverse.SequenceEqual(new[]{3,2,1,0}))throw new InvalidOperationException($"Stand runtime does not reverse all four frames: {kind}/{string.Join(',',reverse)}");
                var standing=sprite.DrawnBounds;sprite.Pose(stand,2.4,1);
                if(sprite.PresentedFrameIndex!=0||sprite.DrawnBounds!=standing||sprite.IsStandingUp)
                    throw new InvalidOperationException("Standing endpoint changes body size or remains active: "+kind);
                for(var warm=0;warm<5;warm++)sprite.Pose(BodyAction.Sit,3+warm*.1,1);
                sprite.Pose(BodyAction.Walk,4,1);
                if(!sprite.IsStandingUp)throw new InvalidOperationException("Walking skips standing up: "+kind);
                for(var warm=1;warm<5;warm++)sprite.Pose(BodyAction.Walk,4+warm*.1,1);
                if(sprite.IsStandingUp||sprite.FrameIndex!=0)throw new InvalidOperationException("Walking did not resume after standing: "+kind);
                evidence.Add(new{Character=kind.ToString(),SitIndices=forward,StandIndices=reverse,Grounded=true,TransparentHitTesting=true,EndpointBoundsStable=true,WalkStartsWithStand=true,ClipDurationSeconds=.32,sprite.AssetDiagnostic});
            }
        }
        var bitmap=new RenderTargetBitmap(1856,864,192,192,PixelFormats.Pbgra32);bitmap.Render(sheet);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(root,"sit-stand-cycles.png")))encoder.Save(stream);
        File.WriteAllText(Path.Combine(root,"sit-stand-checks.json"),JsonSerializer.Serialize(new{Status="PASS",Cases=evidence},new JsonSerializerOptions{WriteIndented=true}));
        return evidence.ToArray();
    }
    private static void DrawFrame(CompanionSpriteVisual sprite,DrawingContext drawing,int column,int row)
    {
        sprite.UpdateLayout();var frame=new RenderTargetBitmap(232,288,192,192,PixelFormats.Pbgra32);frame.Render(sprite);
        var pixels=new byte[232*288*4];frame.CopyPixels(pixels,232*4,0);
        if(!Enumerable.Range(232*286,232*2).Any(p=>pixels[p*4+3]>24))throw new InvalidOperationException("Sit/stand frame floats above support.");
        if(pixels[3]!=0||pixels[((288-1)*232)*4+3]!=0||sprite.InputHitTest(new Point(0,140)) is not null)
            throw new InvalidOperationException("Sit/stand frame has an opaque or clickable corner.");
        drawing.DrawImage(frame,new Rect(column*116,row*144,116,144));
    }
}
