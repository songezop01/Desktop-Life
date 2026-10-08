# Desktop Life 0.10 開發方式

目前為 Windows 11 x64、本機 WPF 陪伴程式。安裝 `global.json` 指定的 .NET 10 SDK；建置優先使用 `tools/dotnet/dotnet.exe`，沒有本機工具時使用 PATH 的 `dotnet`。首次建置需要從 NuGet 還原套件。

## 建置與驗證

在專案根目錄使用適合當次工作的入口：

```powershell
./scripts/build.ps1
./scripts/smoke.ps1
./scripts/verify.ps1 -Mode Standard
```

`build.ps1` 建置並執行核心測試；`smoke.ps1` 檢查隔離 WPF 視窗；`verify.ps1` 是統一的 Quick／Standard／Full 流程。WPF smoke 需要已登入的互動式 Windows 桌面，CI 僅建置與執行單元測試。Standard 含真實時間壓力測試，Full 再包含原生長測；未執行或仍在執行的階段保持待驗收。當次結果見 `artifacts/verification/latest/verification-summary.md`。

0.10 同場景前後採樣資料見 `artifacts/verification/0.10/PERFORMANCE.md`。UI 量測是派送延遲，不是實際畫面幀率；CPU、配置量、工作集與私有記憶體需分開呈現。短測不能證明長期沒有洩漏，不把舊版或尚未完成的量測當作最終結果。

## 發行、升級與復原

發行包是含 .NET 執行環境的 Windows x64 單一 EXE。`scripts/publish.ps1` 產生新版本資料夾與 ZIP；`scripts/install.ps1` 是底層複製與捷徑工具，不負責完整升級驗證和資料備份。

0.10 更新使用安全升級入口，傳入已通過的 Standard 驗證目錄：

```powershell
./scripts/upgrade-010.ps1 -VerificationDirectory '<Standard 已通過的驗證目錄>'
```

此入口核對驗證與來源雜湊、產生發行包並執行封裝隔離 smoke，之後安全結束舊版、完整備份角色資料與舊安裝資訊，再安裝至獨立版本目錄。不要用強制終止取代 `DesktopLife.exe --shutdown`；它會要求正在執行的程式保存、恢復圖示與退出，不能安全結束時應中止升級。

升級紀錄與一鍵復原 CMD 位於 `artifacts/recovery/0.10-<時間>`。需要還原時也可指定當次紀錄：

```powershell
./scripts/rollback-010.ps1 -RecordPath '<upgrade-record.json>'
```

實際安裝、備份及復原驗證以當次升級紀錄為準，腳本存在本身不代表安裝完成。

## 程式與素材入口

- `src/DesktopLife.Core`：需求、人格、三角色能力與存檔、重力、平台導航、家具相容性及活動模型。
- `src/DesktopLife.App`：WPF 主控台與透明視窗、角色 renderer、原生視窗定位、音效與各角色 runtime。
- `src/DesktopLife.App/CompanionSpriteVisual.cs`：透明圖集依 alpha 連通區域擷取、凍結圖片快取、支撐基準與透明邊界命中。
- `src/DesktopLife.App/Assets/Companions`：四張生成 PNG，作為 WPF Resource 封裝；`SOURCES.md` 記錄來源、雜湊與原始區域，`prompts.json` 記錄生成提示。
- `src/DesktopLife.Windows`：Windows 桌面圖示與本機閒置／游標互動。
- `tests/DesktopLife.Tests`：核心、IPC、存檔遷移、三角色隔離及導航回歸。
- `DesktopLife.ExperimentalConnectome`、ConnectomeTool 與舊研究文件：歷史程式，相容測試仍保留，發行桌寵不採用這些模型。

插畫在執行時先以寬 1024 像素解碼；每張擷取 8 個主要姿態，女孩有基本與活動兩套。姿態使用共同縮放與 y=144 支撐基準，命中依當前 alpha 判斷。貓精確接觸仍保留程序式骨架，家具與部分道具仍為向量。這不是 Live2D、Spine、完整分層動畫或人體 IK。後續規格見 `docs/ART_DIRECTION_010.md` 與 `docs/UPGRADE_ROADMAP.md`。

## 存檔與測試資料

0.10 的 `OrganismSnapshot` 使用 schema v6；讀取 v2／v3／v4／v5 時完整保留原角色歷史並遷移，較新未知版本拒絕。第三角色獨立保存，舊 0.9 的讀取門檻會拒絕 v6，避免忽略新欄位後丟失其歷史。回到舊版必須使用升級前原始格式的完整備份，不可修改 schema 標記假裝相容。

Windows Store 版 Codex 的子程序可能將 AppData 讀取導向封裝快取，而獨立 WMI 程式使用原生資料。升級與復原入口透過 `native-profile-context.ps1` 委派原生 PowerShell 執行，並等待結果；不要以封裝子程序的舊 `organism.json` 判斷目前程式的存檔。測試已確認原生工作程序讀到實際 schema6 與 170 次獎勵，而封裝快取仍是較舊 schema2。

`DesktopLife.exe --smoke-test` 使用獨立暫存資料，不讀寫真實角色或移動真實桌面圖示。真實圖示互動預設關閉，啟用前的備份、恢復與正常退出保存不可省略。測試通過不代表混合 DPI、多螢幕、自然養成或兩小時真實長測全部完成。

分享問題時請附版本、重現步驟與錯誤訊息；不要上傳個人 `organism.json`、桌面圖示備份、完整 AppData 或帳號憑證。`tools/`、`artifacts/`、下載資料與個人備份都不屬於原始碼。

原始參考附圖只用於風格溝通；production 角色使用另外生成的 PNG。素材來源見 `SOURCES.md`。尚未指定專案開源授權；公開可供閱讀與討論不代表另行授予所有商業使用權。第三方套件及歷史資料依各自授權。
