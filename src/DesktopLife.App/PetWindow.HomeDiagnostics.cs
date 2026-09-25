using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    public void SmokeHome(string directory)
    {
        timer.Stop();var saved=Furniture.ToArray();Furniture.Clear();var b=Bounds();
        var box=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.Box,b.Left+260,b.Top+b.Height-100));
        Furniture.Add(box);box.Show();
        try
        {
            ResetPosition();body.Place(b.Left+260,b.Top+b.Height-144,b);StepPetGravity(.033);
            SetAction(BodyAction.Hide);var seen=new HashSet<BehaviorPhase>();
            for(var i=0;i<2200&&sequence is not null;i++)
            {
                var phase=sequence.Phase;
                TickSequence(.016);StepPetGravity(.016);ApplyPose();UpdateLayout();
                if(phase!=sequence?.Phase&&sequence is {} s&&seen.Add(s.Phase)&&s.Phase is BehaviorPhase.Inspect or BehaviorPhase.Hide or BehaviorPhase.Peek or BehaviorPhase.Rest)
                {
                    foreach(var scale in new[]{1d,1.5})
                    {
                        var frame=new RenderTargetBitmap((int)(116*scale),(int)(144*scale),96*scale,96*scale,PixelFormats.Pbgra32);frame.Render(feline);
                        if(s.Phase!=BehaviorPhase.Inspect)
                        {
                            if(feline.Clip is null)throw new Exception("Box occlusion missing.");
                            if(feline.InputHitTest(new Point(65,130)) is not null)throw new Exception("Hidden box body intercepts input.");
                            var pixels=new byte[frame.PixelWidth*frame.PixelHeight*4];frame.CopyPixels(pixels,frame.PixelWidth*4,0);
                            var bottom=(int)(130*scale)*frame.PixelWidth;
                            if(Enumerable.Range(bottom,frame.PixelWidth).Any(p=>pixels[p*4+3]>0))throw new Exception("Cat visible through box front.");
                        }
                    }
                    var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
                    {
                        dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,220,210));
                        dc.DrawRectangle(new VisualBrush((Visual)box.Content),null,new Rect(30,100,160,100));
                        var cat=new RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);cat.Render(feline);
                        dc.DrawImage(cat,new Rect(30+body.X-box.Left,100+body.Y-box.Top,116,144));
                    }
                    var sheet=new RenderTargetBitmap(220,210,96,96,PixelFormats.Pbgra32);sheet.Render(visual);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(sheet));using var file=File.Create(Path.Combine(directory,"box-"+s.Phase+".png"));encoder.Save(file);
                }
            }
            if(sequence is not null||!seen.Contains(BehaviorPhase.Peek))throw new Exception($"Box sequence did not finish: {sequence?.Phase}, {body.X},{body.Y}, nav={MovementPhase}, seen={string.Join(',',seen)}");
        }
        finally{box.Close();Furniture.Clear();Furniture.AddRange(saved);ResetPosition();timer.Start();}
        SmokeScratch();
        SmokeBed();
        SmokeHomeChanges();
        RenderHomeFurniture(directory);
        SmokeBoxSleep();
    }
    private void SmokeScratch()
    {
        timer.Stop();var saved=Furniture.ToArray();Furniture.Clear();var b=Bounds();
        var scratch=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.Scratcher,b.Left+260,b.Top+b.Height-35));Furniture.Add(scratch);scratch.Show();
        try
        {
            ResetPosition();body.Place(b.Left+220,b.Top+b.Height-144,b);StepPetGravity(.1);SetAction(BodyAction.Stretch);var contact=false;
            for(var i=0;i<2000&&sequence is not null;i++)
            {
                TickSequence(.016);StepPetGravity(.016);ApplyPose();UpdateLayout();
                if(sequence?.Phase==BehaviorPhase.Scratch)
                {
                    var frame=new RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);frame.Render(feline);
                    if(feline.RenderedPawTip is not {} tip||Math.Abs(body.Y+tip.Y-scratch.Platforms[0].Y)>2)throw new Exception("Scratch paw missed board.");
                    contact=true;
                }
            }
            if(!contact||sequence is not null)throw new Exception($"Scratch sequence failed: phase={sequence?.Phase}, nav={MovementPhase}, position={body.X},{body.Y}, contact={contact}");
        }
        finally{scratch.Close();Furniture.Clear();Furniture.AddRange(saved);ResetPosition();timer.Start();}
    }
    private void SmokeBed()
    {
        timer.Stop();var saved=Furniture.ToArray();Furniture.Clear();var b=Bounds();var old=EmotionalState;
        var bed=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.PetBed,b.Left+260,b.Top+b.Height-55));Furniture.Add(bed);bed.Show();
        try
        {
            ResetPosition();body.Place(b.Left+260,b.Top+b.Height-144,b);StepPetGravity(.1);EmotionalState=new(){Energy=80,Fatigue=10};SetAction(BodyAction.Sleep);StartSleepAt(new(bed.Item.Id+":0",FurnitureKind.PetBed,bed.Left+85,bed.Top+28));var phases=new HashSet<BehaviorPhase>();
            for(var i=0;i<7000&&sequence is not null;i++)
            {
                phases.Add(sequence.Phase);TickSequence(.016);StepPetGravity(.016);
                if(sequence?.Phase==BehaviorPhase.Sleep&&Math.Abs(body.Y+144-bed.Platforms[0].Y)>2)throw new Exception("Bed sleep lost support.");
            }
            if(sequence is not null||!phases.Contains(BehaviorPhase.Knead)||!phases.Contains(BehaviorPhase.Wake))throw new Exception("Bed sleep chain failed.");
        }
        finally{bed.Close();Furniture.Clear();Furniture.AddRange(saved);EmotionalState=old;ResetPosition();timer.Start();}
    }

    private void SmokeHomeChanges()
    {
        timer.Stop();var saved=Furniture.ToArray();Furniture.Clear();var b=Bounds();
        var box=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.Box,b.Left+260,b.Top+b.Height-100));Furniture.Add(box);box.Show();
        try
        {
            ResetPosition();body.Place(b.Left+270,b.Top+b.Height-150,b);StepPetGravity(.1);SetAction(BodyAction.Hide);
            box.Relocate(box.Left+100,box.Top);
            for(var i=0;i<600&&sequence is not null;i++){TickSequence(.016);StepPetGravity(.016);}
            if(sequence is not null)throw new Exception("Moved box left a stuck sequence.");
            ResetPosition();body.Place(box.Left, b.Top+b.Height-144,b);StepPetGravity(.1);SetAction(BodyAction.Hide);Furniture.Remove(box);
            for(var i=0;i<600&&sequence is not null;i++){TickSequence(.016);StepPetGravity(.016);}
            if(sequence is not null)throw new Exception("Deleted box left a stuck sequence.");
            Furniture.Add(box);ResetPosition();StepPetGravity(.1);SetRoomEditing(true);var plans=PathPlans;
            for(var i=0;i<120;i++){box.Relocate(box.Left+(i%2==0?1:-1),box.Top);Navigate(box.Left,box.Top,.016,b,80);StepPetGravity(.016);}
            if(PathPlans!=plans)throw new Exception("Editing caused repeated pathfinding.");SetRoomEditing(false);
            ResetPosition();body.Place(b.Left+100,b.Top+b.Height-144,b);StepPetGravity(.1);Navigate(b.Left+150,b.Top+b.Height-144,.016,b,80);plans=PathPlans;var rebuilds=PlatformRebuilds;
            for(var i=0;i<120;i++){Navigate(b.Left+150,b.Top+b.Height-144,.016,b,80);StepPetGravity(.016);}
            if(PathPlans!=plans||PlatformRebuilds!=rebuilds)throw new Exception($"Unchanged room recomputed routes or platforms: plans {plans}->{PathPlans}, geometry {rebuilds}->{PlatformRebuilds}");
        }
        finally{SetRoomEditing(false);box.Close();Furniture.Clear();Furniture.AddRange(saved);ResetPosition();timer.Start();}
    }

    private void RenderHomeFurniture(string directory)
    {
        foreach(var scale in new[]{1d,1.5})
        {
            var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())
            {
                dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,600,200));var x=15d;
                foreach(var kind in new[]{FurnitureKind.Box,FurnitureKind.Scratcher,FurnitureKind.PetBed})
                {
                    var furniture=new RoomWindow(new(Guid.NewGuid(),kind,Bounds().Left,Bounds().Top));
                    try
                    {
                        furniture.Show();furniture.UpdateLayout();
                        var frame=new RenderTargetBitmap((int)(furniture.Width*scale),(int)(furniture.Height*scale),96*scale,96*scale,PixelFormats.Pbgra32);frame.Render((Visual)furniture.Content);
                        var pixels=new byte[frame.PixelWidth*frame.PixelHeight*4];frame.CopyPixels(pixels,frame.PixelWidth*4,0);
                        if(!pixels.Where((_,i)=>i%4==3).Any(a=>a>0))throw new Exception("Furniture rendered empty.");
                        if(pixels[3]!=0)throw new Exception("Furniture transparent corner is opaque.");
                        dc.DrawImage(frame,new Rect(x,160-furniture.Height,furniture.Width,furniture.Height));
                        dc.DrawText(new FormattedText(UiText.Label(kind),System.Globalization.CultureInfo.GetCultureInfo("zh-TW"),FlowDirection.LeftToRight,new Typeface("Microsoft JhengHei"),14,Brushes.DarkSlateGray,1),new Point(x+55,175));x+=195;
                    }
                    finally{furniture.Close();}
                }
            }
            var sheet=new RenderTargetBitmap((int)(600*scale),(int)(200*scale),96*scale,96*scale,PixelFormats.Pbgra32);sheet.Render(drawing);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(sheet));using var file=File.Create(Path.Combine(directory,$"home-furniture-{scale*100:0}.png"));encoder.Save(file);
        }
    }
    private void SmokeBoxSleep()
    {
        timer.Stop();var saved=Furniture.ToArray();Furniture.Clear();var b=Bounds();var old=EmotionalState;
        var box=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.Box,b.Left+260,b.Top+b.Height-100));Furniture.Add(box);box.Show();
        try
        {
            ResetPosition();body.Place(box.Left+22,box.Top+94-144,b);StepPetGravity(.1);EmotionalState=new(){Energy=80,Fatigue=10};SetAction(BodyAction.Sleep);
            StartSleepAt(new(box.Item.Id+":0",FurnitureKind.Box,box.Left+80,box.Top+94));var slept=false;
            for(var i=0;i<6000&&sequence is not null;i++)
            {
                TickSequence(.016);StepPetGravity(.016);
                if(sequence?.Phase==BehaviorPhase.Sleep&&!slept)
                {ApplyPose(25);UpdateLayout();if(feline.Clip is null||Math.Abs(body.Y+144-box.Top-94)>2)throw new Exception("Box sleep lost occlusion or support.");slept=true;}
            }
            if(!slept||sequence is not null)throw new Exception("Box sleep failed.");
            // Overlapping unreachable furniture is rejected instead of snapping to its coordinates.
            box.Relocate(b.Left+260,b.Top+1);var duplicate=new RoomWindow(box.Item with{Id=Guid.NewGuid()});Furniture.Add(duplicate);duplicate.Show();
            try{ResetPosition();body.Place(b.Left+100,b.Top+b.Height-144,b);StepPetGravity(.1);if(TryBeginHome(SequenceKind.Box,FurnitureUse.Hide))throw new Exception("Unreachable overlapping boxes selected.");}
            finally{Furniture.Remove(duplicate);duplicate.Close();}
        }
        finally{box.Close();Furniture.Clear();Furniture.AddRange(saved);EmotionalState=old;ResetPosition();timer.Start();}
    }

}
