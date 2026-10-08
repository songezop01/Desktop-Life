# Desktop Life V0.8.1 — Verification & Dual Presence

V0.8.1 把驗證工作移出 Codex 等待迴圈，並讓栗子與女孩可以共享房間、家具和玩具，同時保留各自的生活狀態。

## 驗證 harness

入口是 `scripts/verify.ps1`，另有可雙擊的 `Quick Verification.cmd` 與 `Full Verification.cmd`。

- Quick：Release build、全部單元測試、四人格（Explorer／Social／Playful／Relaxed）四小時邏輯決策、六週不同互動歷史、角色能力、save compatibility、Dual Presence model tests。
- Standard：Quick 加隔離 WPF smoke 與 10 分鐘短 stress。
- Full：Standard 加至少 7200 秒原生 WPF soak，可用 `-SoakSeconds 14400` 或 `28800` 延長。

Standard／Full 建立獨立 worker 後立即返回；不需要 Codex、GPT 或使用者保持視窗開啟。worker 使用隔離存檔，成功或失敗會更新固定入口 `artifacts/verification/latest/verification-summary.md` 和 `.json`。完整 raw telemetry 留在該次 run 目錄，不會預設讀入模型。只有發生例外、卡住、導航失敗、存檔錯誤或資源趨勢異常時，才建立 `failures/<timestamp>-<type>/failure-summary.json` 與少量 log tail。失敗資料不含私人桌面截圖、鍵盤內容或桌面檔案清單。

摘要只使用 PASS／REVIEW／FAIL／RUNNING／NOT RUN。`scripts/analyze-soak.py` 會計算暖機後斜率、rolling median、配置速率、handle 和視窗趨勢；它不宣稱「證明沒有 memory leak」。

本次執行紀錄中 Quick 已完成：Release build PASS、316/316 tests PASS、Identity PASS、Longitudinal PASS、Dual Presence PASS。WPF smoke 已以隔離資料通過一次；Standard worker 的實際狀態以 `artifacts/verification/latest/` 為準，不需要 Codex 輪詢。Full soak 未在本次開發中等待或假稱完成。

## Dual Presence

設定提供「只顯示栗子」、「只顯示女孩」、「兩個都顯示」。`PresenceMode` 只控制顯示與 runtime 是否運作，不改寫角色外觀 enum。

Shared World：房間幾何、家具、玩具、顯示器座標、音效設定、桌面圖示安全策略和共享作品畫布。

Per Character：`CharacterKind`、位置、`PetState`／需求、原始 `PersonalityProfile`、`PersonalityAdaptation`、Bond、Attention、`BehaviorSequence`、`HomeRoutine`、transition habits、能力邊界、短期 interest 和睡眠／互動狀態。家具 occupancy 是 runtime-only，重啟後重新計算，不寫入存檔。

兩個角色使用同一套能力與決策管線，但不共用飢餓、能量、Bond、人格、行為序列或照顧結果。角色靠近時只有輕量 `ObserveOther`、`AvoidOverlap` 或 `BriefApproach`；本版沒有角色 Bond、對話、共同玩具 choreography、嫉妒或故事線。

## Save migration

schema v4 讀取後升級至 v5。active 角色的完整歷史、房間、作品、人格和關係保留；另一角色使用產品預設 profile，不複製舊角色的 hunger、personality、Bond 或 transition habits。兩個位置會保存；共享房間仍位於 root learning state。舊檔案和三代備份維持原子保存與未來版本拒絕覆寫行為。

## Care target and limits

控制台顯示「目前照顧對象：栗子／女孩」，直接點擊角色會自動切換目標。女孩的梳毛按鈕會停用／顯示不支援，不播放貓梳毛表現；栗子維持餵食、摸頭、陪玩、梳理、休息。貓仍禁止人類文字與女孩塗鴉，女孩仍可畫模板塗鴉與寫便條。

## Files

主要新增 `scripts/verify.ps1`、`docs/VERIFICATION_WORKFLOW.md`、`src/DesktopLife.Core/CharacterProfile.cs`、`Household.cs`、`src/DesktopLife.App/CharacterRuntime.cs`、`MainWindow.Presence.cs`、`MainWindow.DualDiagnostics.cs`、`PetWindow.Presence.cs`，以及 Dual Presence／四人格／六週模擬測試。產品版本與單檔發行腳本已切換為 0.8.1；尚未自動安裝、merge、force push 或上傳 GitHub。
