# V0.1 Validation — 2026-09-12

## 自動結果

- Release build：0 warnings / 0 errors。
- .NET unit tests：116 passed；Python FlyWire adapter tests：2 passed。
- Windows sensors：9/9 可用；暖機/缺失/reset 另有純核心測試。
- WPF smoke：視窗/dispatcher、生理進展、完整存讀、透明角落不獎勵、角色 routed click 獎勵、Fly/Connectome plasticity、17姿態與show/hide/reset。
- 行為驗證：持續獎勵 ChaseCursor/DrawDoodle/Sit 後機率增加；懲罰 ChaseCursor 後機率下降；Create/Rest 類別偏好增加。
- Fly：獎勵提高對應 drive，懲罰降低，單純 Evaluate 不改權重，持久化重現輸出。
- Connectome：真實2794/9836 subset、七種 drive 獎勵後上升、懲罰後下降；原始 weight 不變，非plastic edge 不學習，delta重載可重現。
- Migration：legacy schema1 → atomic organism schema2；未知版本/缺欄位/壞數值拒絕；保留舊檔與前一份 backup。

## 真實 Connectome 固定情境實验

每種 drive 使用新 simulator，100次正獎勵後再200次負獎勵；這是加速方向性測試，不是自然時間培養。

| Drive | Before | Rewarded | Punished |
|---|---:|---:|---:|
| Approach | .15631 | .15695 | .15609 |
| Explore | .20567 | .23177 | .19698 |
| Avoid | .24110 | .26329 | .23371 |
| Rest | .20159 | .21744 | .19630 |
| Create | .17864 | .19635 | .17275 |
| Play | .17898 | .19097 | .17499 |
| Interact | .20828 | .23171 | .20047 |

執行 ConnectomeTool data/flywire-mb.json --validate-all 可重現；不改角色或 dataset 存檔。

## 尚未由自動結果證明

人工視覺與跨程式 click-through、混合 DPI、多螢幕、真正 exclusive fullscreen 遊戲、實際 sleep/hibernate、長時間 memory leak 與數週個體差異。
此輪未截圖、未讀取私人文件。WPF routed click 是程式內合成事件，不能冒稱已完成真滑鼠跨應用驗收。


## 本機發行驗證
self-contained win-x64 DesktopLife.App.exe 已從發行目錄執行 --smoke-test，exit 0；包含真實subset、routed reward與完整checkpoint檢查。這是本機建置，未作外部發佈或簽章。


## 2026-09-12 顯示與外觀更新

- 單元測試 122 項：新增四層級規則、手動隱藏優先、舊設定預設與新設定序列化、步態／呼吸差異。
- WPF 冒煙測試包含兩種外觀的角色點擊、全部動作、層級共用與設定保存，以及實際最大化／無邊框測試視窗的原生偵測。
- 冒煙程序只渲染自行產生的角色向量圖，輸出 appearance-check.png 供外觀檢查；不擷取桌面或其他應用程式影像。
- 低層級採桌面前景限定與桌面上方的視窗排序，非嵌入桌布。Explorer 不同版本、多螢幕混合 DPI、獨佔全螢幕遊戲仍需實際使用驗收。


## 視窗恢復與直接互動修正

127 項核心測試通過，包含拖曳閾值／抓取偏移、玩具位移／持有／反彈、長幀移動上限與躲藏姿態。WPF 原生視窗測試驗證最大化還原、最小化不需桌面點擊，以及四個顯示視窗在低層級均位於測試應用程式之下。輕點獎勵路由測試涵蓋按下與放開。

低層級已改為獨立於前景焦點的 Explorer 桌面排序；舊版桌面前景限定的描述不再適用。圓球與方塊分離為只在形狀範圍接受滑鼠的視窗，塗鴉層仍穿透點擊。不會修改真實快捷圖示或桌布。實際跨 DPI 拖曳、不同 Explorer 版本及獨佔全螢幕仍需使用驗收。


## 參數化生成行為

135 項 .NET 測試通過，保留所有既有測試。程序圖形、組句、防重複、相反獎勵訓練與重載差異等證據及限制見 BEHAVIOR_VARIATION.md。WPF smoke 包含新作品及風格記憶保存重載。

## 2026-09-13 角落玩具與真實快捷圖示

144 項單元測試通過。ObjectContact 統一可到達的接觸位置，四角玩具推力朝向可用區域。新增 ObserveShortcut / PushShortcut，共 19 動作；既有列舉編號不變。
真實快捷圖示預設關閉，Shell IFolderView2 背景 STA 操作；首次移動前持久化備份、位置讀回核對、防重疊、跨程序鎖。一鍵／正常退出恢復，異常退出下次啟動重試。只恢復仍存在的相同項目與排列旗標；螢幕範圍或原點改變則保留備份並拒絕舊座標。
實機測試讀取 62 項／43 快捷圖示，移動後完整核對 62 項原位置與排列旗標恢復成功。WPF 測試視窗以明確 HWND 偵測，避免 Windows 前景鎖造成假失敗；不改正常前景判斷。混合 DPI 與其他 Explorer 版本仍待使用驗收。詳見 docs/DESKTOP_ICONS.md。

