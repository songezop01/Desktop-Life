# Desktop Life V0.8 — Individuality Update

功能實作與 Release 編譯完成；完整驗收未完成。使用者在 M5 後明確要求「跳過測試，繼續」，因此後段未執行自動測試、WPF smoke、多人格／多週模擬或第二次兩小時長測。先前通過的結果只適用於各自當時的程式狀態。

## A：實際修改與新增檔案

分支 `codex/individuality-08`，基於 V0.7 `8b45bfec23905480b72a754933775b37bb0a2672`。未合併、推送或改寫歷史。逐檔清單：

- `README.md`
- `docs/CHARACTER_ART_PIPELINE.md`
- `docs/CHARACTER_CAPABILITIES.md`
- `docs/CURRENT_PRODUCT_DIRECTION.md`
- `docs/INDIVIDUALITY_08_WORKLOG.md`
- `docs/UPDATE_08.md`
- `scripts/analyze-soak.py`
- `scripts/install.ps1`
- `scripts/publish.ps1`
- `scripts/stress.ps1`
- `src/DesktopLife.App/App.xaml.cs`
- `src/DesktopLife.App/ArtWindow.cs`
- `src/DesktopLife.App/DesktopLife.App.csproj`
- `src/DesktopLife.App/MainWindow.Brain.xaml.cs`
- `src/DesktopLife.App/MainWindow.Companion.xaml.cs`
- `src/DesktopLife.App/MainWindow.HomeStress.cs`
- `src/DesktopLife.App/MainWindow.Identity.cs`
- `src/DesktopLife.App/MainWindow.IdentityDiagnostics.cs`
- `src/DesktopLife.App/MainWindow.Learning.xaml.cs`
- `src/DesktopLife.App/MainWindow.Variation.xaml.cs`
- `src/DesktopLife.App/MainWindow.xaml`
- `src/DesktopLife.App/PetWindow.Behavior.cs`
- `src/DesktopLife.App/PetWindow.Home.cs`
- `src/DesktopLife.App/PetWindow.xaml.cs`
- `src/DesktopLife.Core/BehaviorTransitionPreference.cs`
- `src/DesktopLife.Core/Brain.cs`
- `src/DesktopLife.Core/CharacterCapability.cs`
- `src/DesktopLife.Core/Companion.cs`
- `src/DesktopLife.Core/CreativeBehavior.cs`
- `src/DesktopLife.Core/GenerativeOutput.cs`
- `src/DesktopLife.Core/GirlDoodleGenerator.cs`
- `src/DesktopLife.Core/HomeRoutine.cs`
- `src/DesktopLife.Core/Learning.cs`
- `src/DesktopLife.Core/MilestoneMemory.cs`
- `src/DesktopLife.Core/OrganismStore.cs`
- `src/DesktopLife.Core/PersonalityAdaptation.cs`
- `src/DesktopLife.Core/RestSpots.cs`
- `tests/DesktopLife.Tests/CharacterCapabilityTests.cs`
- `tests/DesktopLife.Tests/GirlDoodleTests.cs`
- `tests/DesktopLife.Tests/IndividualityIntegrationTests.cs`
- `tests/DesktopLife.Tests/LocationHabitTests.cs`
- `tests/DesktopLife.Tests/OrganismPersistenceTests.cs`
- `tests/DesktopLife.Tests/PersonalityAdaptationTests.cs`
- `tests/DesktopLife.Tests/SaveRecoveryTests.cs`
- `tests/DesktopLife.Tests/TransitionHabitTests.cs`

## B–C：角色能力架構與差異

CharacterCapability 在 ActionSelection 評分前限制意圖，PetWindow 的執行與 ArtWindow 的創作入口再次檢查。Cat 不再產生 WriteNote、DrawDoodle 或人類語句；高額舊創作偏好不能繞過限制。貓透過既有姿態與聲音表達，控制台可提供第三人稱系統描述。

Girl 保留本機短句、便條、模板塗鴉。抓板／箱內貓動作使用安全備援，舔爪時不顯示貓式理毛，貓爪覆層與呼嚕受角色限制。切換外觀清除序列、排隊意圖、氣泡和未完成的轉換上下文。沒有另建 ECS 或女孩骨架。詳見 CHARACTER_CAPABILITIES.md。

