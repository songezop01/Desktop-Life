using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    internal async Task SmokeSharedTeaserContact(string directory)
    {
        if (worldOwner is not null || Furniture.Count != 0) throw new InvalidOperationException("Teaser checks require a fresh isolated actor.");
        var watch = Stopwatch.StartNew(); var cases = new List<object>();var contactsEvidence=new List<object>();
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Case(string name) => cases.Add(new { Case = name, Passed = true });
        void Save(BitmapSource bitmap, string path) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); png.Save(stream); }
        SetSimulationEnabled(false); ConfigureHouse(1, false); Show();
        var bounds = Bounds(); var floor = House!.Floors[0]; var scale = House.SceneScale;
        AddRoomItem(new(Guid.NewGuid(), FurnitureKind.CatTree, bounds.Left + bounds.Width * .42, floor.Y - 230 * scale));
        var tree = Furniture.Single(); tree.SetEditing(false); tree.Show();
        try
        {
            foreach (var kind in new[] { PetAppearance.Cat, PetAppearance.BorderCollie })
            {
                SetAppearance(kind); PrepareCareMenuDiagnostic(0); SetPaused(false); SetRoomEditing(false);
                // The fixture reuses one HWND for two independently profiled
                // residents. Retire the previous resident's toy-interest cooldown.
                toyInterest.Step(30);
                Check(TryTeaserStance(tree, out var stance), $"{kind} has no physically reachable hanging toy stance.");
                body.Place(stance.X + 50 * scale, floor.Y - BodyHeight, bounds); StepPetGravity(.016);
                var before = TeaserContactCount;
                Check(TryStartTeaserActivity(tree), $"{kind} did not bind the clicked hanging toy.");
                Check(AttentionTarget == tree.Item.Id.ToString(), "Clicked teaser selected an unrelated floor ball.");
                Check(PhysiologicalAction != BodyAction.PlayToy, "Merely noticing the hanging toy granted play benefit.");
                Case($"{kind}-clicked-target-bound-without-early-benefit");
                for (var frame = 0; frame < 1800 && TeaserContactCount == before; frame++)
                {
                    if (watch.Elapsed.TotalSeconds > 6) throw new Exception("Shared teaser checks exceeded their six second native budget.");
                    houseControlledThisFrame = false; navigationStepped = false; poseAction = null; teaserPawTarget = null;
                    tree.StepTeaser(.016); TickSequence(.016); StepPetGravity(.016); ApplyPose(100 + frame * .016);
                    if (kind == PetAppearance.BorderCollie) Check(Math.Abs(body.Y + BodyHeight - floor.Y) < 1, "The dog climbed a cat-tree shelf.");
                    if (TeaserContactCount == before) continue;
                    // The simulation is frozen; give the real composition clock
                    // time to present its existing facing interpolation.
                    await Task.Delay(300);
                    UpdateLayout(); tree.UpdateLayout();
                    var actor = new RenderTargetBitmap(116, 144, 96, 96, PixelFormats.Pbgra32); actor.Render(Character);
                    Check(sprite.Visibility == Visibility.Visible && feline.Visibility != Visibility.Visible, "Teaser contact replaced the entire illustration with geometry.");
                    Check(sprite.FrameIndex==7&&sprite.PresentedFrameIndex==7,"The hanging toy received an impulse before its actual play pose was presented.");
                    Check(teaserPawTarget is { } goal && TeaserRenderedPawTip is { } tip && (tip - goal).Length < .1, "Pendulum momentum was not caused by the visible illustrated paw.");
                    var pawOnly = new RenderTargetBitmap(116, 144, 96, 96, PixelFormats.Pbgra32); pawOnly.Render(teaserPawVisual);
                    var pawPixels = new byte[116 * 144 * 4]; pawOnly.CopyPixels(pawPixels, 116 * 4, 0);
                    var endpoint = teaserPawTarget!.Value;
                    var px = Math.Clamp((int)Math.Round(endpoint.X), 0, 115); var py = Math.Clamp((int)Math.Round(endpoint.Y), 0, 143);
                    Check(pawPixels[(py * 116 + px) * 4 + 3] > 100, "The claimed illustrated paw contact tip was transparent.");
                    Check(TeaserBallIsVisible(tree),"Presented play/facing pose covered the hanging sphere after its impulse.");
                    var spriteOnly=new RenderTargetBitmap(116,144,96,96,PixelFormats.Pbgra32);spriteOnly.Render(sprite);
                    var spritePixels=new byte[116*144*4];spriteOnly.CopyPixels(spritePixels,116*4,0);
                    var bodyScale=BodyHeight/144;var displayedBall=tree.TeaserPosition;
                    var localBall=new Point((displayedBall.X-body.X)/bodyScale,(displayedBall.Y-body.Y)/bodyScale);
                    var ballRadius=HangingToyInteraction.ContactRadius*scale/bodyScale;var overlappingPixels=0;
                    for(var iy=0;iy<144;iy++)for(var ix=0;ix<116;ix++)
                        if(Math.Pow(ix+.5-localBall.X,2)+Math.Pow(iy+.5-localBall.Y,2)<=Math.Pow(ballRadius,2)&&spritePixels[(iy*116+ix)*4+3]>=24)
                            overlappingPixels++;
                    Check(overlappingPixels==0,"The actual contact raster hid part of the ball behind its body/head.");
                    var actorPixels=new byte[116*144*4];actor.CopyPixels(actorPixels,116*4,0);
                    var pixelOffset=(py*116+px)*4;
                    Check(actorPixels[pixelOffset+3]>100&&Enumerable.Range(0,3).All(c=>
                        Math.Abs(actorPixels[pixelOffset+c]-pawPixels[pixelOffset+c])<=3),
                        "A layer hid or changed the illustrated paw at the actual composited contact endpoint.");
                    Check(Math.Abs((endpoint-localBall).Length-ballRadius)<=.25*scale/bodyScale,
                        "The composited paw endpoint did not reach the displayed sphere circumference.");
                    contactsEvidence.Add(new{Resident=kind.ToString(),SphereFullyVisible=true,FutureFacingClearance=true,
                        CompositePawVisible=true,ContactFrame=sprite.PresentedFrameIndex,OverlappingSpritePixels=overlappingPixels,LocalBall=localBall,PawTip=endpoint,
                        BallRadius=ballRadius,ReachLength=(endpoint-new Point(endpoint.X<58?43:73,113)).Length});
                    Check(PhysiologicalAction == BodyAction.PlayToy && phaseContact && sequence?.Phase == BehaviorPhase.Contact, "Actual hanging toy contact did not permit play benefit.");
                    Check(Math.Abs(tree.Teaser!.AngularVelocity) > .1, "The real hanging ball did not receive an impulse.");
                    if (teaserPawVisual.PawAsset is { } source) Save(source, Path.Combine(directory, $"teaser-paw-source-{kind}.png"));
                    // VisualBrush's automatic descendant bounds crop transparent
                    // margins and can misplace the paw in a composite. Preserve
                    // the actual canonical viewport and the room's DIP viewport.
                    var roomRaster = new RenderTargetBitmap((int)Math.Ceiling(tree.Width), (int)Math.Ceiling(tree.Height),96,96,PixelFormats.Pbgra32);
                    roomRaster.Render((Visual)tree.Content);
                    var visual = new DrawingVisual();
                    using (var drawing = visual.RenderOpen())
                    {
                        drawing.DrawRectangle(Brushes.FloralWhite, null, new Rect(0, 0, 600, 520));
                        drawing.PushTransform(new ScaleTransform(2 / scale, 2 / scale));
                        var originX = tree.Item.X - 60 * scale; var originY = tree.Item.Y;
                        drawing.DrawImage(roomRaster, new Rect(tree.Item.X - originX, 0, tree.Width, tree.Height));
                        drawing.DrawImage(actor, new Rect(body.X - originX, body.Y - originY, BodyWidth, BodyHeight));
                        drawing.Pop();
                    }
                    var capture = new RenderTargetBitmap(600, 520, 96, 96, PixelFormats.Pbgra32); capture.Render(visual);
                    Save(capture, Path.Combine(directory, $"teaser-{kind}-contact.png"));
                }
                Check(TeaserContactCount == before + 1, $"{kind} could not walk to and physically touch the hanging toy.");
                Case($"{kind}-illustrated-physical-contact-and-real-support");
                var contacts = TeaserContactCount;
                var unreachable = new RoomPoint(tree.TeaserPosition.X, body.Y - 30);
                Check(HangingToyInteraction.ContactPoint(kind, unreachable, body.X, body.Y, BodyHeight, scale) is null &&
                    !ApplyTeaserContact(tree, null, 1) && TeaserContactCount == contacts, "An unreachable ball granted pretend contact.");
                Case($"{kind}-unreachable-contact-rejected");
                EndSequence(); teaserTarget = null;
                Check(tree.Teaser!.TargetLength == HangingToy.Length, "Cancellation did not retract the shared toy string.");
                for (var i = 0; i < 200; i++) tree.StepTeaser(.016);
                Check(tree.Teaser.RopeLength == HangingToy.Length, "The string did not visibly return after play.");
                Case($"{kind}-cancelled-string-restored");
                Check(TryStartTeaserActivity(tree) && AttentionTarget == tree.Item.Id.ToString(), "A repeated explicit toy click was replaced by an autonomous cooldown target.");
                Check(TeaserContactCount == contacts && !phaseContact, "Clicking during cooldown invented another contact.");
                EndSequence(); teaserTarget = null;
                Case($"{kind}-explicit-replay-binds-target-without-new-contact");
            }
            SetAppearance(PetAppearance.Girl); PrepareCareMenuDiagnostic(0);
            Check(!TryStartTeaserActivity(tree), "The girl accepted a pet-only hanging toy."); Case("girl-is-not-a-pet-toy-resident");
            SetAppearance(PetAppearance.BorderCollie); PrepareCareMenuDiagnostic(0); SetPaused(true);
            Check(!TryStartTeaserActivity(tree), "A paused actor accepted a hanging toy."); SetPaused(false); SetRoomEditing(true);
            Check(!TryStartTeaserActivity(tree), "Editing started a hanging toy route."); SetRoomEditing(false);
            Check(Occupancy.TryAcquire(tree.Item.Id.ToString(), PetAppearance.Cat), "Could not reserve fixture hanging toy.");
            Check(!TryStartTeaserActivity(tree), "An occupied hanging toy was stolen."); Occupancy.Release(PetAppearance.Cat);
            Case("paused-editing-and-other-resident-occupancy-rejected");
            Check(watch.Elapsed.TotalSeconds <= 6, "Shared teaser checks exceeded their six second native budget.");
            File.WriteAllText(Path.Combine(directory, "shared-teaser-report.json"), JsonSerializer.Serialize(new { Status = "PASS", Gate = "Component checks only", WallSeconds = watch.Elapsed.TotalSeconds, Contacts=contactsEvidence, Cases = cases }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { RestoreTeaserLength(); teaserTarget = null; sequence = null; }
    }
}
