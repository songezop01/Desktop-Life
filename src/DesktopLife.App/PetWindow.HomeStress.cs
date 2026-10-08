using DesktopLife.Core;
namespace DesktopLife.App;
public partial class PetWindow
{
    private bool stressEnabled;
    public int StuckSequences {get;private set;}
    public Dictionary<BehaviorPhase,long> StressPhases {get;}=[];
    private BehaviorSequence? reportedStuck;
    public void ConfigureHomeStress()
    {
        var b=Bounds();EnableStressRecording();Routine=new(70);
        foreach(var kind in new[]{FurnitureKind.Yarn,FurnitureKind.BellBall,FurnitureKind.ToyMouse})AddRoomItem(new(Guid.NewGuid(),kind,b.Left+100+ExtraToys.Count*90,b.Top+b.Height-40));
        var kinds=new[]{FurnitureKind.Box,FurnitureKind.PetBed,FurnitureKind.Scratcher,FurnitureKind.CatTree,FurnitureKind.Bookshelf,FurnitureKind.Desk,FurnitureKind.HumanBed,FurnitureKind.Chair,FurnitureKind.DiningTable,FurnitureKind.Computer,FurnitureKind.DrawingBook,FurnitureKind.LegoBox,FurnitureKind.Sofa,FurnitureKind.CatBowl};
        for(var i=0;i<21;i++)
        {
            var kind=kinds[i%kinds.Length];var size=RoomWindow.Size(kind);
            AddRoomItem(new(Guid.NewGuid(),kind,b.Left+30+(i%7)*(b.Width-220)/7,b.Top+b.Height-size.Height-(i/7)*125));
        }
        ResetPosition();body.Place(b.Left+200,b.Top+b.Height-BodyHeight,b);
    }
    public void EnableStressRecording()
    {
        stressEnabled=true;
        // Real cursor hover must not create lazy tooltip HWNDs in isolated
        // resource measurements. Routed interaction remains covered by smoke.
        System.Windows.Controls.ToolTipService.SetIsEnabled(Character,false);
        foreach(var toy in AllToys)toy.DisableDiagnosticTooltip();
    }
    private void RecordStressFrame()
    {
        if(!stressEnabled||sequence is not {} s)return;
        RecordApproachTimeout(s);
        StressPhases[s.Phase]=StressPhases.GetValueOrDefault(s.Phase)+1;
        var budget=s.Phase==BehaviorPhase.Approach?s.ApproachBudgetSeconds+3:35;
        if(s.Phase!=BehaviorPhase.Sleep&&s.PhaseAge>budget&&reportedStuck!=s){StuckSequences++;reportedStuck=s;}
    }
    public void StressSceneStep(int second)
    {
        if(second%29==28&&Furniture.Count>0)
        {
            var f=Furniture[(second/29)%Furniture.Count];SetRoomEditing(true);
            f.Relocate(f.Item.X+(second%2==0?22:-22),f.Item.Y);SetRoomEditing(false);
        }
        if(second%97==96&&Furniture.Count>0)
        {
            var f=Furniture.FirstOrDefault(w=>w.Item.Id==homeTarget?.Id)??Furniture[0];var item=f.Item;
            Furniture.Remove(f);f.Close();restPreference.Prune(Furniture.Select(w=>w.Item.Id));
            AddRoomItem(item with{Id=Guid.NewGuid()});RoomChanged?.Invoke();
        }
        if(second%41==40)Ball.Model.Kick(second%2==0?230:-230,-190);
    }
}
