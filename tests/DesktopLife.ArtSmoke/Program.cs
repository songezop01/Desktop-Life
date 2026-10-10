using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.App;
using DesktopLife.Core;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var root=Path.GetFullPath(args.FirstOrDefault()??"artifacts/verification/0.11/visuals");Directory.CreateDirectory(root);
        var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};var checks=new List<object>();
        try
        {
            var girl=CharacterGeometry.For(PetAppearance.Girl);var cat=CharacterGeometry.For(PetAppearance.Cat);var dog=CharacterGeometry.For(PetAppearance.BorderCollie);
            Require(Math.Abs(girl.Height/cat.Height-3.2)<.00001&&Math.Abs(dog.Height/cat.Height-1.3)<.00001,"World body proportions");
            var sheet=new DrawingVisual();
            using(var drawing=sheet.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.FloralWhite,null,new Rect(0,0,928,432));
                foreach(var kind in Enum.GetValues<PetAppearance>())
                {
                    var sprite=new CompanionSpriteVisual();sprite.SetCharacter(kind);Require(sprite.Ready,"Basic atlas loaded "+kind);
                    sprite.Measure(new Size(116,144));sprite.Arrange(new Rect(0,0,116,144));
                    for(var warm=0;warm<5;warm++)sprite.Pose(BodyAction.Walk,warm*.1,1);
                    var indices=new HashSet<int>();var hashes=new HashSet<string>();
                    for(var i=0;i<8;i++)
                    {
                        if(i>0)sprite.AdvanceWalk(sprite.WalkStrideDip/8);
                        sprite.Pose(BodyAction.Walk,.5+.1*i,1);sprite.UpdateLayout();indices.Add(sprite.FrameIndex);
                        var bitmap=Render(sprite,232,288);var pixels=Pixels(bitmap);hashes.Add(Convert.ToHexString(SHA256.HashData(pixels)));
                        Require(pixels[3]==0&&pixels[((288-1)*232)*4+3]==0,"Walk alpha corners "+kind+"/"+i);
                        Require(Enumerable.Range(232*286,232*2).Any(p=>pixels[p*4+3]>24),"Grounded foot "+kind+"/"+i);
                        Require(sprite.InputHitTest(new Point(0,140)) is null,"Transparent hit testing "+kind+"/"+i);
                        drawing.DrawImage(bitmap,new Rect(i*116,(int)kind*144,116,144));
                    }
                    Require(indices.Count==8&&hashes.Count==8,"Eight real rendered walk frames "+kind);
                    var stopped=sprite.FrameIndex;sprite.Pose(BodyAction.Walk,50,1);Require(stopped==sprite.FrameIndex,"No steps without movement "+kind);
                    checks.Add(new{Character=kind.ToString(),WalkFrameCount=indices.Count,DistinctRenderedFrames=hashes.Count,SupportContact=true,StationaryPhaseStable=true,sprite.AssetDiagnostic});
                }
            }
            Save(Render(sheet,1856,864),Path.Combine(root,"walk-cycles.png"));
            Require(FurnitureArt.LoadedCount==Enum.GetValues<FurnitureKind>().Length,"All furniture sprites loaded: "+FurnitureArt.Failure);
            var furnitureSheet=new DrawingVisual();
            using(var drawing=furnitureSheet.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.FloralWhite,null,new Rect(0,0,1200,960));
                var kinds=Enum.GetValues<FurnitureKind>();
                for(var i=0;i<kinds.Length;i++)
                {
                    var kind=kinds[i];var size=Size(kind);var scale=Math.Min(220/size.Width,210/size.Height);
                    drawing.PushTransform(new TranslateTransform(i%5*240+10,i/5*240+10));drawing.PushTransform(new ScaleTransform(scale,scale));
                    Require(FurnitureArt.Draw(drawing,kind,size.Width,size.Height),"Draw furniture "+kind);
                    foreach(var p in FurnitureArt.Platforms(kind,size.Width,size.Height))
                    {
                        Require(p.X>=0&&p.X+p.Width<=size.Width+.5&&p.Y>=0&&p.Y<=size.Height&&p.EndY>=0&&p.EndY<=size.Height,"Support within art "+kind);
                        drawing.DrawLine(new Pen(Brushes.MediumVioletRed,1/scale),new(p.X,p.Y),new(p.X+p.Width,p.EndY));
                    }
                    drawing.Pop();drawing.Pop();
                }
            }
            Save(Render(furnitureSheet,2400,1920),Path.Combine(root,"furniture-supports.png"));
            IllustrationAnimationChecks.VerifyTransitions(root);
            if(args.Contains("--candidates"))ValidateTransitionCandidates(root);
            File.WriteAllText(Path.Combine(root,"asset-checks.json"),JsonSerializer.Serialize(new{Status="PASS",Timestamp=DateTimeOffset.Now,Proportions=new{Girl=girl,Cat=cat,Dog=dog},WalkChecks=checks,Furniture=FurnitureArt.Diagnostic},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS: body ratios, three 8-frame walk cycles, grounded alpha, transparent hit areas, stationary phase and all furniture support maps.");application.Shutdown();return 0;
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(root,"asset-checks.json"),JsonSerializer.Serialize(new{Status="FAIL",Error=ex.ToString()},new JsonSerializerOptions{WriteIndented=true}));
            Console.Error.WriteLine(ex);application.Shutdown();return 1;
        }
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static RenderTargetBitmap Render(Visual visual,int width,int height){var image=new RenderTargetBitmap(width,height,192,192,PixelFormats.Pbgra32);image.Render(visual);return image;}
    private static byte[] Pixels(BitmapSource image){var result=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(result,image.PixelWidth*4,0);return result;}
    private static void Save(BitmapSource image,string path){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(path))encoder.Save(stream);}
    private static (double Width,double Height) Size(FurnitureKind kind)=>kind switch
    {FurnitureKind.HumanBed=>(280,110),FurnitureKind.Sofa=>(260,120),FurnitureKind.Chair=>(110,145),FurnitureKind.DiningTable=>(250,160),FurnitureKind.Computer=>(110,90),FurnitureKind.DrawingBook=>(100,28),FurnitureKind.LegoBox=>(130,65),FurnitureKind.CatBowl=>(80,28),FurnitureKind.CatTree=>(180,230),FurnitureKind.Slide=>(260,190),FurnitureKind.Desk=>(230,160),FurnitureKind.Bookshelf=>(180,220),FurnitureKind.Box=>(160,100),FurnitureKind.Scratcher=>(160,35),FurnitureKind.PetBed=>(170,55),FurnitureKind.Comb=>(120,40),_=>(140,45)};
}
