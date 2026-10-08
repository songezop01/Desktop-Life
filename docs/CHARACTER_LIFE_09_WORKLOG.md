# Desktop Life V0.9 開發紀錄

## 2026-09-29 20:42：依使用者即刻安裝要求，V0.9 已安裝

使用者明確要求「馬上給我安裝0.9」，取代等待 Standard gate 的安排。上一輪阻擋源於 Python unittest 預設 stderr 在 Windows PowerShell Stop 模式被當成錯誤（Analyzer 顯示 `....`）；已將 runner 輸出改到 stdout。沒有改寫舊測試結果，也未聲稱 Standard／Full 已通過。

已發布、備份、安裝並啟動：`%USERPROFILE%\AppData\Local\Programs\DesktopLife\0.9.0-20260929-204150\DesktopLife.exe`，FileVersion **0.9.0.0**，SHA256 **0BC622794F56E09859F728EDE93A691E29E7A3BA2F6B30DCF40CAED9AF795B5E**。

已核對獨立程序 **PID 12024** 的 ExecutablePath 與桌面捷徑均指向此版本。
升級前備份：`%USERPROFILE%\AppData\Local\DesktopLife\upgrade-backups\before-0.9-immediate-20260929-204242`。關鍵備份檔與安裝 EXE 雜湊驗證通過。結果記錄 `artifacts/installation/immediate-09.json`。原 0.8 安裝目錄保留。新版完整 Standard／Full 尚未確認通過，不得據此宣稱長時間穩定性已驗收。

## 2026-09-29 19:39：導航修正與重新啟動安裝驗證

使用者明確要求「解決問題，成功後安裝新版本」。舊程序 `0.9-20260929-005556` 已 NEEDS_REVIEW 且 Installed=false；其 Standard 完整 608.55 秒、0 exception、0 stuck，但 17 NavigationFailures，原因未分項，不可回推為已恢復。

本輪查出下落起始判定允許距離離台點 14px，而離台點僅在平台外 10px，因此仍站在平台上就進入 Airborne，可能隨即被判 unexpected landing。改為距離離台點 <=2px，補上左右邊界與實際 GravityBody 下落測試。

同時：家具幾何變化清空舊路徑後由重力落地、重新規劃，不再無條件算作執行失敗；不可達目標記為 UnreachableTargets，避免靜止拒絕路徑再被 stall detector 重複算失敗。真實 no-progress／flight-timeout／unexpected-landing 保留 NavigationFailures 並記原因。壓力報告現在合計兩角色失敗，失敗原因寫入摘要包。分析器仍對任何真實導航失敗回 REVIEW，安裝仍只接受 PASS。

驗證：Quick `20260929-193603-c7f2eb79`，352/352 .NET tests PASS；4/4 Python 分析器安裝判定回歸 PASS。原生 60 秒隔離回歸 `artifacts/verification/0.9-regression/navigation-fix/analysis.json`：實際 60.8405 秒、Exceptions=0、StuckSequences=0、NavigationFailures=0、UnreachableTargets=1、Conclusion=PASS。這不是十分鐘 Standard 或兩小時 Full 完成證據。

新背景完成程序：**PID 7180**，資料 **`artifacts/installation/0.9-20260929-193920`**。已一次確認程序存在、status=VERIFYING、Installed=false、Standard summary 建立。不要輪詢或再啟動重複程序。接續先讀此 status 與 latest summary；Standard／正式 EXE 短測全通過才備份安裝，Full 安裝後獨立啟動。此時仍未確認安裝完成。

## 2026-09-29：角色活動、家具與安裝接續

最新授權：使用者要求「完成後安裝到我電腦」。已完成新家具與角色活動第一版，詳見 `docs/UPDATE_09.md` 的 A–S 說明與限制；本檔下方 9/28「尚未完成」清單是歷史狀態。

- 新增八種家具向量、美術與中文選單；共用 RoomActivity／BehaviorSequence 流程、女孩用餐／頭髮整理／繪畫／寫字／電腦／積木／閱讀、女孩床上躺姿與非床休息姿勢。
- 角色限制、家具优先序、附近椅子、可達性、共享預約區域、獨立習慣接入。畫作延後到活動收尾才產生。
- 版本 metadata 0.9.0；schema 保持 5，新家具 enum 僅追加；新增個別習慣保存測試。
- 最新 Quick `20260929-005435-65677b53`：Build/Tests/Identity/Longitudinal/DualPresence PASS，347/347；整體 REVIEW 因 Quick 不含原生 WPF/Stress。
- 本輪兩次隔離 `scripts/smoke.ps1` 通過（第二次含 SleepIdentity、各活動姿勢與共享 zone）。已實際查看 `.../DesktopLifeSmoke/1c6134c52f0d4ae695493bca32a297cf/girl-life-Feed.png` 的餐盤、坐姿與人體手部。
- 壓力測試改為 Both，加入新家具及 InvalidFurnitureInteractions／SecondaryStuckSequences；尚無本輪十分鐘完成結果。

