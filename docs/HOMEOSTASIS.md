# Homeostasis — M4

這是桌面生命的工程模型，並非生物學模擬。所有八項值皆為 0–100；Energy/Mood/Curiosity/Excitement 表示狀態，Hunger/Boredom/Loneliness/Fatigue 越高表示需求越高。不存在死亡旗標或永久不可恢復狀態。

## 環境供能

CPU/GPU/RAM 使用率除以 100；磁碟各以 20 MiB/s、網路各以 2 MiB/s、滑鼠以 400 px/s 正規化至 0–1。
權重：CPU .30、GPU .20、RAM .10、磁碟讀寫各 .075、網路上下傳各 .05、滑鼠 .15。僅計算有效感測並依可用權重重新正規化。
presence = exp(-idleSeconds / 300)。有 activity 與 presence 時，level = .8 × activity + .2 × presence；僅有一項時使用該項。
這些尺度是 V0.1 初始工程選擇，不會鼓勵使用者刻意提高負載，也不代表硬體健康指標。

三秒以上過期/全部缺失的感測會凍結本次生理影響。缺失 idle 不改 Loneliness；部分數值缺失會在面板註明。供能值不是 dopamine 或行為獎勵，程式不存取任何偏好或神經權重。

## 每分鐘變化

L 為供能 0–1，P 為 presence（缺失時除 Loneliness 外以下項目使用 0）。

| 數值 | 每分鐘增量或目標 |
|---|---|
| Energy | -.12 + .55 L；Sit/Sleep 額外 +.10 |
| Hunger | +.12 - .70 L |
| Mood | -.08 + .30 L + .05 P |
| Boredom | +.10 - .25 L - .10 P |
| Loneliness | +.10 - .25 P；P 缺失時凍結 |
| Excitement | 以 .04/min 指數靠近 10 + 80 L |
| Fatigue | +.06 + .06 L；Sit/Sleep 額外 -.30 |
| Curiosity | 以 .03/min 指數靠近 40 + .25 Mood + .10 Boredom |

每次完成後 clamp。1 Hz UI timer 依 Stopwatch 經過時間換算，不依 frame 數更新。超過十秒的中斷走離線計算，不把剛恢復的 CPU 活動乘上整段時間。正式行為選擇由 Utility/Fly/Real/Hybrid 提供分數，生理安全層可覆寫為休息。

## 離線與保存

一次離線最多計算 8 小時：每小時 Hunger/Loneliness +2、Boredom +1、Energy -1、Mood -.75、Fatigue -1，Excitement 緩慢靠近 10，Curiosity 不變。負的離線時差按 0 處理。
目前保存 `%LOCALAPPDATA%/DesktopLife/organism.json`（schema 2），其中 Pet 欄位包含 schema 1 的八項狀態、LastSaveTime、TotalRuntimeSeconds。正常執行時間不計入推估離線段。
每 60 秒、正常關閉、Windows suspend 與 session ending 保存；resume 更新後再保存。重啟載入後立即 checkpoint，避免重複計算同一離線區間。突然斷電可能失去最近約 60 秒變化。
寫入 temp 並 flush，再原子替換正式檔並保留 `.bak`。壞資料/缺欄位/未知版本拒絕載入，不無聲重設；將來版本需明確 migration。保存失敗顯示錯誤且取消一般關閉。單一資料目錄以檔案鎖防止同時寫入。
M15 已整合全部學習與神經權重，與生理一起原子保存；舊 pet-state.json 只作 migration 輸入。感測快照歷史不保存。

## 驗證範圍

自動測試涵蓋供能/閒置方向、休息恢復、未知感測、不同 timestep、長期 clamp、極低狀態恢復、離線 cap、JSON roundtrip、backup、拒絕損壞/未知版本，以及 checkpoint 不重複套用。
WPF smoke 使用獨立 temp 角色，驗證真實取樣推動狀態與存讀一致；不改正式角色。Windows 真正 sleep/hibernate 事件、顯示器縮放的人工驗收仍未完成。

