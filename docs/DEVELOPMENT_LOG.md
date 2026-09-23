# 開發紀錄

## 2026-09-11 — Milestone 1
- 建立 Solution、Core、WPF、xUnit、設定與輪替紀錄、開發腳本。
- Release Build：0 warnings/errors。Tests：8/8 passed。
- 程式 smoke：WPF loaded、Dispatcher responsive、exit 0。
- 人工視覺驗收未完成，未使用螢幕截圖。

## 2026-09-11 — Milestone 2
- 透明無邊框角色、幾何 hitbox、Idle/Walk/Wander/Sit/Sleep、控制台操作、主螢幕工作區限制。
- Release Build：0 warnings/errors。Tests：15/15 passed。
- 自動 WPF smoke：控制台/角色 loaded、透明角落無 hit、角色有 hit、五種姿態、hide/show/reset、exit 0。
- 這些是程式內 smoke，尚非跨應用滑鼠穿透或人工視覺驗收。
- 暫停於此交付有限階段；後續從 M2 人工驗收與 M3 感測開始。

## 2026-09-11 — Milestone 3
- 使用者確認前次交付並授權繼續。建立獨立 Windows 感測層和 immutable EnvironmentState。
- CPU/GPU/RAM、磁碟讀寫、網路上下傳、session idle、滑鼠取樣位移共 9 項；背景每秒取樣、控制台顯示最新值及 N/A 原因。
- Release build 0 warnings/errors；30/30 unit tests passed。
- 原生感測整合 --require-all：9/9 available、exit 0。WPF smoke 新鮮 CPU/RAM snapshot 與既有角色檢查全部通過、exit 0。
- 六次樣本平均 16.38 ms、最高 28.27 ms。屬短測，不是長期效能驗收。
- 不記錄感測歷史；不讀網路內容/鍵盤內容。溫度未啟用。下一階段 M4 Homeostasis。

## 2026-09-11 — Milestone 4
- PetState 八項 0–100、生理規則、環境供能與可解釋狀態面板。缺失/過期感測不冒充零活動。
- 一秒生理更新；活躍供能、閒置需求、Sleep/Sit 恢復；環境更新完全不接行為學習。
- 本機 pet-state.json / schema 1、總執行秒數、原子保存與前一份 backup；每分鐘/關閉/suspend checkpoint，resume/啟動離線 cap 8 小時。
- 正式 instance.lock 避免多開互相覆寫；smoke 改為獨立 temp 資料目錄。
- Build 0 warnings/errors；56/56 tests passed。原生感測 9/9；WPF smoke 真實供能使 PetState 改變、存讀一致、正常 exit 0。
- 實際 sleep/hibernate 與混合 DPI 仍未人工驗收。下一階段 M5 Reward & Learning。

## M5 — Reward & Learning
- 角色 routed hitbox 左 +3／右 +1／中 -2；5 秒指數 eligibility trace、三層偏好、慢速學習、clamp/decay、本機 learning.json。
- Build 0 warnings/errors；70 tests；9/9 sensors；WPF 透明角落不獎勵、角色 routed click 學習通過。
- 使用者授權後續驗證通過自動前進；接續 M6。

## M6 — Utility Brain
- 統一 IBrainController / BrainOutput、PersonalityProfile、IPetAction / IAnimationController、softmax 動作取樣、偏好影響分數、低能量安全覆寫與休息 hysteresis。
- Build 0 warnings/errors；76 tests；原生感測及 WPF smoke 通過。接續 M7 FlyInspiredBrain。

## M7 — FlyInspiredBrain
- 16 維 sensory encoder、256 KC-inspired units / top-16 sparse activation、7 MBON-inspired outputs、dopamine reward plasticity、Hybrid 可調混合。神經權重保存於 learning.json。
- Build 0 warnings/errors；81 tests；sensor/WPF smoke 通過。不是生物精確果蠅模型。接續 M8 Affordance / Actions。

## M8 — Affordance / Extended Actions
- 集中游標/虛擬圖示/玩具 affordances，17 action IDs，追逐/迴避/角落休息/探索/虛擬推動與玩具 overlay。真 Explorer 圖示不動。創作 action 到 M9 才開放。
- Build clean；85 tests；sensor/WPF smoke 通過。接續 M9。