### 已啟動獨立完成程序，勿重複啟動或陪跑

本機時間 **2026-09-29 00:55:56**，PID **8272**。

資料：`artifacts/installation/0.9-20260929-005556`。

一次啟動確認：程序存在、`status.json`=VERIFYING、Installed=false，Standard summary 已建立。首個 launch 因 PowerShell 5 不支援 `||` 被拒；已改為 `-or` 並成功啟動。

`scripts/complete-09.ps1` 依序：Standard 必須 PASS → 確認來源雜湊沒變 → 發布單檔 EXE → 正式 EXE 隔離短測 → 正常關閉舊版 → 升級前備份與關鍵檔雜湊比對 → 安裝與獨立啟動 → 核對 EXE 雜湊 → 啟動獨立 Full（不等待）。任何 REVIEW／FAIL 均不安裝。沒有跳過導航警告，也沒有把 10 分鐘當 2 小時。

下一次接續先讀安裝 `status.json` 與 `artifacts/verification/latest/verification-summary.md`。若 NEEDS_REVIEW，讀該 summary 指到的 failure package；不要預設掃 raw。尚未確認安裝成功，不得向使用者宣稱已安裝。

## 2026-09-28：基線檢查與角色家具邊界

規格：`%USERPROFILE%\.codex\attachments\048f84a9-3b1c-4e3b-9f2e-a43381fae98b\貼上的文字.txt`。
工作分支仍為 `codex/individuality-08`；保留所有既有未提交 V0.8／V0.8.1 變更。未安裝、未上傳、未變更真實存檔。

### 已確認的基線問題

- 舊 Standard：`artifacts/verification/runs/20260928-003001-5f9e56a5`，ShortStress FAIL。
- `stress.ps1` 將絕對 OutputDirectory 再接上專案根目錄，Windows PowerShell 結果收集失敗。現已分開處理絕對／相對路徑，並複製獨立執行檔快照，避免長測鎖住開發版輸出。
- 舊隔離程序 22352 的資料位於 `%TEMP%/DesktopLifeSmoke/e1e9d0f6f6014632920eec09dc588282`。進度最後 Second=450、WallSeconds=460.43，app.log 記錄 Normal shutdown，沒有 stress-report.json。因此 **不能** 將舊測試算作完成 600 秒，也不能斷言路徑修正已解決提前退出；需獨立重跑驗證。
- 基線 Quick `20260928-183110-2b83a841`：Build/Tests/Identity/Longitudinal/DualPresence PASS；REVIEW 僅因 Quick 不含 WPF 與壓力測試。此輪 Quick 在後續睡眠流程修改前建置，後續改動另行驗證。

### 本輪修改

- 新增集中式 `FurnitureCompatibility.CanUse`／`AvailableUses`，先過角色限制再讓既有偏好与導航挑選。
- 女孩不再把貓床、貓跳台、紙箱、貓抓板、書架頂當成休息候選；原有女孩觀察家具能力保留。
- `HomeRoutine` 可用家具用途與實際 Home/Sleep 目標共用相同限制。
- 既有 `BehaviorSequence` 增加角色參數。女孩睡眠跳過 Curl、Knead、LickPaw、WashFace；貓原流程保留。這只完成身份邊界，尚非完整人類床鋪動畫。
- 重設角色位置時釋放家具占用，避免重設後另一角色無法使用家具。
- 新增角色限制與女孩完整睡醒序列測試。

### 明確尚未完成

V0.9 仍在開發，不能標示完成：新增人類家具、美術、用餐／整理頭髮／書桌／電腦／積木／閱讀流程、分區占用、角色照顧 UI、完整 WPF 情境與最終 Full 均待實作。未新增家具 enum，未改存檔 schema。M1 亦仍需配合新家具擴充規則與優先序。

### 接續方式

本輪最終 Quick：`20260928-183458-f78a6a78`，323/323 測試通過、建置與模型檢查通過；未包含 WPF 驗收。驗證腳本另通過 PowerShell 語法解析。

Standard 已獨立啟動：PID **16496**，本機 2026-09-28 18:36；隔離執行資料 `artifacts/verification/runs/20260928-183625-2acf9e95`。已一次確認 PID 存在、launcher.json 與 run summary 已建立，未輪詢結果。首次 sandbox 啟動 WMI 被拒，改以獲准的升權工具啟動成功。此輪尚無 Standard 完成結論。

背景 worker 現接收啟動端解析的 git/python 路徑，避免桌面程序 PATH 不同而無法產生版本／分析報告；JSON 摘要保留來源檔 SHA256，Markdown 顯示測試數量。

先讀 `artifacts/verification/latest/verification-summary.md`。Standard 在獨立背景程序執行時不要陪跑／輪詢。僅 FAIL／REVIEW 時讀所指失敗包。若仍提前退出，查隔離日誌與程序結束來源；勿把 460 秒算成完成。驗證腳本改善後，依規格完成 M1–M11，再啟動最終 Full，不等待兩小時。
