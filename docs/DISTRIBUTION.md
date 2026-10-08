# Windows 獨立程式

Desktop Life 0.11 使用 Windows x64 的單一 EXE，內含 .NET 執行環境。雙擊 `DesktopLife.exe` 或桌面「Desktop Life 桌面寵物」捷徑即可執行，不需要開啟 Codex、CMD、PowerShell 或另外安裝 .NET。

開發者執行 `scripts/publish.ps1` 會建立新的 `artifacts/standalone/0.11.0-日期時間/DesktopLife.exe` 與 ZIP。每次發行使用不同資料夾，不覆蓋正在執行的版本。既有使用者的 0.11 升級使用 `scripts/upgrade-011.ps1 -VerificationDirectory <同份來源的 Full 執行目錄>`，依序核對測試與來源、封裝檢查、安全退出、資料備份及捷徑更新。`scripts/install.ps1` 是底層檔案安裝工具，不能替代這些升級步驟。

寵物記憶、房間與桌面圖示備份仍保存在 `%LOCALAPPDATA%/DesktopLife`。0.11 的資料格式升至 schema7，加入一至三層小屋配置；升級保留角色歷史及家具 ID。回復 0.10.1 時必須配合當次升級前的 schema6 備份，請使用 `artifacts/recovery/0.11.0-<時間>/Restore previous Desktop Life.cmd`，不能只替換 EXE。

新版重複啟動會開啟現有控制台。控制台的關閉偏好可即時選擇「進入托盤」或「結束程式」。維護工具可執行 `DesktopLife.exe --shutdown`，透過限目前 Windows 使用者的本機通道要求程式保存、恢復圖示並結束；成功回傳 0，不能安全結束時回傳 2，不會強制終止程式。

目前為本機製作、未經數位簽章的版本。若搬到另一台電腦，Windows 可能顯示發行者未驗證；EXE 的 SHA-256 記錄在同資料夾的 `release.json`。尚未加入自動更新或開機自動啟動。

## 維護注意

從封裝宿主執行的工具與獨立程式可能看見不同的 AppData 檔案，詳見 [Microsoft MSIX 資料虛擬化說明](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)。不能因路徑字串相同就覆蓋資料；以獨立程式實際使用的資料為準。

一般備份與匯入請使用控制台。`scripts/import-profile.ps1` 僅供已明確選定來源的舊版維護移轉：在宿主外執行、取得資料鎖、確認沒有未恢復的圖示交易，並先保留目的端備份。不要把舊的虛擬副本直接覆蓋目前存檔。
