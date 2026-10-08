# Desktop Life 0.10.1 升級與復原

本版只在同一份來源通過 **Full** 驗收後發布及升級。Full 包含核心、分析器、隔離安裝故障、WPF、十分鐘三角色及至少兩小時原生長測；不以 Quick 或 Standard 取代。

2026-10-03 已安裝並啟動 `0.10.1-20261003-201245`，原生核對 PASS。安裝紀錄：`artifacts/installation/0.10.1-20261003-201244/upgrade-record.json`；核對：同目錄 `native-verification.json`。正式執行檔：`%USERPROFILE%\AppData\Local\Programs\DesktopLife\0.10.1-20261003-201245\DesktopLife.exe`，SHA256 `421F171CF5754E8D4A4BDC2F4F1E189C384AAEBC91A46A81A3C922B087F6FE84`。

升級前完整資料備份：`%USERPROFILE%\AppData\Local\DesktopLife\upgrade-backups\before-0.10.1-20261003-201355`。一鍵復原：`artifacts/recovery/0.10.1-20261003-201356/Restore previous Desktop Life.cmd`。17個備份檔案雜湊一致，三角色、歷史與10件房間物件保留，桌面及開始選單捷徑均指向新版；上一版執行檔保留。

使用 `scripts/upgrade-010.ps1 -VerificationDirectory <Full 執行目錄>`。版本門檻為 0.10.1，腳本會核對完整來源清單及 SHA256、封裝 smoke、安全退出及資料鎖，再完成原生完整備份、獨立版本安裝與捷徑更新。封裝 Codex 的同名 AppData 可能是舊快取，因此升級及復原入口一律委派獨立 Windows PowerShell；不能直接從封裝 shell 覆蓋正式資料。

保存格式仍是 v6。備份包含正式資料及附屬檔案的雜湊，排除正在持有的 instance.lock 及既有備份樹。升級紀錄與一鍵復原入口保存在 `artifacts/recovery/0.10.1-<時間>`，上一版執行檔保留。

復原入口使用當次紀錄、先安全結束、保存目前完整資料，然後還原升級前資料與其匹配的上一版。中途失敗自動補償原本作用中的資料、安裝紀錄和兩個捷徑；補償成功及鎖探測通過才可恢復原程式。若持續磁碟／權限／檔案鎖阻礙補償，會明確標為 COMPENSATION_FAILED 並留下驗證過的 original-data 快照，不能把它當作已完成的復原。

已提交的新安裝或可能已啟動的新程式不會自動降回舊版，避免在新版開始寫入後套用過期快照。啟動失敗會保留有效安裝與捷徑並記錄狀態；前置備份失敗且資料未修改時，驗證原執行檔、安裝紀錄與資料鎖後恢復原程式。

故障注入測試全部使用隔離資料、原始檔案內容、真實 WScript 捷徑及正式安裝共用函式。它們驗證補償邏輯，並不表示已對使用者正式資料做過降版演練。實際安裝版本、備份及執行檔雜湊以當次安裝紀錄及原生核對結果為準。
