# Architecture — V0.1

## 模組與資料流

DesktopLife.Core：PetState/Homeostasis、Learning/EligibilityTrace、Personality、IBrainController、Utility/FlyInspired、IPetAction、ActionSelection、Affordance、Doodle/Note、OrganismStore。
DesktopLife.Windows：Win32 / PDH 感測器、游標、fullscreen detector 與 overlay click-through；不讀鍵盤內容或私人文件。
DesktopLife.ExperimentalConnectome：IConnectomeSource / Loader、Graph / Neuron / Edge / Metadata / Subgraph、Extractor、gzip Cache、SimulationBudget、SimulationAdapter、BehaviorDecoder。
DesktopLife.App：WPF 控制台、PetWindow、ArtWindow、托盤與生命週期。Brain/learning/tray/connectome 檔案為 MainWindow 的 partial modules。
DesktopLife.ConnectomeTool：本機 subset/統計/驗證 CLI，不操作 UI。
tests：純核心 unit tests、真 Windows sensor smoke；Python adapter 使用小型合成檔驗證官方格式映射。

    Windows sensors → EnvironmentState → EnvironmentFeeding → HomeostasisSession → PetState
    PetState + environment + personality → IBrainController → BrainOutput → ActionSelection
    AffordanceProvider + learned preferences + safety → IPetAction → IAnimationController → PetWindow / ArtWindow

    Pet hitbox → RewardEvent → 5-second EligibilityTrace
               → ActionPreference / CategoryPreference / ContextAssociation
               → Fly plastic weights / Connectome learned deltas

Environment Feeding 不呼叫 Reward。BrainOutput 不直接呼叫 OS；渲染層只執行 enum 白名單動作。

## 更新頻率與安全

感測在单一背景 worker，1 Hz；生理 1 Hz；Brain/trace 4 Hz；connectome 每次評估 3 個內部 rate steps。動畫約 30 Hz；debug 最高 1 Hz 且隱藏時停止重繪。
每個 action 約 6 秒一段；最多 30 秒重新啟動以恢復 lifecycle。低能量或高疲勞覆寫為 Sleep；Sleep 有恢復 hysteresis。
位置 clamp 到主螢幕 work area；full-screen geometry checks 在独立 1 Hz timer，隱藏時清除 trace。Pause 停止 AI/動畫/獎勵，生理與感測继续。
溫度不可靠時不讀取；資料不完整時 N/A。Connectome 超出節點/edge/memory/tick 預算會拒絕或 fallback。

## 持久化

OrganismSnapshot schema 2：PetSnapshot、LearningState、Personality、Settings；包括 FlyWeights、ConnectomeFingerprint/Deltas、作品、runtime/feedback counts。
單一原子 checkpoint，先寫 temp + flush，再 replace + backup。旧 schema-1 split files 僅作首次 migration 輸入，保留原檔。
原始 connectome 絕不因學習改写；delta 只套用相同 source SHA256。最近 trace 與 raw sensor snapshots 不保存。
settings.json 是使用者可調設定；完整 snapshot 另保存一份設定，當設定檔缺失時可恢復。
instance.lock 防止兩份程式同時覆寫；診斷 smoke 使用隔離 temp 目錄。

## 已知範圍

生物簡化見 NEURAL_DESIGN.md，資料來源見 CONNECTOME_DATA.md，驗證邊界見 VALIDATION.md。