## 2026-09-15 陪伴版 0.2（目前產品方向，取代神經原型）

使用者明確放棄果蠅腦方案。App 已移除 ExperimentalConnectome 參考、載入器與神經模式 UI；不建立／評估 FlyInspiredBrain，不以 CPU/GPU/網路供能。舊核心研究與神經存檔欄位只保留相容／歷史用途。發行包不附實驗 DLL 或 FlyWire dataset。
新增 Companion.cs：餵飯、摸摸、陪玩、梳毛、休息；冷卻與拒絕、需求變化、親密度、30 則照顧事件回憶。Learning.Companion 可選欄位相容舊檔，舊外觀／個性／作品沿用。HomeostasisSession.AdvanceCompanion / ApplyCompanionOffline 用於新版，原 Advance 僅保留舊研究測試。
CompanionPresenceSensor 只看閒置／游標。安靜陪伴設定保存，回來招呼。UI 奶油白／鼠尾草綠陪伴小屋，右鍵照顧；中鍵不懲罰。新增 Eat/Groom/Nuzzle/Greet，23 動作，Eat 不能自主選取。
橘貓重繪眼睛、漸層毛色、項巾鈴鐺，獨立蜷睡、食碗、愛心、耳朵／追視／眨眼與平滑轉姿；女孩換髮飾服裝。155 單元測試通過、隔離 WPF 餵食／路由摸摸／保存／原生層級／角色圖集通過。詳 docs/COMPANION.md；長期養成和混合 DPI 不冒稱已驗證。

## 2026-09-16 陪伴房間 0.3

GravityBody 共用重力／平台／斜坡，InteractiveToy 加入碰撞傳遞與點擊方位力，拖放沿速度拋出。PetWindow.Room.cs 接上角色重力、跳台、側邊玩具接觸。房間八類物件、最多 24 件，Learning.Room 保存家具和玩具位置；一般家具點擊穿透，佈置模式拖曳／右鍵移除。人手、梳子、伸爪動畫。
DesktopIconSession 基準長期保留，pending 獨立；Restore(pendingOnly:true) 用於啟動／退出，UpdateBackup 主動更新與 previous 備份，舊 restored 可遷回。X 預設 CloseBehavior.Tray，RequestExit 才明確退出。167 tests + WPF 房間／重力／左右點擊／托盤／排序 smoke 通過。詳 docs/ROOM.md。

## 2026-09-19 音效、四足貓與渲染回歸

加入可替換音效包：audio-v1/custom/Meow.wav、Purr.wav、Bell.wav 優先於內建合成 WAV；控制台總音量、貓咪／玩具／環境分類音量與靜音。自訂檔案不被覆蓋。
FelineVisual 為獨立四足向量骨架，四腿步態、耳朵朝向、游標追視與瞳孔、眨眼、貓科情緒臉部；人形女孩保持原骨架。家具依相對高度將角色置於家具前後，透明玩具視窗加像素對齊／透明畫布設定，降低正方形旋轉黑邊與殘影。
角落碰撞加入強制向內向上脫困，側牆低速反彈有最小回彈速度；Soundscape 音效冷卻避免連續噪音。175 單元與封裝 smoke 通過，發行版已啟動。詳 docs/AUDIO_AND_RENDERING.md。

## 2026-09-21 陪伴房間 0.4

本輪重新核對原始碼，舊版顯示排序及旋轉視窗設定不足以排除閃動／裁切，現已修正。新版使用獨立轉耳、杏仁眼瞳孔縮放、四足支撐／抬腳步態、腳趾與尾部環紋。所有接地姿勢維持 Y=144 的接觸基準，落地解除空中姿勢。

- Release 全方案建置：0 警告、0 錯誤；183 項測試通過（含阻尼吊球／平台穩定、單一執行個體通道及同步等候 UI 執行緒不死鎖的回歸測試）。
- 本機 WPF --smoke-test exit 0；檢查主機原生視窗前後順序、五次穩定更新不重新排序、透明角落點擊穿透、托盤與保存／載入。
- cat-platform-check.png 已人工檢視：站立／走路／伸展／睡覺／吃飯均貼合上層跳台；渲染像素檢查最後兩列仍有角色接觸。
- 實際 PetWindow 路徑跳躍至下層後碰到吊球，繪製爪尖與施力位置誤差小於 2 DIP；核心測試驗證擺動方向、繩長與阻尼。
- square-rotation-check.png：45 度與 1.12 倍拿起縮放下，64×64 視窗邊界完全透明；已檢視輸出圖。

診斷使用臨時存檔，只繪製本程式內容，不搬動使用者桌面快捷圖示。混合 DPI、多螢幕、不同顯示卡及長時間效能尚未完成實機驗收。新版獨立發行啟動結果記於 DISTRIBUTION.md。
