using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLife.Core;
namespace DesktopLife.App;

public partial class MainWindow
{
    /// <summary>Visits every fixed house preset on connected displays in an isolated candidate process.</summary>
    public async Task SmokeDisplays(string root)
    {
        var originalSettings=settings;var originalDisplay=DisplayWorkspace.Active;
        var originalPause=aiPaused;var originalPresence=CurrentPresence;var originalFloors=Pet.House?.FloorCount??1;
        var originalPositions=characterWindows.Select(character=>character.Position).ToArray();
        var originalWindowVisible=IsVisible;var originalPriority=Priorities.SelectedItem;
        var inventory=DisplayWorkspace.Enumerate();var rows=new List<object>();
        try
        {
            FreezeRestartVerification();Hide();Priorities.SelectedItem=DisplayPriority.Highest;
            SetPaused(true);foreach(var character in characterWindows)character.SetSimulationEnabled(false);
            PresenceOptions.SelectedIndex=(int)PresenceMode.All;
            foreach(var display in inventory)
            {
                var previous=DisplayWorkspace.Active;var current=DisplayWorkspace.Select(display.Id,inventory);
                settings=settings with{ActiveDisplay=current.Id};
                RemapDisplayWorkspace(previous,current);RefreshDisplayList(inventory);
                for(var floors=1;floors<=HouseLayout.MaximumFloorCount;floors++)
                {
                    Pet.ConfigureHouse(floors,true);UpdatePetVisibility();
                    var house=Pet.House!;var bounds=current.Bounds;
                    for(var i=0;i<characterWindows.Length;i++)
                    {
                        var character=characterWindows[i];var floor=house.Floors[i%floors];character.ResetPosition();
                        var x=house.SafeFootX(floor.Index,floor.Left+floor.Width*(i+1)/4,character.Width/2);
                        character.RestorePosition(new(x-character.Width/2,floor.Y-character.Height));character.SetAction(BodyAction.Idle);
                    }
                    await Task.Delay(180);
                    var surfaces=new List<object>();var paintBounds=new List<object>();
                    void Check(Window surface,Point expected,string kind)
                    {
                        if(!surface.IsVisible)throw new Exception("Display diagnostic surface hidden: "+kind);
                        var native=DisplayWorkspace.NativeWindowBounds(surface);var point=DisplayWorkspace.RoomPosition(surface);
                        if(Math.Abs(point.X-expected.X)>1||Math.Abs(point.Y-expected.Y)>1)
                            throw new Exception($"{current.Label}: {kind} room coordinates drifted from native HWND.");
                        if(Math.Abs(native.Dpi/96d-current.Scale)>.01)
                            throw new Exception($"{current.Label}: {kind} has the wrong native DPI.");
                        if(Math.Abs(native.Pixels.Width-surface.Width*current.Scale)>3||Math.Abs(native.Pixels.Height-surface.Height*current.Scale)>3)
                            throw new Exception($"{current.Label}: {kind} physical size does not match room scale.");
                        if(native.Pixels.Left<current.PixelWorkArea.Left-3||native.Pixels.Top<current.PixelWorkArea.Top-3
                            ||native.Pixels.Left+native.Pixels.Width>current.PixelWorkArea.Left+current.PixelWorkArea.Width+3
                            ||native.Pixels.Top+native.Pixels.Height>current.PixelWorkArea.Top+current.PixelWorkArea.Height+3)
                            throw new Exception($"{current.Label}: {kind} extends outside the available desktop.");
                        surfaces.Add(new{Kind=kind,ExpectedRoomPosition=expected,ObservedRoomPosition=point,WpfLeft=surface.Left,WpfTop=surface.Top,NativePixels=native.Pixels,NativeDpi=native.Dpi});
                    }
                    for(var i=0;i<characterWindows.Length;i++)
                    {
                        var character=characterWindows[i];var expected=new Point(character.Position.X,character.Position.Y);
                        Check(character,expected,"Character "+i);character.UpdateLayout();
                        var width=Math.Max(1,(int)Math.Ceiling(character.Width*current.Scale));
                        var height=Math.Max(1,(int)Math.Ceiling(character.Height*current.Scale));
                        var image=new RenderTargetBitmap(width,height,96*current.Scale,96*current.Scale,PixelFormats.Pbgra32);
                        image.Render((Visual)character.Content);var pixels=new byte[width*height*4];image.CopyPixels(pixels,width*4,0);
                        int left=width,top=height,right=-1,bottom=-1;
                        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
                            if(pixels[(y*width+x)*4+3]>32){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
                        if(bottom<height-Math.Ceiling(4*current.Scale))throw new Exception("Scaled character artwork lost its feet anchor: "+i);
                        paintBounds.Add(new{Character=i,LocalPixelBounds=new{Left=left,Top=top,Right=right,Bottom=bottom},PixelWidth=width,PixelHeight=height,GroundAnchor=true});
                    }
                    foreach(var furniture in Pet.Furniture)
                    {
                        Check(furniture,new(furniture.Item.X,furniture.Item.Y),"Furniture "+furniture.Item.Kind);
                        foreach(var platform in furniture.Platforms)
                            if(platform.X<furniture.Item.X-2||platform.X+platform.Width>furniture.Item.X+furniture.Width+2
                                ||platform.Y<furniture.Item.Y-2||platform.Y>furniture.Item.Y+furniture.Height+2
                                ||platform.EndY<furniture.Item.Y-2||platform.EndY>furniture.Item.Y+furniture.Height+2)
                                throw new Exception($"{current.Label}: furniture platform drifted outside its illustration.");
                    }
                    // A new window must have correct geometry on its first DPI crossing too.
                    var fresh=new RoomWindow(new(Guid.NewGuid(),FurnitureKind.PetBed,bounds.Left+40,bounds.Top+bounds.Height-60));
                    try
                    {
                        fresh.SetSceneScale(house.SceneScale);fresh.Show();fresh.SetEditing(false);await Task.Delay(100);
                        Check(fresh,new(fresh.Item.X,fresh.Item.Y),"Fresh furniture");
                        if(fresh.Platforms.Any(p=>p.X<fresh.Item.X-2||p.X+p.Width>fresh.Item.X+fresh.Width+2||p.Y<fresh.Item.Y-2||p.Y>fresh.Item.Y+fresh.Height+2))
                            throw new Exception("Fresh furniture contact geometry uses WPF screen coordinates instead of room coordinates.");
                    }
                    finally{fresh.Close();}
                    var directInputs=await SmokeDisplayDirectInputs(root);
                    var imageName=$"display-{current.DeviceName.Replace("\\\\.\\","")}-{floors}-floors.png";
                    RenderHousePreview(Path.Combine(root,imageName));
                    rows.Add(new{current.Id,current.DeviceName,current.Label,current.Bounds,current.Scale,current.Adapter,current.PixelBounds,current.PixelWorkArea,current.Portrait,
                        Floors=floors,house.SceneScale,Surfaces=surfaces,CharacterPaintBounds=paintBounds,DirectInputs=directInputs,GeneratedPreview=imageName});
                }
            }
            File.WriteAllText(Path.Combine(root,"display-report.json"),JsonSerializer.Serialize(new
            {
                Succeeded=true,VisitedDisplays=rows,WorkspaceRemaps,
                Scope="Every fixed house preset on real connected monitors: native HWND/DPI/workarea, rendered feet, real hover/stroke, comb capture/contact and left-click food strips. No physical phone disconnect, rotation, sleep or OS layout change performed."
            },new JsonSerializerOptions{WriteIndented=true}));
        }
        finally
        {
            var previous=DisplayWorkspace.Active;var restored=DisplayWorkspace.Select(originalDisplay.Id);
            settings=originalSettings;RemapDisplayWorkspace(previous,restored);
            Pet.ConfigureHouse(originalFloors,true);RefreshDisplayList();
            PresenceOptions.SelectedIndex=(int)originalPresence;
            for(var i=0;i<characterWindows.Length;i++)
            {characterWindows[i].RestorePosition(originalPositions[i]);characterWindows[i].SetSimulationEnabled(true);}
            SetPaused(originalPause);
            Priorities.SelectedItem=originalPriority;if(originalWindowVisible)Show();
        }
    }
}
