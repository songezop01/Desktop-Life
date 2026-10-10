using DesktopLife.Core;

namespace DesktopLife.App;

public partial class MainWindow
{
    private readonly HashSet<Guid> interactiveTeasers=[];
    private void InitializeDirectCare()
    {
        Pet.FurnitureAdded+=AttachTeaserInteraction;
        foreach(var f in Pet.Furniture)AttachTeaserInteraction(f);
        foreach(var kind in Enum.GetValues<PetAppearance>())
        {
            var resident=kind;var actor=CharacterWindow(resident);
            actor.DirectCareContactRequested=contact=>
            {
                if(aiPaused||saveClosing||suspendedAt is not null||runtimeDiagnosticSceneSuspended
                    ||!PresencePolicy.Includes(CurrentPresence,resident)||!actor.IsVisible)return false;
                var learning=resident==settings.PetAppearance?Learning:RuntimeFor(resident)!.Learning;
                var life=LifeFor(resident);
                var result=actor.ApplyDirectContact(contact,life.State,learning.State.Companion);
                if(!result.Accepted)return false;
                life.ApplyCare(result.Pet);learning.State.Companion=result.Companion;
                learning.Reward(RewardButton.Left,lifeClock.Elapsed.TotalSeconds,settings);
                actor.EmotionalState=result.Pet;actor.Bond=result.Companion.Bond;
                if(contact.Kind==CareKind.Pet)learning.State.Adaptation.Observe(AdaptiveTrait.Social,DateTimeOffset.UtcNow);
                CareStatus.Text=contact.Kind==CareKind.Pet?"牠感覺到這次溫柔的撫摸。":"這次梳理讓牠很舒服。";
                if(resident==PetAppearance.Girl)CareStatus.Text=contact.Kind==CareKind.Pet?"她感覺到這次溫柔的陪伴。":"她的頭髮整理好了。";
                ShowCompanion();ShowHomeostasis();ShowLearning(force:true);QueuePetSave();return true;
            };
        }
    }
    private void AttachTeaserInteraction(RoomWindow surface)
    {
        if(surface.Teaser is null||!interactiveTeasers.Add(surface.Item.Id))return;
        surface.TeaserTouched+=item=>
        {
            if(aiPaused||saveClosing||runtimeDiagnosticSceneSuspended||Pet.EditingRoom)return;
            foreach(var kind in new[]{PetAppearance.Cat,PetAppearance.BorderCollie}.Where(k=>PresencePolicy.Includes(CurrentPresence,k))
                .OrderBy(k=>
                {
                    var actor=CharacterWindow(k);var size=CharacterGeometry.For(k,Pet.House?.SceneScale??1);
                    var dx=actor.Position.X+size.Width/2-item.TeaserPosition.X;var dy=actor.Position.Y+size.Height*.75-item.TeaserPosition.Y;
                    return dx*dx+dy*dy;
                }))
                if(CharacterWindow(kind).TryStartTeaserActivity(item))break;
        };
        surface.Closed+=(_,_)=>interactiveTeasers.Remove(surface.Item.Id);
    }
}