## D–F：女孩塗鴉、模板、歷史相容

GirlDoodleGenerator 先依既有 BehaviorVariation 的 Creativity／Playfulness／Calmness 與最近輸出選模板，再加入小幅旋轉（約 ±2.9 度）、尺寸（0.88–0.94）、位置偏移、輕微線條抖動及有限裝飾。輸出沿用受限 GeneratedDrawing，不再混合任意 spiral／polygon。正式新便條使用 ComposableText.GenerateGirl 的短句模板；畫畫間隔至少 45 秒、便條至少 90 秒。

16 種模板：愛心、星星、太陽、雲朵、花朵、笑臉、貓臉、小屋、月亮、樹、小人、兔子、蝴蝶、杯子、彩虹、小花邊。重複模板會降權，不以每張完全不同為目標。

CreativeWork 的 DoodleTemplateId 是 optional。舊 GeneratedDrawing 直接顯示原線條；沒有 Drawing 的舊作品仍用 Legacy Pattern renderer。舊貓咪作品不刪除、不重生成。原 ProceduralDrawing 與歷史測試保留。

## G–H：緩慢人格調整

原始 PersonalityProfile 始終獨立保存；有效人格為原始值加上有界 offset，再限制至 0–1。可調整好奇、愛玩、親人、獨立四項，其餘特質不重建。

每個特質每分鐘最多一份 evidence，最多 40；超過 8 的最低證據門檻才可能緩慢變化。Evidence 以七日尺度衰減；單一特質最大變化率為每個活躍日 0.006，offset 安全界限 ±0.12，目前只累積正向偏移。長離線不增加偏移，只衰減 evidence。事件或每分鐘更新有效人格，沒有每幀重新計算。

來源：接受陪玩／摸摸、新家具真正使用、偵測到使用者在場時的安靜陪伴。一般 UI 沒有人格點數或滑桿。這些參數尚未經最終多週體驗校準。

## I：活動轉換習慣

最多 32 組 Previous／Next pair，Weight 0–1、Count 上限 10000。成功完成才學習，玩球需實際接觸；中斷或切換角色清除未完成的上下文。Pair 最多每分鐘增加一次，14 日尺度衰減，超過 10 分鐘不接成同一段習慣。最近三個活動有降權，避免反覆循環。

原 ActionSelection／HomeRoutine 使用小幅 bias；Wake 分支、探索後觀察、玩後抓板／休息等由人格和近期活動共同加權。高疲勞仍優先休息，習慣不是強制腳本。

## J–K：關係、人格與家具

Bond 只表達與使用者的關係，沒有直接寫入 Social。獨立但高 Bond 的角色仍可偏向遠一點的地點。RestSpotPreference 沿用既有 GUID、熟悉度、近期使用與新鮮感，再結合慵懶的距離成本、親人的靠近傾向及獨立的較遠地點傾向。所有選擇都先考慮可達性，再做 soft weighting。

Schema 升為 v4 是為保護新身份資料不被舊寫入器丟棄；v2/v3 既有 Pet、Personality、Bond、Memories、Room、家具 GUID、Artworks、LocationHabits、Learning、Settings 沿用。新增 adaptation／transition／milestone 區塊獨立處理 optional 損壞；重大外層 JSON 仍走原備份復原。使用 v4 後不可直接降版，需使用升級前備份。

## L：真實里程碑與描述

六類：箱內睡著、實際使用抓板、自主選家具入睡、家具熟悉度達標、高 Bond 且靠近使用者的自主蹭蹭、重複完成的活動銜接。事件發生才評估，六小時冷卻，每類最多一次；「第一次記錄」指此追蹤功能的觀測，不編造升級前歷史。

回憶總數仍為 30，其中最多保留 6 則里程碑。正式產品一般照顧摘要每 30 分鐘最多一則，描述真實照顧行動，不把要求休息寫成已經睡著。系統「牠最近的樣子」由累積偏移或真實家具習慣提供；進階區可看 Base／Effective／Offset、轉換、家具及原因。

