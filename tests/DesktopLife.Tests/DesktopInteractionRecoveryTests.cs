using DesktopLife.Core;
using Xunit;
namespace DesktopLife.Tests;
public class DesktopInteractionRecoveryTests
{
    [Theory]
    [InlineData(0,0)][InlineData(960,0)][InlineData(0,760)][InlineData(960,760)]
    public void PetCanReachAndPushAllFourCornerToysInward(double x,double y)
    {
        var bounds=new BodyBounds(0,0,1000,800);var pet=new DesktopBody();pet.Reset(bounds);var toy=new InteractiveToy(x,y);
        var approach=ObjectContact.Approach(x,y,InteractiveToy.Size,bounds);
        for(var i=0;i<300&&!ObjectContact.Reached(pet.X,pet.Y,approach);i++)pet.MoveToward(approach.X,approach.Y,.1,bounds,100);
        Assert.True(ObjectContact.Reached(pet.X,pet.Y,approach));
        toy.Kick(approach.PushX*180,approach.PushY*180);
        for(var i=0;i<10;i++)toy.Step(.1,bounds);
        Assert.InRange(toy.X,5,955);Assert.InRange(toy.Y,5,760);
    }
    private sealed class FakeDesktop : IDesktopIconBackend
    {
        public DesktopIconLayout Layout=new(5,1000,800,0,0,[new("a.lnk",10,20,true),new("folder",200,20,false)]);
        public bool FailMove;
        public DesktopIconLayout Read()=>Layout;
        public void SetArrangeFlags(uint flags)=>Layout=Layout with{Flags=flags};
        public void Move(string id,int x,int y)
        {
            if(FailMove)throw new IOException("simulated shell failure");
            Layout=Layout with{Icons=Layout.Icons.Select(i=>i.Id==id?i with{X=x,Y=y}:i).ToArray()};
        }
    }
    private static string Journal()=>Path.Combine(Path.GetTempPath(),"DesktopLifeTests",Guid.NewGuid().ToString("N"),"layout.json");
    [Fact] public void OriginalLayoutSurvivesRepeatedMovesAndNewSessionRecovery()
    {
        var desktop=new FakeDesktop();var file=Journal();var session=new DesktopIconSession(desktop,file);
        session.MoveShortcut("a.lnk",70,80);session.MoveShortcut("a.lnk",90,100);Assert.True(File.Exists(file));
        Assert.Equal(0u,desktop.Layout.Flags);Assert.Equal(90,desktop.Layout.Icons[0].X);
        Assert.Equal(2,new DesktopIconSession(desktop,file).Restore());Assert.Equal(10,desktop.Layout.Icons[0].X);Assert.Equal(20,desktop.Layout.Icons[0].Y);Assert.Equal(5u,desktop.Layout.Flags);Assert.True(File.Exists(file));
        Assert.Equal(0,new DesktopIconSession(desktop,file).Restore(pendingOnly:true));
    }
    [Fact] public void FailedMutationKeepsRecoveryBackupAndFoldersCannotBeMoved()
    {
        var desktop=new FakeDesktop();var file=Journal();var session=new DesktopIconSession(desktop,file);
        Assert.Throws<InvalidOperationException>(()=>session.MoveShortcut("folder",10,10));Assert.False(session.HasBackup);
        desktop.FailMove=true;Assert.Throws<IOException>(()=>session.MoveShortcut("a.lnk",70,80));Assert.True(session.HasBackup);
        Assert.Throws<IOException>(()=>session.Restore());Assert.True(session.HasBackup);Assert.Equal(5u,desktop.Layout.Flags);
        desktop.FailMove=false;session.Restore();Assert.True(session.HasBackup);
    }
    [Fact] public void RestoreDoesNotConfuseRenamedOrAddedIcons()
    {
        var desktop=new FakeDesktop();var session=new DesktopIconSession(desktop,Journal());session.MoveShortcut("a.lnk",70,80);
        desktop.Layout=desktop.Layout with{Icons=[new("renamed.lnk",70,80,true),new("folder",200,20,false),new("new.lnk",400,400,true)]};
        Assert.Equal(1,session.Restore());Assert.Equal(70,desktop.Layout.Icons[0].X);Assert.Equal(400,desktop.Layout.Icons[2].X);
    }
    [Fact] public void MonitorChangeRetainsBackupWithoutApplyingOldCoordinates()
    {
        var desktop=new FakeDesktop();var session=new DesktopIconSession(desktop,Journal());session.MoveShortcut("a.lnk",70,80);
        desktop.Layout=desktop.Layout with{OriginX=-1000};Assert.Throws<InvalidOperationException>(()=>session.Restore());Assert.True(session.HasBackup);
    }
    [Fact] public void PermanentBackupCanBeReusedAndExplicitlyUpdated()
    {
        var desktop=new FakeDesktop();var file=Journal();var session=new DesktopIconSession(desktop,file);
        session.UpdateBackup();desktop.Move("a.lnk",80,90);session.Restore();Assert.Equal(10,desktop.Layout.Icons[0].X);
        desktop.Move("a.lnk",90,100);session.UpdateBackup();desktop.Move("a.lnk",20,30);
        Assert.Equal(0,new DesktopIconSession(desktop,file).Restore(pendingOnly:true));
        new DesktopIconSession(desktop,file).Restore();Assert.Equal(90,desktop.Layout.Icons[0].X);Assert.True(File.Exists(file+".previous"));
    }
    [Fact] public void ArchivedOldBackupIsAvailableAgainWithoutStartupRearranging()
    {
        var desktop=new FakeDesktop();var file=Journal();Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file+".restored",System.Text.Json.JsonSerializer.Serialize(desktop.Layout));desktop.Move("a.lnk",90,100);
        var session=new DesktopIconSession(desktop,file);Assert.Equal(0,session.Restore(pendingOnly:true));Assert.True(session.HasBackup);
        session.Restore();Assert.Equal(10,desktop.Layout.Icons[0].X);
    }
    [Fact] public void OccupiedPositionDoesNotChangeLayoutOrCreateBackup()
    {
        var desktop=new FakeDesktop();var session=new DesktopIconSession(desktop,Journal());
        Assert.Throws<InvalidOperationException>(()=>session.MoveShortcut("a.lnk",210,30));Assert.False(session.HasBackup);Assert.Equal(5u,desktop.Layout.Flags);
    }
}
