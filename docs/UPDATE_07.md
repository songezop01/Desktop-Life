# Desktop Life V0.7 — Home Update

房間開始提供用途與生活偏好：紙箱用來調查、躲藏、探頭與休息；抓板用來抓抓與伸展；睡窩沿用原有睡眠鏈。全部在本機離線運作，不增加網路 AI、商店、照顧懲罰或新 renderer。

## A–B：修改範圍與家具語意

核心新增 FurnitureAffordance／FurnitureContext／FurnitureUse、LocationHabit 與安全 JSON converter、HomeRoutine。擴充既有 BehaviorSequence、RestSpotPreference、RoomNavigation、LearningState 與 OrganismStore。

App 新增 PetWindow.Home、HomeDiagnostics、HomeStress 與 MainWindow.HomeStress；擴充 RoomWindow、FelineVisual、PetWindow 的 Behavior／Navigation／Room／主體、MainWindow 的 Brain／Learning／Room／介面及啟動參數。另有家具中文標籤、版本／發行腳本、stress.ps1、新增測試與本文件。完整逐檔清單如下。

<details><summary>本次修改檔案</summary>

- `README.md`
- `docs/CHARACTER_ART_PIPELINE.md`
- `docs/CURRENT_PRODUCT_DIRECTION.md`
- `docs/HOME_07_WORKLOG.md`
- `docs/UPDATE_07.md`
- `scripts/install.ps1`
- `scripts/publish.ps1`
- `scripts/stress.ps1`
- `src/DesktopLife.App/App.xaml.cs`
- `src/DesktopLife.App/DesktopLife.App.csproj`
- `src/DesktopLife.App/FelineVisual.cs`
- `src/DesktopLife.App/MainWindow.Brain.xaml.cs`
- `src/DesktopLife.App/MainWindow.HomeStress.cs`
- `src/DesktopLife.App/MainWindow.Learning.xaml.cs`
- `src/DesktopLife.App/MainWindow.Room.xaml.cs`
- `src/DesktopLife.App/MainWindow.xaml`
- `src/DesktopLife.App/PetWindow.Behavior.cs`
- `src/DesktopLife.App/PetWindow.Home.cs`
- `src/DesktopLife.App/PetWindow.HomeDiagnostics.cs`
- `src/DesktopLife.App/PetWindow.HomeStress.cs`
- `src/DesktopLife.App/PetWindow.Navigation.cs`
- `src/DesktopLife.App/PetWindow.Room.cs`
- `src/DesktopLife.App/PetWindow.xaml.cs`
- `src/DesktopLife.App/RoomWindow.cs`
- `src/DesktopLife.App/UiText.cs`
- `src/DesktopLife.Core/BehaviorSequence.cs`
- `src/DesktopLife.Core/FurnitureAffordance.cs`
- `src/DesktopLife.Core/HomeRoutine.cs`
- `src/DesktopLife.Core/Learning.cs`
- `src/DesktopLife.Core/LocationHabit.cs`
- `src/DesktopLife.Core/OrganismStore.cs`
- `src/DesktopLife.Core/RestSpots.cs`
- `src/DesktopLife.Core/RoomNavigation.cs`
- `src/DesktopLife.Core/RoomPhysics.cs`
- `tests/DesktopLife.Tests/HomeAffordanceTests.cs`
- `tests/DesktopLife.Tests/HomeRoutineTests.cs`
- `tests/DesktopLife.Tests/HomeSequenceTests.cs`
- `tests/DesktopLife.Tests/LocationHabitTests.cs`
- `tests/DesktopLife.Tests/OrganismPersistenceTests.cs`
- `tests/DesktopLife.Tests/SaveRecoveryTests.cs`

</details>

FurnitureAffordance 集中描述用途，FurnitureContext 包含現有 RoomItem（GUID、類型、位置）、用途、平台及穩定性。平台依然決定物理支撐，語意不會讓角色傳送至不可達地點。沒有建立 ECS 或第二套行為／注意力／導航系統。

## C–D：家具行為

