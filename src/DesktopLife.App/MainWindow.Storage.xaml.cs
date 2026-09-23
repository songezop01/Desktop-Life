using System.IO;
using System.Windows;
using DesktopLife.Core;
namespace DesktopLife.App;
public partial class MainWindow
{
    public void ShowStorageNotice(string? message)=>StorageStatus.Text=message??"存檔格式 v2；自動保留三代備份，主檔損毀時找回有效備份。";
    private void ExportPetSave(object sender,RoutedEventArgs e)
    {
        var picker=new Microsoft.Win32.SaveFileDialog{Title="匯出寵物與房間存檔",Filter="Desktop Life 存檔 (*.json)|*.json",FileName="DesktopLife-"+DateTime.Now.ToString("yyyyMMdd")+".json"};
        if(picker.ShowDialog(this)!=true)return;
        try{if(!SavePetState())return;organismStore.Export(picker.FileName);StorageStatus.Text="已匯出寵物、房間與設定。桌面圖示備份保持獨立。";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException){StorageStatus.Text="匯出失敗："+ex.Message;}
    }
    private void ImportPetSave(object sender,RoutedEventArgs e)
    {
        var picker=new Microsoft.Win32.OpenFileDialog{Title="選擇要匯入的寵物存檔",Filter="Desktop Life 存檔 (*.json)|*.json"};
        if(picker.ShowDialog(this)!=true)return;
        try{organismStore.StageImport(picker.FileName);StorageStatus.Text="存檔驗證成功，下次完整結束並重新開啟程式時套用。原存檔會保留備份，桌面圖示排列不受影響。";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException or ArgumentException){StorageStatus.Text="無法匯入，原存檔未變動："+ex.Message;}
    }
}
