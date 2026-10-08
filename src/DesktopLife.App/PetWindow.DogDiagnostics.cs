using System.Diagnostics;
using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    // A standalone world and fresh profile keep this deterministic smoke isolated
    // from the user's household, room layout, learned preferences and memories.
    public void SmokeDogPlayContact()
    {
        if(worldOwner is not null)throw new InvalidOperationException("Dog play smoke requires an isolated world.");
        timer.Stop();
        var profile=CharacterProfile.Create(PetAppearance.BorderCollie,DateTimeOffset.UtcNow);
        SetAppearance(profile.Kind);BehaviorPersonality=profile.Personality;EmotionalState=profile.Pet.State;
        var parameters=BehaviorParameters.Default with{Seed=70,Play=new(110,.3),Movement=BehaviorParameters.Default.Movement with{Speed=85}};
        ParameterFactory=_=>parameters;
        var bounds=Bounds();var floor=NavigationFloor;
        var startX=bounds.Left+100;
        AddRoomItem(new(new Guid("8462426f-3d92-4805-b6b9-4eab3c1b7641"),FurnitureKind.Yarn,startX+80,floor-40));
        AddRoomItem(new(new Guid("8462426f-3d92-4805-b6b9-4eab3c1b7642"),FurnitureKind.ToyMouse,startX+110,floor-40));
        AddRoomItem(new(new Guid("8462426f-3d92-4805-b6b9-4eab3c1b7643"),FurnitureKind.BellBall,startX+210,floor-40));
        AddRoomItem(new(new Guid("8462426f-3d92-4805-b6b9-4eab3c1b7644"),FurnitureKind.CatTree,bounds.Left+bounds.Width-250,floor-230));
        var yarn=ExtraToys.Single(toy=>toy.RoomItem?.Kind==FurnitureKind.Yarn);
        var mouse=ExtraToys.Single(toy=>toy.RoomItem?.Kind==FurnitureKind.ToyMouse);
        var bell=ExtraToys.Single(toy=>toy.RoomItem?.Kind==FurnitureKind.BellBall);
        var tree=Furniture.Single(item=>item.Item.Kind==FurnitureKind.CatTree);
        Ball.Model.Place(bounds.Left+bounds.Width-160,floor-40,bounds);
        Square.Model.Place(bounds.Left+bounds.Width-80,floor-40,bounds);
        void PlaceDog()
        {ResetPosition();body.Place(startX,floor-BodyHeight,bounds);StepPetGravity(.016);}
        PlaceDog();
        if(CanPlayToy(yarn)||CanPlayToy(mouse)||CanPlayTeaser(tree)||!CanPlayToy(bell))throw new Exception("Dog toy identity filter failed.");

        var requests=0;
        InteractionRequested+=_=>requests++;
        yarn.Model.Kick(230,0);mouse.Model.Kick(-230,0);
        stimulusElapsed=7;AllowCursorAttraction=true;body.Action=BodyAction.Idle;
        TickToyAttention(.016);
        if(requests!=0)throw new Exception("Fast feline toys attracted the dog.");
        yarn.Model.Place(startX+80,floor-40,bounds);mouse.Model.Place(startX+110,floor-40,bounds);
        foreach(var rejected in new[]{yarn,mouse})
        {
            PlaceDog();requestedToy=rejected;SetAction(BodyAction.PlayToy);
            if(playTarget==rejected||teaserTarget is not null||AttentionTarget==ToyId(rejected))throw new Exception("Dog selected a requested feline toy.");
        }
        teaserTarget=tree;BeginSequence(BodyAction.PlayToy);
        if(teaserTarget is not null||AttentionTarget==tree.Item.Id.ToString())throw new Exception("Dog selected the cat-tree teaser.");

        PlaceDog();requestedToy=bell;SetAction(BodyAction.PlayToy);
        if(playTarget!=bell||teaserTarget is not null||AttentionTarget!=ToyId(bell))throw new Exception("Dog could not select its bell ball.");
        var contacts=GenericPlayContactCount;var felineContacts=TeaserContactCount;
        var elapsed=Stopwatch.StartNew();
        for(var i=0;i<900&&GenericPlayContactCount==contacts;i++)
        {
            if(elapsed.Elapsed>TimeSpan.FromSeconds(3))throw new Exception("Dog play smoke exceeded its time budget.");
            poseAction=null;teaserPawTarget=null;
            TickSequence(.016);StepPetGravity(.016);ApplyPose(i*.016);
            if(poseAction==BodyAction.BatToy||teaserPawTarget is not null)throw new Exception("Dog used the feline contact rig.");
            bell.Step(.016);
        }
        if(GenericPlayContactCount<=contacts||Math.Abs(bell.Model.VelocityX)<50||bell.Model.VelocityY>=0)
            throw new Exception($"Dog made no physical play contact: phase={sequence?.Phase}, dog={body.X},{body.Y}, ball={bell.Model.X},{bell.Model.Y}, navigation={MovementPhase}");
        if(TeaserContactCount!=felineContacts)throw new Exception("Dog play changed the feline teaser contact count.");
    }
}
