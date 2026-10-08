using DesktopLife.Core;
namespace DesktopLife.App;

public partial class PetWindow
{
    private RoomActivity? roomActivity;
    private bool activityProduced;
    private RoomItem? activityChair;
    private readonly GirlLifeVisual girlLife=new(){Width=116,Height=144,IsHitTestVisible=false};
    private int activityTemplate;
    public RoomActivity? CurrentRoomActivity=>sequence?.Kind==SequenceKind.Activity?roomActivity:null;
    public int InvalidFurnitureInteractions {get;private set;}
    public void StartRoomActivity(RoomActivity activity)
    {
        if(!RoomActivityPolicy.Allowed(appearance,activity))return;
        requestedActivity=activity;SetAction(activity switch{RoomActivity.Feed=>BodyAction.Eat,RoomActivity.HairCare=>BodyAction.Groom,RoomActivity.Draw=>BodyAction.DrawDoodle,RoomActivity.Write=>BodyAction.WriteNote,_=>BodyAction.Sit});
    }
    private RoomActivity? requestedActivity;
    private bool TryBeginRoomActivity(BodyAction action)
    {
        var activity=requestedActivity;requestedActivity=null;
        activity??=action switch
        {
            BodyAction.Eat=>RoomActivity.Feed,
            BodyAction.Groom when appearance==PetAppearance.Girl=>RoomActivity.HairCare,
            BodyAction.DrawDoodle when appearance==PetAppearance.Girl=>RoomActivity.Draw,
            BodyAction.WriteNote when appearance==PetAppearance.Girl=>RoomActivity.Write,
            BodyAction.PlayToy or BodyAction.PseudoPushIcon when appearance==PetAppearance.Girl=>ChooseGirlActivity(),
            _=>null
        };
        if(activity is null)return false;
        if(!RoomActivityPolicy.Allowed(appearance,activity.Value))return false;
        roomActivity=activity;activityProduced=false;activityChair=null;activityTemplate=movementRandom.Next(4);
        var use=RoomActivityPolicy.Use(activity.Value);var b=Bounds();var platforms=RoomPlatforms();
        var room=Furniture.Select(f=>f.Item).ToArray();
        var candidates=Furniture.Where(f=>!EditingRoom&&FurnitureCompatibility.CanUse(appearance,f.Item.Kind,use)&&Occupancy.Available(FurnitureCompatibility.Reservation(f.Item,appearance),appearance))
            .Select(f=>(Window:f,Spot:ActivitySpot(f,activity.Value,room)))
            .Where(c=>CanReach(c.Spot.X,c.Spot.Feet))
            .ToArray();
        if(candidates.Length>0)
        {
            var rank=candidates.Min(c=>RoomActivityPolicy.Rank(appearance,c.Window.Item,activity.Value,room));
            var eligible=candidates.Where(c=>RoomActivityPolicy.Rank(appearance,c.Window.Item,activity.Value,room)==rank).ToArray();
            var selected=restPreference.ChooseHome(eligible.Select(c=>c.Spot),BehaviorPersonality,Bond,body.X+HalfWidth,cursor?.X,DateTimeOffset.UtcNow,EmotionalState.Fatigue,movementRandom)!;
            var target=eligible.First(c=>c.Spot==selected).Window;
            if(Occupancy.TryAcquire(FurnitureCompatibility.Reservation(target.Item,appearance),appearance))
            {
                homeTarget=target.Item;homeX=selected.X;homeFeet=selected.Feet;
                if(appearance==PetAppearance.Girl&&target.Item.Kind is FurnitureKind.Desk or FurnitureKind.DiningTable)
                {
                    activityChair=RoomActivityPolicy.NearbyChair(target.Item,room);
                    if(activityChair is {} chair&&!Occupancy.TryAcquire(FurnitureCompatibility.Reservation(chair,appearance),appearance))
                    {Occupancy.Release(appearance);homeTarget=null;activityChair=null;}
                }
            }
        }
        if(homeTarget is null)
        {homeX=Math.Clamp(body.X+HalfWidth,b.Left+HalfWidth,b.Left+b.Width-HalfWidth);homeFeet=LocalFloor;}
        homeUseRecorded=false;
        sequence=new(SequenceKind.Activity,BehaviorPersonality,Bond,homeTarget?.Id.ToString(),homeTarget?.Kind,character:appearance,activity:activity,approachBudgetSeconds:EstimateApproachBudget(homeX,homeFeet,Parameters.Movement.Speed*.8));
        return true;
    }
    private RoomActivity ChooseGirlActivity()
    {
        var choices=new List<(RoomActivity Activity,double Weight)>();
        foreach(var a in new[]{RoomActivity.Draw,RoomActivity.Read,RoomActivity.Lego,RoomActivity.Computer})
            if(Furniture.Any(f=>FurnitureCompatibility.CanUse(appearance,f.Item.Kind,RoomActivityPolicy.Use(a))))
                choices.Add((a,a==RoomActivity.Draw?.2+BehaviorPersonality.Creativity:a==RoomActivity.Read?.2+BehaviorPersonality.Curiosity:a==RoomActivity.Lego?.2+BehaviorPersonality.Playfulness:.5));
        var draw=movementRandom.NextDouble()*choices.Sum(c=>c.Weight);
        foreach(var c in choices){draw-=c.Weight;if(draw<=0)return c.Activity;}
        return RoomActivity.Draw;
    }
    private RestSpot ActivitySpot(RoomWindow f,RoomActivity activity,IReadOnlyList<RoomItem> room)
    {
        var b=Bounds();double x=f.Item.X+f.Width/2,feet=RoomActivityPolicy.StandingFeet(f.Item,House,b);
        if(appearance==PetAppearance.Girl)
        {
            var chair=f.Item.Kind is FurnitureKind.Desk or FurnitureKind.DiningTable?RoomActivityPolicy.NearbyChair(f.Item,room):null;
            if(chair is not null){var seat=Furniture.First(w=>w.Item.Id==chair.Id);x=chair.X+seat.Width/2;feet=seat.Platforms.FirstOrDefault().Y;}
            else if(f.Item.Kind==FurnitureKind.Chair){feet=f.Platforms.FirstOrDefault().Y;}
            else if(f.Item.Kind is FurnitureKind.HumanBed or FurnitureKind.Sofa){x=f.Item.X+65*sceneScale;feet=f.Platforms[0].Y;}
            else{x=f.Item.X+40*sceneScale;}
        }
        else if(activity==RoomActivity.Rest&&f.Platforms.Count>0)feet=f.Platforms[0].Y;
        return new(f.Item.Id+":"+FurnitureCompatibility.Zone(appearance,f.Item.Kind),f.Item.Kind,Math.Clamp(x,b.Left+HalfWidth,b.Left+b.Width-HalfWidth),feet);
    }
    private bool TickRoomActivity(BehaviorSequence s,double dt)
    {
        var use=RoomActivityPolicy.Use(roomActivity!.Value);
        var exists=homeTarget is null||!EditingRoom&&Furniture.Any(f=>f.Item==homeTarget)&&(activityChair is null||Furniture.Any(f=>f.Item==activityChair));
        if(homeTarget is {} target&&!FurnitureCompatibility.CanUse(appearance,target.Kind,use))
        {InvalidFurnitureInteractions++;s.Interrupt(BehaviorInterruptReason.Safety);}
        var reached=Math.Abs(body.X+HalfWidth-homeX)<4&&Math.Abs(body.Y+BodyHeight-homeFeet)<5;
        s.Step(dt,new(TargetExists:exists,Reached:reached,Grounded:petGravity.Grounded,NavigationFailed:MovementPhase==NavigationPhase.Unreachable));
        attentionPoint=new(homeX,homeFeet-60);sequencePoseAge=s.PhaseAge;
        poseAction=s.Phase==BehaviorPhase.Approach?BodyAction.Walk:appearance!=PetAppearance.Girl&&s.Phase==BehaviorPhase.Work?BodyAction.Eat:BodyAction.Sit;
        if(s.Phase==BehaviorPhase.Approach)MovePetToward(homeX-HalfWidth,homeFeet-BodyHeight,dt,Bounds(),Parameters.Movement.Speed*.8);
        if(s.Phase==BehaviorPhase.Close&&!activityProduced&&s.InterruptedBy is null)
        {
            activityProduced=true;LearnHome(use);
            if(roomActivity is RoomActivity.Draw or RoomActivity.Write)
                CreativeAction?.Invoke(roomActivity==RoomActivity.Draw?BodyAction.DrawDoodle:BodyAction.WriteNote,body.X-Bounds().Left+80,body.Y-Bounds().Top-60);
        }
        if(s.Finished)EndSequence();return true;
    }
    private void UpdateGirlLife(double time)
    {
        if(!Character.Children.Contains(girlLife))Character.Children.Add(girlLife);
        var show=!sprite.Ready&&appearance==PetAppearance.Girl&&sequence is {} s&&s.Phase!=BehaviorPhase.Approach&&s.Kind is SequenceKind.Activity or SequenceKind.Sleep;
        girlLife.Visibility=show?System.Windows.Visibility.Visible:System.Windows.Visibility.Collapsed;
        GirlVisual.Opacity=show?0:1;
        if(show)
        {
            girlLife.Pose(CurrentRoomActivity,sequence!.Phase,time,sequence.PhaseAge,sequence.Kind==SequenceKind.Sleep,homeTarget?.Kind==FurnitureKind.HumanBed,activityTemplate);
            FoodBowl.Visibility=GroomComb.Visibility=PettingHand.Visibility=Pencil.Visibility=System.Windows.Visibility.Collapsed;
        }
    }
}
