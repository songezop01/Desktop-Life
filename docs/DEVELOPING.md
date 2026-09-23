# 開發與請教問題

Windows 11 x64，安裝 `global.json` 指定的 .NET 10 SDK，於專案根目錄執行：

```powershell
./scripts/build.ps1
./scripts/smoke.ps1
./scripts/publish.ps1
./scripts/install.ps1 -Launch
```

建置優先使用 `tools/dotnet/dotnet.exe`，沒有本機工具時使用 PATH 的 `dotnet`。首次建置需要從 NuGet 還原套件。WPF smoke 需要已登入的互動式 Windows 桌面；CI 僅建置與執行單元測試。

- `src/DesktopLife.Core`：需求、性格、存檔、重力、平台導航、音效包與座標模型。
- `src/DesktopLife.App`：WPF 控制台與透明視窗、向量角色、原生視窗定位、音效播放。
- `src/DesktopLife.Windows`：Windows 桌面圖示與環境互動。
- `tests/DesktopLife.Tests`：核心與 IPC 回歸測試。
- `DesktopLife.ExperimentalConnectome`、ConnectomeTool 與舊研究文件：歷史程式，相容測試仍保留，發行的桌寵不採用這些模型。

執行 `DesktopLife.exe --smoke-test` 使用獨立暫存資料，不讀寫真實角色或移動桌面圖示。真實圖示互動必須使用者自行啟用，備份、恢復與正常退出保存不可省略。`--shutdown` 請求正在執行的程式安全保存與退出，請勿以強制終止作為更新流程。

分享問題時請附版本、重現步驟與錯誤訊息；不要上傳個人 `organism.json`、桌面圖示備份、完整 AppData 或帳號憑證。`tools/`、`artifacts/`、下載資料與個人備份都不屬於原始碼。

角色、家具及程式合成音效位於原始碼；附圖只用於風格溝通，沒有作為可散布素材加入。尚未指定專案開源授權；公開可供閱讀與討論不代表另行授予所有商業使用權。第三方套件及歷史資料依各自授權。
