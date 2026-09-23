using System.Windows;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    private Soundscape? audio;
    private SoundPackLibrary? soundLibrary;
    private bool soundListReady;
    private sealed record SoundChoice(string Id,string Name,string Details);
    private void InitializeAudio(string root)
    {
        AudioVolume.Value=settings.AudioVolume*100;CatVolume.Value=settings.CatVolume*100;ToyVolume.Value=settings.ToyVolume*100;AmbientVolume.Value=settings.AmbientVolume*100;MuteAudio.IsChecked=settings.MuteAudio;
        audio=new(root){Volume=settings.AudioVolume,CatVolume=settings.CatVolume,ToyVolume=settings.ToyVolume,AmbientVolume=settings.AmbientVolume,Muted=settings.MuteAudio||settings.QuietCompanion};
        soundLibrary=new(root);
        audio.ErrorChanged+=message=>SoundPackStatus.Text=message;
        if(settings.SoundPackPath is null)
            settings=settings with{SoundPackPath=System.IO.Directory.Exists(System.IO.Path.Combine(root,"audio-v1","custom"))?"legacy":"builtin"};
        ReloadSoundPacks();
        audio.SelectPack(settings.SoundPackPath);
        Pet.SoundRequested+=(sound,strength)=>audio.Play(sound,strength);
        Closed+=(_,_)=>audio.Dispose();
    }
    private void ChangeAudio(object sender,RoutedEventArgs e)
    {
        if(audio is null)return;
        settings=settings with{AudioVolume=AudioVolume.Value/100,MuteAudio=MuteAudio.IsChecked==true};settingsStore.Save(settings);
        audio.Volume=settings.AudioVolume;audio.CatVolume=settings.CatVolume;audio.ToyVolume=settings.ToyVolume;audio.Muted=settings.MuteAudio||settings.QuietCompanion;
        if(audio.Muted||audio.Volume==0)audio.Stop();
        audio.ApplyLevels();
    }
    private void PreviewCatSound(object sender,RoutedEventArgs e)
    {if(settings.PetAppearance==PetAppearance.Cat)audio?.Play(PetSound.Meow,1,true);else CareStatus.Text="女孩目前沒有角色音效；請先切換橘貓。";}
    private void PreviewBellSound(object sender,RoutedEventArgs e)=>audio?.Play(PetSound.Bell,1,true);
    private void ChangeAudioCategory(object sender,System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if(audio is null)return;
        settings=settings with{CatVolume=CatVolume.Value/100,ToyVolume=ToyVolume.Value/100,AmbientVolume=AmbientVolume.Value/100};settingsStore.Save(settings);
        audio.CatVolume=settings.CatVolume;audio.ToyVolume=settings.ToyVolume;audio.AmbientVolume=settings.AmbientVolume;
        audio.ApplyLevels();
    }
    private void ReloadSoundPacks()
    {
        soundListReady=false;
        var choices=new List<SoundChoice>{new("builtin","內建音效","程式產生的貓叫、呼嚕與鈴鐺音效。")};
        if(settings.SoundPackPath=="legacy")choices.Add(new("legacy","舊版自訂音效","沿用原 custom 資料夾；原始檔案不會刪除。"));
        choices.AddRange(soundLibrary!.List().Select(p=>new SoundChoice(p.Id,p.Name,$"作者：{p.Author} · 授權：{p.License}")));
        SoundPacks.ItemsSource=choices;SoundPacks.SelectedItem=choices.FirstOrDefault(p=>p.Id==settings.SoundPackPath)??choices[0];
        soundListReady=true;SelectSoundPack(this,new System.Windows.Controls.SelectionChangedEventArgs(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,Array.Empty<object>(),Array.Empty<object>()));
    }
    private void SelectSoundPack(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!soundListReady||audio is null||SoundPacks.SelectedItem is not SoundChoice choice)return;
        settings=settings with{SoundPackPath=choice.Id};settingsStore.Save(settings);audio.SelectPack(choice.Id);
        SoundPackStatus.Text=audio.Error??choice.Details;RemoveSoundPackButton.IsEnabled=choice.Id is not ("builtin" or "legacy");
    }
    private void ImportSoundPack(object sender,RoutedEventArgs e)
    {
        var picker=new Microsoft.Win32.OpenFolderDialog{Title="選取含 pack.json 與 WAV 的音效包資料夾"};
        if(picker.ShowDialog(this)!=true)return;
        try{var pack=soundLibrary!.Import(picker.FolderName);settings=settings with{SoundPackPath=pack.Id};ReloadSoundPacks();}
        catch(Exception ex) when(ex is System.IO.IOException or System.Text.Json.JsonException or UnauthorizedAccessException){SoundPackStatus.Text="音效包未匯入："+ex.Message;}
    }
    private void RemoveSoundPack(object sender,RoutedEventArgs e)
    {
        if(SoundPacks.SelectedItem is not SoundChoice choice||choice.Id is "builtin" or "legacy")return;
        try{audio!.SelectPack("builtin");soundLibrary!.Remove(choice.Id);settings=settings with{SoundPackPath="builtin"};ReloadSoundPacks();}
        catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException){SoundPackStatus.Text="移除失敗："+ex.Message;}
    }
    private void PreviewPurrSound(object sender,RoutedEventArgs e)=>audio?.Play(PetSound.Purr,1,true);
}
