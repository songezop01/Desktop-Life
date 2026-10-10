using DesktopLife.Core;

namespace DesktopLife.App;

public partial class PetWindow
{
    // Each window owns its ledger. Appearance buckets keep a diagnostic identity
    // swap, or a primary-profile swap, from lending one resident's contact to another.
    internal void AdvanceCompanionWithTeaserEvidence(HomeostasisSession life, TimeSpan elapsed, bool present)
    {
        var sampledAction = PhysiologicalAction;
        var contactDrivenTeaser = teaserTarget is not null && sampledAction == BodyAction.PlayToy;
        ObservedPlaySettlement.Advance(life, teaserPlayExposure, appearance, elapsed, sampledAction, present, contactDrivenTeaser);
    }
}