| 家具 | 用途與表現 |
|---|---|
| 紙箱 | Hide／Rest／Play；注意→靠近→調查→進入→安頓→藏身→探頭→休息→離開→恢復。箱板前的身體以 clipping 隱去，保留可見頭部；也可以被睡眠選為地點。 |
| 貓抓板 | Scratch／Stretch；靠近、調查、定位、前爪交替刮抓、伸展、停頓、有限重複與收尾。後腳維持支撐，前爪接觸板面。 |
| 睡窩 | Rest／Sleep／Social；使用原 Sleep Sequence，調查後增加安頓與交替踏爪，再坐下、躺下、蜷睡、醒來與伸展。 |
| 貓跳台 | Platform／Observe／Rest／Play；可高處觀察、休息，保留既有吊球互動。 |
| 書架 | Platform／Observe／Rest；可在可達層休息，床並非唯一候選。 |
| 書桌 | Platform／Observe；不再把所有桌面自動當作優先睡點。 |
| 睡墊 | Rest／Sleep／Social；保留安靜休息。 |
| 溜滑梯 | Platform／Play；保留原斜面與重力。 |
| 毛線球／鈴鐺球／玩具老鼠 | Play；保留既有玩具物理與音效。 |

用途旗標是選擇能力描述，不代表每個旗標都有新的專屬動畫。新家具使用原創 WPF 向量，沒有外部素材與新音效系統。女孩使用安全坐姿／觀察表現，不播放貓抓、舔爪、踩奶或呼嚕；紙箱藏身動作仍以貓為主。

## E–F：偏好、熟悉度與保存

RestSpotPreference 擴充有界家具習慣。每次真正抵達並使用後增加熟悉度與偏好；熟悉度縮短調查時間。選擇考慮用途、可達性、水平距離、疲勞、個性、Bond／游標距離、新鮮感、近期使用降權與約七日尺度的偏好衰減。

候選使用 soft weighting 抽選，不固定取最高分；高疲勞先限制在較近的候選，再抽選。近期使用影響以分鐘為粒度保存。偏好是較常選，不是只准選；固定種子模擬驗證床和箱子都仍有選中機會。

Learning.LocationHabits 為 optional list，最多 32 筆。每筆為家具 GUID、用途、0–1 熟悉度／偏好、最多 10000 次使用與分鐘時間戳。不保存序列、逐幀位置或事件清單。刪除家具時清理無效記錄；清除習慣不碰名字、Bond、回憶、作品、其他學習或桌面圖示備份。

存檔升為 schema v3：讀取 v2 時保留原資料並使用空習慣；正常保存沿用原子替換與三代備份。V0.6 會拒絕覆寫 v3，避免不認識新家具時回退舊房間。較新未知主 schema 仍拒絕。單獨損毀或格式不符的 optional habit 可丟棄並採預設；外層 JSON、Pet／Room 等嚴重損毀仍走原備份救援，不靜默建立全新寵物。若要回 V0.6，必須先保留 v3 並使用升級前備份，不能直接降版讀新檔。

## G–H：自發事件與導航

- 主動調查紙箱，稍後探頭；可選紙箱／書架睡覺，即使有床。
- 玩完球後坐著看球，再離開。
- 到可達高平台觀察游標。
- 醒來伸展後，可能接理毛、觀察或恢復活動。
- 活躍一段時間後偏向休息；剛玩完會暫時降低再次玩球；高 Social 且孤單時有靠近傾向。

HomeRoutine 使用既有 brain clock，每次機會後冷卻 30–65 秒，不使用硬式作息表或家具各自的計時器。生理安全與玩家照顧仍優先。紙箱／抓板／平台觀察承諾期間不被一般游標刺激不停打斷。

佈置模式將家具視為不穩定，暫停路線規劃並讓角色觀察／落地。移動、刪除使用中的家具會安全收尾；半空移除平台仍由重力落地。修正低矮平台的支撐識別容差；家具與房間平台快取只在幾何變化時重建，固定目標不重複 pathfind。沒有加入完整桌腿碰撞、navmesh 或剛體系統。