## M–O：已完成與跳過的檢查

- 原 270 項保留，新增 32 項；M5 當時 302 tests PASS。舊 schema 測試更新版本預期，沒有刪除案例。
- M2 當時完整 WPF smoke PASS：含 Cat 創作／說話阻擋、Girl 表現、切換外觀與既有房間回歸。
- 16 模板產出 100／150／200% render sheets，已檢視 100／150% 圖片；幾何 bounds、固定 seed、小幅 variation 與 legacy roundtrip 曾通過單元測試。
- 使用者要求跳過後，M6／UI／後續修正僅編譯，沒有重新跑上述驗收；因此不能聲稱最終版本 302 tests PASS。

## P：V0.8 前兩小時 Stability Gate

| 指標 | 實測 |
|---|---|
| 時間 | 7311.38 秒，完成要求的 7200 個一秒迴圈 |
| 場景 | 24 個房間物件（21 家具＋3 額外玩具），總計 5 玩具 |
| 例外／phase stalls | 0／0 |
| 導航失敗／恢復 | 157；路線規劃 692 |
| 整機平均 CPU | 0.883% |
| Working set | 起始 247.08、結束 304.09、峰值 331.42 MiB |
| Private memory | 起始 139.38、結束 168.25、峰值 198.39 MiB |
| GC 累積配置 | 11267.93 MiB，約 92.47 MiB/min |
| GC 0／1／2 | 963／222／22 |
| Process handles | 1575–1646，結束 1597 |
| GDI／USER | 365–373／173–184 |
| Native windows | 80→83 後穩定 |
| WPF／Room／Toy windows | 29／21／5，全程固定 |

Private memory 十分鐘中位數中段約 190 MiB，後段降到 169–171 MiB；handle／window 未呈持續線性上升，所以通過前置 gate。這些觀察不能證明不存在洩漏。原始資料：artifacts/verification/0.8/pre-gate，分析工具 scripts/analyze-soak.py。

## Q–S：最終長測與模擬

V0.8 後兩小時 soak、四人格多小時 Identity Simulation、不同陪伴歷史的多週 Longitudinal Simulation：依使用者最新要求跳過，沒有結果或虛構統計。先前單一 adaptation 上限／重啟案例，不等於完整四組多週比較。不能據此宣稱生活節奏已達到「可辨識但不極端」或沒有穩定性退步。

## T：Release

最終 solution Release 編譯完成，0 warnings／0 errors，沒有執行 test。自含 win-x64 單檔 EXE 由 scripts/publish.ps1 輸出；建置 `0.8.0-20260927-233148`，SHA256 `BAD8773F9C20BA8DDF9BE6F7C9AA0E5366C3070DB9AEF2812249C7FBA44FD8B6`。未安裝、未啟動新版去讀取真實存檔，未推送 GitHub。獨立 EXE 的最終執行驗收也依要求跳過。

## U–V：限制與替代設計

1. 後半段未驗收，新回憶整合、角色切換收尾與 UI 可能仍有執行期問題。
2. 四人格和多週模擬未執行，soft bias 的體感差異、平衡與變化速度需要後續確認。
3. 只對四項特質提供慢速正向學習，沒有負面創傷或全人格訓練。
4. 女孩沿用原姿勢與通用備援，沒有專屬抓板、舔爪或新骨架。
5. 保留 24 物件限制；家具仍為單向平台，導航失敗／恢復存在。沒有新增導航引擎。
6. v4 是保護新資料的相容性決策，不只是配合版本號；舊存檔可升級，新存檔不能由舊版覆寫。
7. 原先要求逐階段全面驗收，後續被使用者「跳過測試」更新；本報告區分實作完成與未驗收項目。

## W：V0.9 建議

先補本次跳過的四人格／多週統計與最後兩小時 soak，再依 CHARACTER_ART_PIPELINE 製作有授權的分層角色素材。美術優先處理女孩手部接觸、貓的箱內照顧與轉身細節；UX 優先處理不打擾的生活描述、房間導航失敗提示與混合 DPI 實機體驗。
