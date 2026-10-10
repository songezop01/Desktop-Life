using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    private sealed class Motion012Case
    {
        public string Character { get; set; } = "";
        public string Case => $"{Character}-{Floors}F-{Orientation}-{RequestedScale.ToString("0.00", CultureInfo.InvariantCulture)}x";
        public int Floors { get; set; }
        public string Orientation { get; set; } = "";
        public double RequestedScale { get; set; }
        public uint ObservedNativeDpi { get; set; }
        public bool NativeActualScaleMatched => Math.Abs(ObservedNativeDpi / 96d - RequestedScale) < .01;
        public double SceneScale { get; set; }
        public string PositionEvidence => "Offscreen production presentation with pixel rounding; no HWND route moves";
        public string RasterContactCoverage => Character == nameof(PetAppearance.Girl) ? "Visible stance sole" : "Visible supporting forepaw; simultaneous hindpaw contact not evaluated";
        public long NativePositionWrites { get; set; }
        public int RouteFrames { get; set; }
        public int RasterContactSamples { get; set; }
        public int MissingCalibrationSamples { get; set; }
        public List<object> MissingCalibrationEvidence { get; } = [];
        public double MaxRasterContactErrorDip { get; set; }
        public double MaxPresentedRootJumpDip { get; set; }
        public double MaxUnexpectedRootJumpDip { get; set; }
        public double MaxFrameBoundaryJumpDip { get; set; }
        public object? PeakJumpContext { get; set; }
        public bool FloorStoppedWithoutStride { get; set; }
        public List<string> Failures { get; } = [];
        public List<object> Contacts { get; } = [];
        public bool Completed { get; set; }
        public bool Passed => Completed && Failures.Count == 0;
    }
    private sealed record Motion012Image(BitmapSource Bitmap, string Label, Point? ActualFoot, Point? ExpectedFoot);
    private sealed record Motion012Raster(BitmapSource Image, Point? ActualFootPixels, Point ExpectedFootPixels, double ErrorDip, bool ActualFootPainted);
    private readonly record struct Motion012Sample(Point Origin, RoomPoint Model, int Frame, StairStepPose? Stair, string? Connector, NavigationPhase Phase);

    /// <summary>Ten-second component check; synthetic raster DPI is separate from real HWND/Rendering evidence.</summary>
    internal static async Task SmokeHouseMotion012(string root)
    {
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DesktopLifeSmoke"));
        var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetDirectoryName(full), allowed, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(full), "N", out _))
            throw new InvalidOperationException("Motion checks require a fresh synthetic smoke profile.");
        Directory.CreateDirectory(full);
        var started = DateTimeOffset.UtcNow; var budget = Stopwatch.StartNew();
        var originalDisplay = DisplayWorkspace.Active; var baselineWindows = Application.Current.Windows.Count;
        var actors = new List<PetWindow>(); var cases = new List<Motion012Case>(); var failures = new List<string>();
        var sheets = new Dictionary<string, List<Motion012Image>>(); var live = new List<object>();
        var needs = new Dictionary<PetWindow, PetState>(); var incomplete = false; var liveCompleted = false; var closedCleanly = false;
        var stage = "starting";
        var callbackFailure = false;
        string? checkpointException = null;
        // Native/WPF callbacks are outside the awaited task's catch. Observe them
        // without marking Handled: a production callback failure remains fatal.
        DispatcherUnhandledExceptionEventHandler callbackObserver = (_, args) =>
        {
            callbackFailure = true;
            WriteCheckpoint(stage, args.Exception);
        };
        Application.Current.DispatcherUnhandledException += callbackObserver;
        WriteCheckpoint(stage);
        try
        {
            Checkpoint("constructing shared owner");
            var owner = new PetWindow(); actors.Add(owner);
            foreach (var appearance in new[] { PetAppearance.Girl, PetAppearance.Cat, PetAppearance.BorderCollie })
            {
                Checkpoint("constructing " + appearance);
                var actor = appearance == PetAppearance.Girl ? owner : new PetWindow(owner);
                if (actor != owner) actors.Add(actor);
                actor.diagnosticRasterPresentation = true;
                actor.SetAppearance(appearance); actor.SetSimulationEnabled(false); actor.AllowCursorAttraction = false;
                Checkpoint("showing " + appearance);
                actor.ShowActivated = false; actor.Show(); needs[actor] = actor.EmotionalState;
            }
            Checkpoint("loaded callbacks");
            await Dispatcher.Yield(DispatcherPriority.Loaded);
            foreach (var actor in actors) { actor.timer.Stop(); actor.simulationEnabled = true; }
            // Compress production navigation/pose/presentation offscreen. Moving an
            // HWND tens of thousands of times synchronously exhausts WPF's native
            // message quota, and proves neither render cadence nor phone FPS.
            foreach (var size in new[] { new BodyBounds(0, 0, 1600, 900), new BodyBounds(0, 0, 720, 1280) })
                foreach (var scale in new[] { 1d, 1.5, 2d })
                    for (var floors = 1; floors <= 3; floors++)
                        foreach (var actor in actors)
                        {
                            CheckBudget(1.0);
                            var orientation = size.Width > size.Height ? "landscape-1600x900" : "portrait-720x1280";
                            Checkpoint($"matrix setup {actor.appearance}-{floors}F-{orientation}-{scale.ToString("0.00", CultureInfo.InvariantCulture)}x");
                            var display = new RoomDisplay($"motion-synthetic-{orientation}-{scale}", "Synthetic motion fixture", 0, size, scale, false)
                            { Adapter = "synthetic", PixelBounds = new(0, 0, size.Width * scale, size.Height * scale), PixelWorkArea = new(0, 0, size.Width * scale, size.Height * scale) };
                            DisplayWorkspace.Select(display.Id, [display]);
                            foreach (var other in actors) { other.timer.Stop(); other.SetPaused(false); other.SetRoomEditing(false); if (other != actor) other.Hide(); }
                            actor.Show(); actor.timer.Stop(); actor.simulationEnabled = true; owner.ConfigureHouse(floors, false);
                            var result = new Motion012Case { Character = actor.appearance.ToString(), Floors = floors, Orientation = orientation,
                                RequestedScale = scale, SceneScale = owner.House!.SceneScale, ObservedNativeDpi = DisplayWorkspace.NativeWindowBounds(actor).Dpi };
                            cases.Add(result);
                            Checkpoint("matrix " + result.Case);
                            var nativeWritesBefore = actor.PresentationWindowWrites;
                            var sheetKey = actor.appearance + "-" + orientation;
                            if (!sheets.TryGetValue(sheetKey, out var images)) sheets.Add(sheetKey, images = []);
                            var house = owner.House!; var startX = house.SafeFootX(0, house.Floors[0].Left + house.Floors[0].Width * .35, actor.HalfWidth);
                            PlaceMotionActor(actor, 0, startX);
                            var time = 1d;
                            WarmMotionWalk(actor, time);
                            Motion012Sample? previousSample = null;
                            var sampled = new HashSet<string>();
                            var activeDestination = 0;
                            foreach (var destination in floors == 1 ? new[] { 0 } : new[] { floors - 1, 0 })
                            {
                                activeDestination = destination;
                                var goalX = house.SafeFootX(destination, house.Floors[destination].Left + house.Floors[destination].Width * .6, actor.HalfWidth);
                                var reached = false;
                                for (var frame = 0; frame < 2000 && !reached; frame++)
                                {
                                    if (frame % 16 == 0) CheckBudget(1.0);
                                    time += .025; MotionRouteFrame(actor, destination, goalX, .025, time, 160);
                                    // Sample both the intermediate presentation and its final committed point.
                                    actor.ApplyPresentedSample(time + .0125, staticStair: false); Observe();
                                    actor.ApplyPresentedSample(time + .025, staticStair: false); Observe();
                                    result.RouteFrames++;
                                    reached = !actor.FinishingMotion && Math.Abs(actor.body.X + actor.HalfWidth - goalX) < .05
                                        && Math.Abs(actor.body.Y + actor.BodyHeight - house.Floors[destination].Y) < .02;
                                }
                                if (!reached) result.Failures.Add($"Production route did not reach floor {destination}.");
                            }
                            actor.body.Action = BodyAction.Idle; actor.poseAction = null; actor.ApplyPose(time + .1); actor.ApplyPosition();
                            var stride = MotionWalkDistance(actor); var stoppedFrame = actor.sprite.PresentedFrameIndex; var stoppedOrigin = actor.PresentedOrigin;
                            for (var i = 0; i < 4; i++) actor.ApplyPresentedSample(time + .1 + i * .02, staticStair: false);
                            result.FloorStoppedWithoutStride = MotionWalkDistance(actor) == stride && actor.sprite.PresentedFrameIndex == stoppedFrame
                                && MotionDistance(stoppedOrigin, actor.PresentedOrigin) <= .01 && !actor.presentationHooked;
                            if (!result.FloorStoppedWithoutStride) result.Failures.Add("Floor stop retained stride, movement or a position-render callback.");
                            if (floors > 1 && result.RasterContactSamples == 0) result.Failures.Add("No independently measured raster stance contact.");
                            if (result.MaxRasterContactErrorDip > 2) result.Failures.Add("Actual painted stance foot missed the tread by more than 2 DIP.");
                            if (result.MaxUnexpectedRootJumpDip > 2) result.Failures.Add("Presented root jump exceeded physical advance * 2.25 + 2 DIP.");
                            if (images.Count == 0 || floors == 1) images.Add(new(RenderMotionSprite(actor, scale), $"{result.Character} {floors}F {scale:P0} floor stop", null, null));
                            actor.SetSimulationEnabled(false);
                            result.NativePositionWrites = actor.PresentationWindowWrites - nativeWritesBefore;
                            if (result.NativePositionWrites != 0) result.Failures.Add("Offscreen matrix unexpectedly wrote native window positions.");
                            result.Completed = true;

                            void Observe()
                            {
                                var origin = actor.PresentedOrigin; var model = actor.Position; var currentFrame = actor.sprite.PresentedFrameIndex;
                                var currentSample = new Motion012Sample(origin, model, currentFrame, actor.PresentedStairPose,
                                    actor.presentationStair?.ConnectorId, actor.MovementPhase);
                                if (previousSample is {} old)
                                {
                                    var jump = MotionDistance(old.Origin, origin); var physical = MotionDistance(old.Model, model);
                                    // Two samples share a physical update. Bound them with half that update
                                    // rather than presenting a zero simulation delta as an unlimited jump.
                                    var allowance = Math.Max(160 * house.SceneScale * .0125, physical) * 2.25;
                                    result.MaxPresentedRootJumpDip = Math.Max(result.MaxPresentedRootJumpDip, jump);
                                    if (jump - allowance > result.MaxUnexpectedRootJumpDip)
                                    {
                                        result.MaxUnexpectedRootJumpDip = jump - allowance;
                                        result.PeakJumpContext = new { Previous = old, Current = currentSample, JumpDip = jump, PhysicalAdvanceDip = physical, AllowanceDip = allowance };
                                    }
                                    if (currentFrame != old.Frame) result.MaxFrameBoundaryJumpDip = Math.Max(result.MaxFrameBoundaryJumpDip, jump);
                                }
                                previousSample = currentSample;
                                if (actor.PresentedStairPose is not { PlantWeight: >= .999999 } pose) return;
                                if (!actor.StairRasterAlignmentAvailable)
                                {
                                    result.MissingCalibrationSamples++;
                                    if(result.MissingCalibrationEvidence.Count<4)
                                        result.MissingCalibrationEvidence.Add(new{pose.StepIndex,pose.StepProgress,pose.PlantWeight,Sprite=actor.sprite.RasterFootDiagnostic});
                                    return;
                                }
                                var sampleKey = $"{activeDestination}:{actor.presentationStair?.ConnectorId}:{currentFrame}";
                                if (sampled.Count >= 16 || !sampled.Add(sampleKey)) return;
                                var raster = MeasureMotionRaster(actor, pose.PlantPoint, scale);
                                result.RasterContactSamples++; result.MaxRasterContactErrorDip = Math.Max(result.MaxRasterContactErrorDip, raster.ErrorDip);
                                result.Contacts.Add(new { Frame = currentFrame, pose.StepIndex, pose.StepProgress, pose.PlantSide, pose.PlantWeight,
                                    ExpectedWorldFoot = pose.PlantPoint, RasterFootPixels = raster.ActualFootPixels, raster.ErrorDip, raster.ActualFootPainted,
                                    PresentedOrigin = origin, Scope = "Actual WPF offscreen raster at synthetic requested DPI and production presented origin; not an HWND route or monitor FPS" });
                                if (!raster.ActualFootPainted) result.Failures.Add("The selected stance contact is not a solid pixel in the independently rendered sprite.");
                                if (images.Count < 24 && (sampled.Count <= 2 || raster.ErrorDip > 2))
                                    images.Add(new(raster.Image, $"{result.Character} {floors}F {scale:P0} ->{activeDestination} frame {currentFrame} error {raster.ErrorDip:F2}", raster.ActualFootPixels, raster.ExpectedFootPixels));
                            }
                        }
            CheckBudget(.8);
            Checkpoint("actual-display rendering setup");
            DisplayWorkspace.Select(originalDisplay.Id, [originalDisplay]);
            foreach (var actor in actors)
            {
                actor.diagnosticRasterPresentation = false;
                actor.presentedPixels = null;
                actor.SnapPresentation();
            }
            owner.ConfigureHouse(2, false);
            foreach (var actor in actors)
            {
                actor.Show(); actor.SetPaused(false); actor.SetRoomEditing(false); actor.simulationEnabled = true; actor.timer.Stop(); actor.sprite.SetCharacter(actor.appearance);
                var entrance = owner.House!.Stairs[0].LowerLanding.X;
                PlaceMotionActor(actor, 0, owner.House.SafeFootX(0, entrance - actors.IndexOf(actor) * 120, actor.HalfWidth));
                WarmMotionWalk(actor, actor.clock.Elapsed.TotalSeconds + 1);
            }
            var callbackStarts = actors.ToDictionary(actor => actor, actor => actor.PresentationRenderFrames);
            Checkpoint("actual-display Rendering callbacks");
            for (var sample = 0; sample < 12; sample++)
            {
                CheckBudget(.25);
                foreach (var actor in actors)
                {
                    var now = actor.clock.Elapsed.TotalSeconds;
                    if (sample == 0) actor.presentationPosition.Snap(actor.Position, Math.Max(0, now - .033), actor.presentationGeometryToken);
                    MotionRouteFrame(actor, 1, owner.House!.Stairs[0].UpperLanding.X, .025, now, 100);
                }
                await Task.Delay(17); await Dispatcher.Yield(DispatcherPriority.Render);
            }
            foreach (var actor in actors)
            {
                Checkpoint("actual-display raster " + actor.appearance);
                var realCallbacks = actor.PresentationRenderFrames - callbackStarts[actor];
                var nativeDpi = DisplayWorkspace.NativeWindowBounds(actor).Dpi;
                Motion012Raster? liveRaster = null;
                if (actor.PresentedStairPose is { PlantWeight: >= .999999 } livePose && actor.StairRasterAlignmentAvailable)
                {
                    liveRaster = MeasureMotionRaster(actor, livePose.PlantPoint, originalDisplay.Scale);
                    if (liveRaster.ErrorDip > 2 || !liveRaster.ActualFootPainted)
                        failures.Add($"{actor.appearance}: actual-display painted stance foot missed its tread by more than 2 DIP.");
                    var nativeSheet = actor.appearance + "-native-live";
                    sheets[nativeSheet] = [new(liveRaster.Image, $"Actual HWND {nativeDpi} DPI frame {actor.sprite.PresentedFrameIndex} error {liveRaster.ErrorDip:F2}", liveRaster.ActualFootPixels, liveRaster.ExpectedFootPixels)];
                }
                live.Add(new { Character = actor.appearance.ToString(), Scope = "Real HWND placement and CompositionTarget.Rendering on selected actual display",
                    ActualDisplay = originalDisplay.Id, OriginalRequestedScale = originalDisplay.Scale, NativeDpi = nativeDpi,
                    NativeActualScaleMatched = Math.Abs(nativeDpi / 96d - originalDisplay.Scale) < .01,
                    RenderingCallbacks = realCallbacks, actor.PresentationWindowWrites, Origin = DisplayWorkspace.RoomPosition(actor),
                    IndependentRasterStanceMeasured = liveRaster is not null, RasterContactErrorDip = liveRaster?.ErrorDip });
                if (realCallbacks == 0) failures.Add($"{actor.appearance}: no actual position Rendering callback observed.");
                Checkpoint("pause " + actor.appearance);
                if (!ArmMotionCallback(actor)) failures.Add($"{actor.appearance}: pause probe could not arm a real position callback.");
                actor.SetPaused(true); if (actor.presentationHooked) failures.Add($"{actor.appearance}: pause retained position callback."); actor.SetPaused(false);
                Checkpoint("hide " + actor.appearance);
                if (!ArmMotionCallback(actor)) failures.Add($"{actor.appearance}: hide probe could not arm a real position callback.");
                actor.Hide(); if (actor.presentationHooked) failures.Add($"{actor.appearance}: hidden actor retained position callback."); actor.Show(); actor.timer.Stop();
                Checkpoint("edit " + actor.appearance);
                if (!ArmMotionCallback(actor)) failures.Add($"{actor.appearance}: edit probe could not arm a real position callback.");
                actor.SetRoomEditing(true); if (actor.presentationHooked) failures.Add($"{actor.appearance}: editing retained position callback."); actor.SetRoomEditing(false);
                Checkpoint("remap " + actor.appearance);
                if (!ArmMotionCallback(actor)) failures.Add($"{actor.appearance}: remap probe could not arm a real position callback.");
                actor.RemapWorkspace(originalDisplay.Bounds, originalDisplay.Bounds);
                if (actor.presentationHooked) failures.Add($"{actor.appearance}: workspace remap retained position callback.");
                if (actor.EmotionalState != needs[actor]) failures.Add($"{actor.appearance}: presentation checks changed needs.");
                Checkpoint("arm close " + actor.appearance);
                if (!ArmMotionCallback(actor)) failures.Add($"{actor.appearance}: close probe could not arm a real position callback.");
            }
            liveCompleted = true;
        }
        catch (TimeoutException ex) { incomplete = true; failures.Add(ex.Message); WriteCheckpoint(stage, ex); }
        catch (Exception ex) { failures.Add(ex.ToString()); WriteCheckpoint(stage, ex); }
        finally
        {
            foreach (var actor in actors.AsEnumerable().Reverse())
            {
                Checkpoint("closing " + actor.appearance);
                try { actor.timer.Stop(); actor.Close(); }
                catch (Exception ex) { failures.Add($"{actor.appearance}: cleanup failed: {ex}"); WriteCheckpoint(stage, ex); }
                if (actor.presentationHooked) failures.Add($"{actor.appearance}: close retained position callback.");
            }
            Checkpoint("restoring display and draining Loaded callbacks");
            try
            {
                DisplayWorkspace.Select(originalDisplay.Id, [originalDisplay]);
                await Dispatcher.Yield(DispatcherPriority.Loaded);
                closedCleanly = Application.Current.Windows.Count == baselineWindows;
            }
            catch (Exception ex) { failures.Add("Display/window cleanup failed: " + ex); WriteCheckpoint(stage, ex); }
            Application.Current.DispatcherUnhandledException -= callbackObserver;
            if (!closedCleanly) failures.Add("Fresh motion fixtures leaked native/WPF windows.");
        }
        if (callbackFailure) failures.Add("An unhandled Dispatcher/native callback exception occurred; this run cannot pass.");
        foreach (var sheet in sheets)
        {
            if (budget.Elapsed.TotalSeconds >= 9.8) { incomplete = true; break; }
            try { WriteMotionSheet(Path.Combine(full, "motion-" + sheet.Key + ".png"), sheet.Value); }
            catch (Exception ex) { failures.Add("Contact sheet failed: " + ex); WriteCheckpoint("writing " + sheet.Key, ex); }
        }
        var status = callbackFailure ? "FAIL" : incomplete || cases.Count != 54 || !liveCompleted ? "INCOMPLETE" : failures.Count > 0 || cases.Any(value => !value.Passed) ? "FAIL" : "PASS";
        File.WriteAllText(Path.Combine(full, "house-motion-report.json"), JsonSerializer.Serialize(new
        {
            Status = status, Scope = "Component checks: 54 offscreen production navigation/presentation samples with actual WPF raster at synthetic DPI, separately bounded real HWND Rendering probe; not phone FPS or release verification",
            StartedUtc = started, ElapsedSeconds = budget.Elapsed.TotalSeconds, LimitSeconds = 10, MatrixExpected = 54,
            InterpolationEvidence = new { CompressedSimulationTime = true, OffscreenPresentation = true, Extrapolation = false,
                RouteFrames = cases.Sum(value => value.RouteFrames), NativePositionWrites = cases.Sum(value => value.NativePositionWrites) },
            RasterEvidence = new { Method = "Independent solid alpha clusters from RenderTargetBitmap; no expected-foot delegate used for detection", FootToleranceDip = 2,
                Samples = cases.Sum(value => value.RasterContactSamples), SyntheticDpiDoesNotChangeHwndDpi = true },
            ContinuityGate = new { ExtraJumpToleranceDip = 2, PhysicalAdvanceMultiplier = 2.25, ProvisionalPrototypeGate = true },
            LiveRenderingEvidence = live, Lifecycle = new { liveCompleted, closedCleanly, PositionCallbackChecks = "pause/hide/edit/remap/close", NeedsUnchanged = actors.All(actor => needs.TryGetValue(actor, out var old) && old == actor.EmotionalState) },
            Failures = failures, RuntimeException = checkpointException, Cases = cases
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (status != "PASS") throw new InvalidOperationException($"House motion component check {status}; see house-motion-report.json.");

        void CheckBudget(double reservedSeconds)
        {
            if (callbackFailure) throw new InvalidOperationException("An unhandled motion callback was recorded; remaining checks are cancelled.");
            if (budget.Elapsed.TotalSeconds >= 10 - reservedSeconds) throw new TimeoutException("Motion fixture reached its ten-second component budget; unfinished evidence is not PASS.");
        }
        void Checkpoint(string nextStage) { stage = nextStage; WriteCheckpoint(nextStage); }
        void WriteCheckpoint(string atStage, Exception? exception = null)
        {
            // A crash may prevent finally from running. Keep evidence on disk
            // before every native lifecycle boundary, never a provisional PASS.
            try
            {
                if (exception is not null) checkpointException = exception.ToString();
                File.WriteAllText(Path.Combine(full, "house-motion-report.json"), JsonSerializer.Serialize(new
                {
                    Status = checkpointException is not null || callbackFailure ? "FAIL" : "INCOMPLETE",
                    Scope = "Unsealed checkpoint: 54 offscreen production presentation samples; bounded actual HWND probe reported separately; not release verification",
                    Stage = atStage, StartedUtc = started, ElapsedSeconds = budget.Elapsed.TotalSeconds, LimitSeconds = 10,
                    MatrixExpected = 54, MatrixStarted = cases.Count, Exception = checkpointException,
                    CallbackFailure = callbackFailure, Failures = failures.ToArray(),
                    Cases = cases.ToArray(), LiveRenderingEvidence = live.ToArray()
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception) { /* Preserve the original callback exception if reporting itself fails. */ }
        }
    }

    private static void PlaceMotionActor(PetWindow actor, int floorIndex, double x)
    {
        actor.sequence = null; actor.ClearHouseRoute(); actor.CancelRoute(); actor.Occupancy.Release(actor.appearance); actor.interactionUntil = 0;
        actor.homeTarget = null; actor.body.Action = BodyAction.Walk; actor.poseAction = BodyAction.Walk; actor.houseControlledThisFrame = false;
        var floor = actor.House!.Floors[floorIndex]; actor.body.Place(x - actor.HalfWidth, floor.Y - actor.BodyHeight, Bounds());
        actor.petGravity.PlaceSupported(actor.body.X, actor.body.Y, actor.BodyWidth, actor.BodyHeight, floor.Platform);
        actor.ApplyPose(); actor.ApplyPosition();
    }
    private static void WarmMotionWalk(PetWindow actor, double time)
    { for (var i = 0; i <= 6; i++) actor.ApplyPose(time + i * .1); }
    private static void MotionRouteFrame(PetWindow actor, int goalFloor, double goalX, double dt, double time, double speed)
    {
        actor.BeginPresentationTick(time);
        try
        {
            var before = new WalkDistanceSample(actor.body.X, actor.body.Y, actor.petGravity.Grounded); var stairBefore = actor.IsOnStair;
            actor.houseControlledThisFrame = false; actor.navigationStepped = false; actor.poseAction = BodyAction.Walk;
            actor.Navigate(goalX - actor.HalfWidth, actor.House!.Floors[goalFloor].Y - actor.BodyHeight, dt, Bounds(), speed);
            actor.StepPetGravity(dt); if (actor.body.X != before.X) actor.facing = Math.Sign(actor.body.X - before.X);
            var kind = actor.houseControlledThisFrame && (stairBefore || actor.IsOnStair) ? WalkDistanceKind.Stair : WalkDistanceKind.Floor;
            actor.sprite.AdvanceWalk(WalkDistanceSampler.Measure(before, new(actor.body.X, actor.body.Y, actor.petGravity.Grounded), actor.BodyHeight / 144, kind));
            actor.ApplyPose(time);
            // The compressed offscreen route must also advance the same facing
            // interpolation as Rendering; otherwise a turn can remain frozen
            // edge-on for an entire synthetic case.
            if(actor.diagnosticRasterPresentation)actor.sprite.AdvancePresentationForDiagnostic(dt);
            actor.ApplyPosition();
        }
        finally { actor.EndPresentationTick(); }
    }
    private static bool ArmMotionCallback(PetWindow actor)
    {
        actor.ClearHouseRoute(); actor.CancelRoute(); actor.SelectPresentationStair(null);
        var floor = actor.House!.Floors[0]; PlaceMotionActor(actor, 0, floor.Left + floor.Width * .35);
        actor.simulationEnabled = true; actor.timer.Stop(); actor.SetPaused(false);
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var now = actor.clock.Elapsed.TotalSeconds; actor.presentationPosition.Snap(actor.Position, Math.Max(0, now - .033), actor.presentationGeometryToken);
            MotionRouteFrame(actor, 0, floor.Left + floor.Width * .7, .025, now, 100);
            if (actor.presentationHooked) return true;
        }
        return false;
    }
    private static double MotionWalkDistance(PetWindow actor)
        => JsonSerializer.SerializeToElement(actor.sprite.AssetDiagnostic).GetProperty("WalkDistance").GetDouble();
    private static double MotionDistance(RoomPoint a, RoomPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static double MotionDistance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static BitmapSource RenderMotionSprite(PetWindow actor, double dpiScale)
    {
        actor.UpdateLayout();
        // Render the actual visual directly at final viewbox/DPI scale. A 96 DPI
        // intermediate bitmap loses sole pixels before the second enlargement
        // and measures a different image from the production Viewbox.
        var drawing = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(drawing, BitmapScalingMode.HighQuality);
        using (var dc = drawing.RenderOpen())
            dc.DrawRectangle(new VisualBrush(actor.sprite) { ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 116, 144), Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null, new Rect(0, 0, actor.BodyWidth, actor.BodyHeight));
        var image = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(actor.BodyWidth * dpiScale)), Math.Max(1, (int)Math.Ceiling(actor.BodyHeight * dpiScale)), 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32);
        image.Render(drawing); image.Freeze(); return image;
    }
    private static Motion012Raster MeasureMotionRaster(PetWindow actor, RoomPoint expected, double scale)
    {
        var image = RenderMotionSprite(actor, scale);
        var origin = actor.diagnosticRasterPresentation ? actor.PresentedOrigin : DisplayWorkspace.RoomPosition(actor);
        var expectedPixel = new Point((expected.X - origin.X) * scale, (expected.Y - origin.Y) * scale);
        var pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(pixels, image.PixelWidth * 4, 0);
        var columns = new List<Point>(); var bottom = -1;
        for (var x = 0; x < image.PixelWidth; x++)
            for (var y = image.PixelHeight - 1; y >= (int)(image.PixelHeight * .75); y--)
                if (pixels[(y * image.PixelWidth + x) * 4 + 3] >= 120) { columns.Add(new(x + .5, y + .5)); bottom = Math.Max(bottom, y); break; }
        var nearBottom = columns.Where(point => point.Y >= bottom - Math.Max(3, image.PixelHeight * .07)).ToArray();
        var groups = new List<List<Point>>();
        foreach (var point in nearBottom)
        {
            if (groups.Count == 0 || point.X - groups[^1][^1].X > 2) groups.Add([]);
            groups[^1].Add(point);
        }
        var candidates = groups.Where(group => group.Count >= 2).Select(group =>
        { var y = group.Max(point => point.Y); var sole = group.Where(point => point.Y >= y - 1).ToArray(); return sole[sole.Length / 2]; }).ToArray();
        Point? actual = candidates.Length == 0 ? null : candidates.OrderBy(point => MotionDistance(point, expectedPixel)).First();
        var error = actual is {} found ? MotionDistance(found, expectedPixel) / scale : 1000000;
        return new(image, actual, expectedPixel, error, actual is not null);
    }
    private static void WriteMotionSheet(string path, IReadOnlyList<Motion012Image> frames)
    {
        if (frames.Count == 0) return;
        const int columns = 4, tileWidth = 232, tileHeight = 272;
        var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(Brushes.FloralWhite, null, new Rect(0, 0, columns * tileWidth, ((frames.Count + columns - 1) / columns) * tileHeight));
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i]; var x = i % columns * tileWidth; var y = i / columns * tileHeight;
                var scale = Math.Min((tileWidth - 20d) / frame.Bitmap.PixelWidth, (tileHeight - 54d) / frame.Bitmap.PixelHeight);
                var box = new Rect(x + 10, y + 40, frame.Bitmap.PixelWidth * scale, frame.Bitmap.PixelHeight * scale);
                dc.DrawImage(frame.Bitmap, box);
                if (frame.ExpectedFoot is {} target) dc.DrawEllipse(null, new Pen(Brushes.Red, 1.5), new(box.X + target.X * scale, box.Y + target.Y * scale), 5, 5);
                if (frame.ActualFoot is {} actual) dc.DrawEllipse(Brushes.DodgerBlue, null, new(box.X + actual.X * scale, box.Y + actual.Y * scale), 2, 2);
                var label = new FormattedText(frame.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.SaddleBrown, 1)
                { MaxTextWidth = tileWidth - 16, MaxTextHeight = 34 };
                dc.DrawText(label, new Point(x + 8, y + 4));
            }
        }
        var image = new RenderTargetBitmap(columns * tileWidth, ((frames.Count + columns - 1) / columns) * tileHeight, 96, 96, PixelFormats.Pbgra32); image.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