## I–M：實際驗證

Release 建置 0 warnings／errors；270 tests PASS（原 233＋新增 37）。最終隔離 WPF smoke PASS，包含 100%／150% 離屏渲染、遮擋區不接收點擊、箱內睡眠、重疊不可達家具拒絕，以及完整床面睡醒鏈；兩種縮放家具圖與探頭畫面已人工檢視。固定種子一小時邏輯模擬通過；這不是一小時實機測試。原 233 項基線測試完整保留；schema 相關舊測試只調整新版本預期值，沒有移除案例。單元測試與可重現模擬包含用途、穩定 ID、家具行為、熟悉度、偏好界限／衰減／新鮮感、清除習慣隔離、v2 相容及有限行為。

WPF 使用隔離存檔與實際透明視窗，檢查箱內遮擋、抓板爪尖、床面支撐、移動／刪除目標、編輯模式與平台／路線快取，並保留 V0.6 的吊球、桌下、半空移除平台、玩具透明邊界及角色姿態驗證。

壓力腳本 `scripts/stress.ps1 -Seconds 600`：固定決策亂數種子 70、24 個房間物件（21 家具＋3 額外玩具）與內建球／方塊，持續自主行為並定期移動、刪除、新增物件及切入睡眠、玩耍、藏身、抓板與觀察。收集 CPU、工作集、私有記憶體、GC 累積配置、例外、長時間未收尾階段、導航失敗及各階段幀數。Windows 幀時序與游標輸入仍受環境影響，並非逐幀確定性重播。

實測 607.99 秒：24 房間物件、5 玩具；例外 0、階段停滯 0、導航失敗／恢復 13、路線規劃 63、平台重建 27。藏箱、探頭、抓板、WatchBall、Sleep 與 Wake 均有實際幀記錄。整機平均 CPU 0.523%（單核心等效 8.37%）；工作集起始／結束 241.93／278.79 MiB、峰值 285.14 MiB；私有記憶體 132.74／156.11 MiB、峰值 161.15 MiB；GC 累積配置約 559 MiB。配置量不等於存活記憶體，短測記憶體增加不能直接判定為洩漏或排除洩漏。原始報告保留於本機 artifacts/verification/0.7/stress-report.json。

M：以自含式 win-x64 單檔 EXE 發行；安裝前保留使用者資料備份，安裝後驗證記錄見 HOME_07_WORKLOG。

## N–O：限制與採用的替代方案

1. 保留既有 24 個房間物件上限，因此壓測為 21 件固定家具＋3 件額外玩具，另有內建球／方塊；沒有為測試擴張產品上限。
2. 家具仍是單向平台，不是完整實體外殼。重疊／不可達時允許換目標或原地休息；不保證所有佈置都能通行。
3. 抓板採低矮水平板，以現有四足骨架交替刮抓；睡窩以交替踏爪取代完整踩奶骨架。箱內使用局部裁切，不新增獨立 renderer 或完整前後板視窗。
4. Box Ambush、帶玩具給玩家、新家具音效與敘事回憶為可選項，本版未加入。沒有為每次使用新增回憶，避免洗版。
5. 路徑先驗證可達，選擇成本主要以水平距離估計；尚非精確旅行時間最佳化。實際導航仍有超時／出口恢復。
6. 10 分鐘測試不能證明沒有記憶體洩漏；2–8 小時 soak、混合 DPI 多螢幕／熱拔插與不同 Windows／顯示卡仍需 Beta 前驗收。

## P：V0.8 建議

1. 2–8 小時 soak 與可重播行為追蹤，針對導航失敗佈置保存匿名測試案例。
2. 以實際路線長度和跳躍成本改進家具選擇，減少選到可達但太遠的地點。
3. 加強箱內照顧、出入過渡與抓板姿態細節，依 CHARACTER_ART_PIPELINE 準備有授權分層素材。
4. 混合 DPI、工作區變動、熱拔插與睡眠喚醒的實機矩陣。
5. 少量來自真實首次使用／明顯習慣形成的回憶，維持低壓力且不頻繁打擾。