## M9 — Creative Behavior
- 七種程序 doodle、Template+PetState 短句、click-through overlay、Clear/Hide/Keep、最多32作品、未保留作品2分鐘到期，作品可保存。
- Build clean；93 tests；sensor/WPF smoke 通過。接續 M10 ExperimentalConnectome framework。

## M10 — ExperimentalConnectome Framework
- 獨立 graph/source/loader、JSON parser、SHA256 keyed gzip cache、type/region/ID/hops/threshold extractor、budget、傳播 adapter/decoder。
- Build clean；98 tests。Synthetic fixture 測試 parser/cache/subgraph/propagation，未宣稱為真實資料。接續 M11 真實資料 adapter。

## M11 — FlyWire v783 Adapter
- 核對官方 Zenodo schema/CC-BY4 connectivity，固定 annotations commit，Python/Arrow 分批串流處理，只輸出摘要。
- 2 adapter tests 通過（方向、跨 neuropil 權重合併、side/type 篩選、mask、拒絕錯誤格式）。原始檔下載中；接續 M12 真實 subset 載入驗證。

## M12 — Real FlyWire Subgraph
- 官方852022274-byte feather MD5驗證；本機串流16847997 rows，2.32秒輸出1.18MB左側MB subset。
- 2794 neurons（2580KC/48MBON/166DAN）、9836 edges、7344候選plastic edges。真實topology活動傳播成功；平均tick0.37ms，最大4.14ms（100 ticks短測）。
- CLI實體載入/輸出、Build/tests及WPF smoke通過。接續M13。

## M13 — Connectome Plasticity
- 5秒trace保存connectome activity；reward只更新PlasticEdgeMask且target drive相符的effective weight，OriginalWeight保持不變。
- 103 tests pass；真實subset測試100次reward使Explore 0.20567→0.23177，2683 edges changed；非plastic edges不變。cache hit載入79ms。接續M14。

## M14 — Brain Comparison
- Debug顯示Utility/Fly/Real/Hybrid七種drive比較、personality、分數拆解、brain stats與memory。Real不可用/超budget明確fallback。
- Build/tests clean；真實dataset WPF routed reward同時改Fly與Connectome權重通過。接續M15統一存檔。

## M15 — Persistence
- organism.json schema2原子checkpoint包含PetState/personality/三層learning/Fly weights/connectome hash+deltas/settings/artworks/runtime/reward counts。
- 明確遷移旧split schema1，保留舊檔；未知schema拒絕。106 tests+WPF存讀驗證通過。接續M16。

## M16 — Fullscreen / Tray / UX
- Tray Show/Hide/Pause/Resume/Debug/Clear/Reset/BrainMode/Exit；最小化收至tray；borderless-monitor fullscreen detector、caption最大化不隱藏。
- 110 tests與sensor/WPF smoke通過。實際exclusive game/多螢幕仍屬人工驗收限制。接續M17效能量測。

## M17 — Performance Pass
- 除錯面板最多1Hz、隱藏後停止文字/狀態重繪；靜止角色重用transform，避免每frame配置。
- 45秒Hybrid/真實subset/感測開啟、面板隱藏短測：整機CPU平均0.353%（16 logical cores）、單核心等效5.64%、peak working set242.3MB；private memory152→106MB，未見持續上升，但不是長期leak證明。
- 110 tests clean；進入M18最終驗證。

## M18 — V0.1 Automated Validation
- 116 .NET tests + 2 adapter tests通過；Chase獎勵/懲罰、Draw/Create及Sit/Rest方向性驗證通過。
- 真實FlyWire七個drive全數reward↑/punishment↓，原始資料不改。文件已重整；製作self-contained win-x64 local release與最後packaged smoke。
- 人工DPI/exclusive-game/sleep/cross-app mouse、長期培養仍未驗收，不宣稱全部產品接受測試完成。

- M18 final：self-contained win-x64 packaged smoke exit0，source build 0 warnings/errors，116+2 tests通過。已保留 CODEX_CONTEXT 與驗證限制。
