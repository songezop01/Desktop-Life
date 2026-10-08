using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    public void SmokePhysiologyLifecycle()
    {
        var originalPosition=Position;var originalPause=paused;var originalVisibility=IsVisible;
        var originalBall=new RoomPoint(Ball.Model.X,Ball.Model.Y);var originalBallVelocity=new RoomPoint(Ball.Model.VelocityX,Ball.Model.VelocityY);var originalBallHeld=Ball.Model.Held;
        try
        {
            SetPaused(false);Show();ResetPosition();SetAction(BodyAction.Sleep);
            if(sequence is not {Kind:SequenceKind.Sleep} sleep)throw new Exception("Physiology sleep fixture missing");
            if(PhysiologicalAction==BodyAction.Sleep)throw new Exception("Sleep preparation recovered needs");
            for(var i=0;i<200&&sleep.Phase!=BehaviorPhase.Sleep;i++)sleep.Step(.1,new(Reached:true,Grounded:true));
            if(sleep.Phase!=BehaviorPhase.Sleep)throw new Exception("Sleep fixture did not reach sleep");
            var bounds=Bounds();var at=restSpot;
            body.Place(at is null?bounds.Left+12:at.X-HalfWidth,at is null?NavigationFloor-BodyHeight:at.Feet-BodyHeight,bounds);
            petGravity.VX=petGravity.VY=0;StepPetGravity(.02);
            if(PhysiologicalAction!=BodyAction.Sleep)throw new Exception("Grounded actual sleep did not recover needs");
            var phaseAge=sleep.PhaseAge;SetPaused(true);Tick(null,EventArgs.Empty);
            if(PhysiologicalAction!=BodyAction.Idle||sleep.PhaseAge!=phaseAge)throw new Exception("Paused sleep retained recovery or advanced sequence");
            var pausedLife=new HomeostasisSession(new());pausedLife.AdvanceCompanion(TimeSpan.FromSeconds(1),PhysiologicalAction,false);
            if(pausedLife.State!=CompanionCare.Step(new(),TimeSpan.FromSeconds(1),BodyAction.Idle,false))throw new Exception("Paused physiology was not ordinary online metabolism");
            SetPaused(false);
            if(PhysiologicalAction!=BodyAction.Sleep)throw new Exception("Actual sleep did not resume after pause");
            body.Place(bounds.Left+12,bounds.Top+20,bounds);petGravity.VX=petGravity.VY=0;StepPetGravity(.02);
            if(petGravity.Grounded||PhysiologicalAction==BodyAction.Sleep)throw new Exception("Airborne sleep recovered needs");
            SuspendPresence();
            if(IsVisible||CurrentPhase is not null||CurrentAction!=BodyAction.Idle||PhysiologicalAction!=BodyAction.Idle)throw new Exception("Hidden character retained stale sleep");
            Show();
            if(PhysiologicalAction==BodyAction.Sleep||CurrentPhase is not null)throw new Exception("Show restored canceled sleep without preparation");
            ResetPosition();body.Place(bounds.Left+12,NavigationFloor-BodyHeight,bounds);StepPetGravity(.02);
            body.Action=BodyAction.PlayToy;sequence=new(SequenceKind.Play,BehaviorPersonality,Bond,"physiology-play",character:appearance);
            if(PhysiologicalAction==BodyAction.PlayToy)throw new Exception("Play notice earned play benefit");
            for(var i=0;i<200&&sequence.Phase!=BehaviorPhase.Contact;i++)sequence.Step(.1,new(Reached:true,Grounded:true));
            phaseContact=false;
            if(PhysiologicalAction==BodyAction.PlayToy)throw new Exception("Play contact attempt earned benefit without physical contact");
            phaseContact=true;
            if(PhysiologicalAction!=BodyAction.PlayToy)throw new Exception("Grounded play contact lost play benefit");
            SetPaused(true);
            if(PhysiologicalAction!=BodyAction.Idle)throw new Exception("Paused play retained benefit");
            SetPaused(false);sequence=null;phaseContact=false;playTarget=Ball;body.Action=BodyAction.PlayToy;
            Ball.Model.Held=false;Ball.Model.Place(bounds.Left+400,NavigationFloor-40,bounds);
            body.Place(bounds.Left+12,NavigationFloor-BodyHeight,bounds);StepPetGravity(.02);
            if(PhysiologicalAction==BodyAction.PlayToy)throw new Exception("Bare play intent earned benefit before touching toy");
            var approach=ToyApproach(Ball);body.Place(approach.X,approach.Y,bounds);StepPetGravity(.02);
            if(PhysiologicalAction!=BodyAction.PlayToy)throw new Exception("Grounded fallback toy contact lost play benefit");
            Ball.Model.Held=true;
            if(PhysiologicalAction==BodyAction.PlayToy)throw new Exception("Held toy earned play benefit");
            SuspendPresence();Show();
            if(PhysiologicalAction!=BodyAction.Idle||CurrentAction!=BodyAction.Idle)throw new Exception("Show restored canceled play intent");
            ResetPosition();StepPetGravity(.02);
            var completions=NavigationRecoveriesCompleted;var timeouts=NavigationRecoveryTimeouts;
            RecoverNavigation("geometry-changed-in-flight");var recoveryPosition=Position;
            SetRoomEditing(true);EndSequence();
            if(MovementPhase!=NavigationPhase.Recovering)throw new Exception("Editing/activity cleanup discarded a pending safety escape");
            SetRoomEditing(false);
            SuspendPresence();
            if(IsVisible||CurrentPhase is not null||CurrentAction!=BodyAction.Idle||Position!=recoveryPosition||MovementPhase!=NavigationPhase.Recovering)
                throw new Exception("Presence suspension discarded or moved an unfinished safety escape");
            Show();
            for(var i=0;i<400&&MovementPhase==NavigationPhase.Recovering;i++){StepNavigationRecovery(.016);StepPetGravity(.016);}
            if(!petGravity.Grounded||NavigationRecoveriesCompleted!=completions+1||NavigationRecoveryTimeouts!=timeouts)
                throw new Exception("Reshown character did not finish its real grounded safety escape");
        }
        finally
        {
            ResetPosition();RestorePosition(originalPosition);SetPaused(originalPause);
            Ball.Model.Held=false;Ball.Model.Place(originalBall.X,originalBall.Y,Bounds());Ball.Model.Kick(originalBallVelocity.X,originalBallVelocity.Y);Ball.Model.Held=originalBallHeld;
            if(!originalVisibility)SuspendPresence();
        }
    }

    public void SmokePrepareActualSleep()
    {
        SetPaused(false);Show();ResetPosition();SetAction(BodyAction.Sleep);
        if(sequence is not {Kind:SequenceKind.Sleep} sleep)throw new Exception("Runtime sleep fixture missing");
        for(var i=0;i<200&&sleep.Phase!=BehaviorPhase.Sleep;i++)sleep.Step(.1,new(Reached:true,Grounded:true));
        var bounds=Bounds();var at=restSpot;
        body.Place(at is null?bounds.Left+12:at.X-HalfWidth,at is null?NavigationFloor-BodyHeight:at.Feet-BodyHeight,bounds);
        petGravity.VX=petGravity.VY=0;StepPetGravity(.02);
        if(PhysiologicalAction!=BodyAction.Sleep)throw new Exception("Runtime actual sleep fixture failed");
    }

    // Deterministic component smoke, not elapsed-time stress evidence.
    public void SmokeFinishActivity()
    {
        if(sequence is not {Kind:SequenceKind.Activity} s)throw new Exception("Activity not started");
        for(var i=0;i<450&&!s.Finished;i++)
        {
            s.Step(.1,new(Reached:true,Grounded:true));
            if(s.Phase==BehaviorPhase.Close&&!activityProduced)
            {
                // Exercise the production completion dispatch after physical contact is established.
                body.Place(homeX-HalfWidth,homeFeet-BodyHeight,Bounds());StepPetGravity(.02);
                TickRoomActivity(s,0);
            }
            ApplyPose(i*.1);
        }
        if(!s.Finished||s.InterruptedBy is not null)throw new Exception("Activity failed to finish");
    }
    public void SmokeLifePose(RoomActivity activity,string path)
    {
        ResetPosition();StartRoomActivity(activity);
        if(sequence is not {Kind:SequenceKind.Activity} s)throw new Exception("Missing room sequence");
        for(var i=0;i<100&&s.Phase!=BehaviorPhase.Work;i++)s.Step(.1,new(Reached:true,Grounded:true));
        ApplyPose(2);UpdateLayout();
        if(appearance==PetAppearance.Girl&&(FoodBowl.Visibility==Visibility.Visible||GroomComb.Visibility==Visibility.Visible||feline.Visibility==Visibility.Visible))throw new Exception("Girl used cat props");
        var bitmap=new RenderTargetBitmap(232,288,192,192,PixelFormats.Pbgra32);bitmap.Render(Surface);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
        SmokeFinishActivity();
    }
    public FurnitureKind? DiagnosticFurniture=>homeTarget?.Kind;
    public void SmokeSleepIdentity()
    {
        ResetPosition();SetAction(BodyAction.Sleep);
        if(homeTarget is {} target&&!FurnitureCompatibility.CanUse(appearance,target.Kind,FurnitureUse.Rest)&&!FurnitureCompatibility.CanUse(appearance,target.Kind,FurnitureUse.Sleep))throw new Exception("Invalid sleep candidate");
        if(sequence is not {Kind:SequenceKind.Sleep} s)throw new Exception("Sleep sequence missing");
        for(var i=0;i<800&&!s.Finished;i++)
        {
            s.Step(.1,new(Reached:true,Grounded:true,Rested:true));ApplyPose(i*.1);
            if(appearance==PetAppearance.Girl&&s.Phase is BehaviorPhase.Knead or BehaviorPhase.Curl or BehaviorPhase.LickPaw)throw new Exception("Girl used feline sleep phase");
        }
    }
}
public partial class MainWindow
{
    public void SmokeCharacterLife(string root)
    {
        SmokeCanceledCarePresence();
        SmokeHouseholdPhysiology();
        var other=otherCharacter??throw new Exception("Second character missing");
        var girl=settings.PetAppearance==PetAppearance.Girl?Pet:other.Window;
        PresenceOptions.SelectedIndex=(int)PresenceMode.Both;UpdatePetVisibility();
        Pet.Art.ResetCreationCooldownsForSmoke();
        foreach(var activity in Enum.GetValues<RoomActivity>())girl.SmokeLifePose(activity,Path.Combine(root,"girl-life-"+activity+".png"));
        var cat=settings.PetAppearance==PetAppearance.Cat?Pet:other.Window;
        girl.SmokeSleepIdentity();cat.SmokeSleepIdentity();
        foreach(var f in Pet.Furniture.Where(f=>f.Item.Kind is FurnitureKind.HumanBed or FurnitureKind.Sofa or FurnitureKind.Desk))
        {
            Pet.Occupancy.Release(PetAppearance.Cat);Pet.Occupancy.Release(PetAppearance.Girl);
            if(!Pet.Occupancy.TryAcquire(FurnitureCompatibility.Reservation(f.Item,PetAppearance.Girl),PetAppearance.Girl)||!Pet.Occupancy.TryAcquire(FurnitureCompatibility.Reservation(f.Item,PetAppearance.Cat),PetAppearance.Cat))throw new Exception("Shared furniture zone conflict");
        }
        if(Pet.InvalidFurnitureInteractions+other.Window.InvalidFurnitureInteractions!=0)throw new Exception("Invalid furniture interaction");
        girl.ResetPosition();cat.ResetPosition();
    }
    private void SmokeCanceledCarePresence()
    {
        var originalPresence=CurrentPresence;var originalPause=aiPaused;var originalHidden=userHidden;var originalAutomatic=automaticActions;
        var needs=careKinds.ToDictionary(kind=>kind,kind=>RuntimeFor(kind)?.Life.State??Life.State);
        var companions=careKinds.ToDictionary(kind=>kind,kind=>CompanionFor(kind));
        var previousCare=companions.ToDictionary(pair=>pair.Key,pair=>(pair.Value.CareCount,pair.Value.Bond,Memories:pair.Value.Memories.ToList(),LastCare:new Dictionary<CareKind,DateTimeOffset>(pair.Value.LastCare)));
        try
        {
            userHidden=false;PresenceOptions.SelectedIndex=(int)PresenceMode.All;UpdatePetVisibility();SetPaused(false);automaticActions=true;
            foreach(var window in characterWindows)window.ResetPosition();
            Life.ApplyCare(Life.State with{Energy=80,Fatigue=20});
            Learning.State.Companion.LastCare.Remove(CareKind.Rest);Care(CareKind.Rest);
            if(careUntil-lifeClock.Elapsed.TotalSeconds<80||Pet.CurrentAction!=BodyAction.Sleep)throw new Exception("Primary rest hold fixture failed");
            runningAction=new(BodyAction.Sleep);runningAction.Start(Pet);var previousAction=runningAction;
            foreach(var character in secondaryCharacters)
            {
                character.Life.ApplyCare(character.Life.State with{Energy=80,Fatigue=20});character.Learning.State.Companion.LastCare.Remove(CareKind.Rest);
                character.Care(CareKind.Rest,out var accepted);
                if(!accepted||character.CareHoldRemainingSeconds<80)throw new Exception("Secondary rest hold fixture failed");
            }
            var careEvidence=companions.ToDictionary(pair=>pair.Key,pair=>(pair.Value.CareCount,pair.Value.Bond,RestCareAt:pair.Value.LastCare[CareKind.Rest]));
            SetPaused(true);userHidden=true;UpdatePetVisibility();
            if(!aiPaused||secondaryCharacters.Any(character=>!character.Paused))throw new Exception("Hiding canceled user pause");
            if(careUntil>lifeClock.Elapsed.TotalSeconds||runningAction is not null||previousAction.Running||secondaryCharacters.Any(character=>character.CareHoldRemainingSeconds>0||character.HasActiveDecision))throw new Exception("Hiding retained canceled care hold or decision");
            foreach(var kind in careKinds)
            {
                var companion=CompanionFor(kind);var expected=careEvidence[kind];
                if(companion.CareCount!=expected.CareCount||companion.Bond!=expected.Bond||companion.LastCare[CareKind.Rest]!=expected.RestCareAt)throw new Exception("Hiding erased care cooldown history");
            }
            userHidden=false;UpdatePetVisibility();
            if(!aiPaused||secondaryCharacters.Any(character=>!character.Paused))throw new Exception("Showing canceled user pause");
            SetPaused(false);TickBrain(.25);
            if(runningAction is not {Running:true})throw new Exception("Primary did not decide immediately after canceled rest");
            foreach(var character in secondaryCharacters)
            {
                character.Tick(1,null,paused:false,quiet:false);
                if(!character.HasActiveDecision)throw new Exception("Secondary did not decide immediately after canceled rest");
            }
        }
        finally
        {
            foreach(var kind in careKinds)
            {
                var life=RuntimeFor(kind)?.Life??Life;life.ApplyCare(needs[kind]);
                var companion=CompanionFor(kind);var saved=previousCare[kind];
                companion.CareCount=saved.CareCount;companion.Bond=saved.Bond;companion.Memories=saved.Memories;companion.LastCare=saved.LastCare;
                CharacterWindow(kind).ResetPosition();
            }
            automaticActions=originalAutomatic;runningAction?.Stop();runningAction=null;careUntil=0;
            userHidden=originalHidden;PresenceOptions.SelectedIndex=(int)originalPresence;UpdatePetVisibility();SetPaused(originalPause);
        }
    }
    private void SmokeHouseholdPhysiology()
    {
        var originalPresence=CurrentPresence;var originalPause=aiPaused;
        var needs=secondaryCharacters.ToDictionary(character=>character.Kind,character=>character.Life.State);
        try
        {
            PresenceOptions.SelectedIndex=(int)PresenceMode.All;SetPaused(false);
            foreach(var window in characterWindows)window.SmokePhysiologyLifecycle();
            foreach(var character in secondaryCharacters)
            {
                character.Window.SmokePrepareActualSleep();
                var before=character.Life.State;
                character.Tick(1,null,paused:true,quiet:true);
                if(character.Life.State!=CompanionCare.Step(before,TimeSpan.FromSeconds(1),BodyAction.Idle,false))throw new Exception("Secondary pause applied after physiology");
                character.Tick(1,null,paused:false,quiet:true);
                if(character.Window.PhysiologicalAction!=BodyAction.Sleep)throw new Exception("Secondary paused sequence did not resume");
                character.SuspendPresence();before=character.Life.State;
                character.Tick(1,null,paused:false,quiet:true);
                if(character.Life.State!=CompanionCare.Step(before,TimeSpan.FromSeconds(1),BodyAction.Idle,false))throw new Exception("Hidden secondary retained stale physiology");
                character.Window.Show();
                if(character.Window.CurrentPhase is not null||character.Window.CurrentAction!=BodyAction.Idle)throw new Exception("Secondary show restored canceled behavior");
            }
        }
        finally
        {
            foreach(var character in secondaryCharacters){character.Life.ApplyCare(needs[character.Kind]);character.Window.ResetPosition();}
            PresenceOptions.SelectedIndex=(int)originalPresence;SetPaused(originalPause);
        }
    }
}
