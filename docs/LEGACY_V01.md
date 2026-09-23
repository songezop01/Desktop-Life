# 歷史版本資料（不適用目前陪伴版）

# Windows Neural Desktop Life V0.1

Windows 11 x64 的本機桌面數位生命原型。M1–M18 已逐階段完成實作與自動驗證；正式美術、混合 DPI、真實遊戲與長期培養仍有驗收限制。

## 啟動與操作

雙擊「Start Desktop Life.cmd」。已有本機獨立版本時會啟動 artifacts/release/DesktopLife.App.exe，不需另外安裝 .NET。

- 左鍵角色：+3 強獎勵；右鍵：+1 獎勵；中鍵：-2 懲罰。只有角色 hitbox 的點擊有效。
- 獎勵回溯最近 5 秒，緩慢改變行為、類別、情境與神經權重。正常 Windows 點擊不計分。
- 控制台可選 大腦模式、手動動作、回到自動、暫停／恢復、顯示／隱藏、重設位置、載入 connectome、清除／保留／隱藏作品。
- 最小化會收到系統托盤，雙擊托盤圖示可開回控制台；右鍵托盤可操作。按視窗 X 或「結束」 會保存並退出。
- 紫色方塊與紅球是玩具。真實快捷圖示互動預設關閉，可於控制台啟用，並可一鍵恢復原排列；桌布不會被修改。

## 顯示層級與外觀

控制台上方「顯示與外觀」或托盤選單可即時調整，設定會保存。

- 最高：維持最上層，無邊框全螢幕仍顯示；不保證覆蓋獨佔全螢幕遊戲與 Windows 安全桌面。
- 高：前景全螢幕時隱藏。
- 中（預設）：前景全螢幕或最大化視窗時隱藏。
- 低：置於桌面上方、所有應用程式視窗下方，視窗還原或最小化後自然露出；不需要點桌面。不會更換桌布或將角色寫入桌布。

層級每 200 毫秒檢查，角色、塗鴉與玩具同步切換；手動隱藏優先於所有層級。低層級只被應用程式實際覆蓋的部分遮住。
外觀可選女孩或橘貓。走路擺動四肢，追逐／躲避游標採用奔跑步態，睡眠會躺下呼吸，另有坐下、伸懶腰、玩球、推圖示與揮筆動作。點擊角色不搶走鍵盤焦點。
控制台、動作名稱與托盤操作統一為繁體中文；資料集名稱、格式、單位與技術錯誤代碼保留原名。

## 拖曳與互動

- 左鍵按住角色、圓球或紫色方塊可拖曳；移動超過 5 點才算拖曳，單純輕點角色仍是獎勵。
- 提起角色會輕晃，放下後停留片刻；圓球與方塊依放手速度滑動、減速並在工作區邊緣反彈。輕點玩具也會彈動並吸引桌寵。
- 紫色方塊是玩具，不是 Windows 快捷圖示。桌寵靠近才會實際推動它，移動後不會每幀歸位；真實快捷圖示互動另有開關、首次移動前備份及退出恢復，詳見 [圖示互動說明](docs/DESKTOP_ICONS.md)。
- 自動模式下，在桌寵附近移動游標會吸引觀察／追逐，有 8 秒間隔與停靠距離；睡眠、角落休息、拖曳與暫停時不打斷。
- 「躲在角落」會慢慢走到角落並蹲下，仍可看見與點選，不再瞬間透明消失。

## 功能

17 種基礎與擴充行為：Idle、Walk、Wander、Sit、Sleep、Stretch、ObserveCursor、ChaseCursor、AvoidCursor、ObserveDesktopIcon、PseudoPushIcon、RestInCorner、PlayToy、DrawDoodle、WriteNote、Hide、Explore。

每秒感測 CPU/GPU/RAM、磁碟讀寫、網路上下傳、閒置時間與滑鼠取樣位移；缺失顯示「無資料」。
八項 0–100 生理狀態隨電腦活動、閒置與休息改變；Environment Feeding 不會獎勵當下 action。
圖畫由多種基本元素程序化組合，短句由本機語言片段組成，沒有 LLM。大腦、個性與獎勵歷史會影響畫風、文風、路徑與社交距離。作品最多 32 件，未保留作品約 2 分鐘到期。

## 行為風格與養成

控制台「行為風格輸出」可觀察創意、新奇、對稱、複雜、社交等傾向，以及實際繪畫、文字、速度與游標距離參數。
對剛完成創作的角色給予獎勵／懲罰，會透過原有五秒回溯緩慢學習該畫風或文風。不同訓練歷史可形成不同的大小、複雜度、句長、速度、角落偏好及親近程度；近期輸出記憶會降低短時間重複機率。
參數、風格偏好與生成作品都在本機處理與保存。這是程序化行為與學習，不是語言模型或意識。實作與驗證參見 docs/BEHAVIOR_VARIATION.md。

## Brain Modes / Debug

- Utility：可解釋的生理 utility、偏好與安全規則。
- FlyInspired：256 KC-inspired 單元、top-16 稀疏活動、7 個可塑輸出。
- RealConnectomeExperimental：真正 FlyWire v783 左側 MB subset，2,794 neurons / 9,836 edges。原始拓樸是真資料，輸入、動態與行為映射是工程簡化。
- Hybrid（預設）：混合以上來源；資料不可用或超預算時明確 fallback。

控制台顯示四種來源的 drive 比較、action 分數拆解、personality、eligibility trace、偏好、生理與感測。
這不是完整果蠅大腦或意識模擬；參見 docs/NEURAL_DESIGN.md。

## 保存與設定

正式資料在 %LOCALAPPDATA%/DesktopLife：
- organism.json：schema 2 原子存檔，含生理、個性、三層學習、Fly 權重、Connectome delta + dataset hash、作品、runtime、reward counts、設定快照。
- organism.json.bak：前一份完整存檔。
- settings.json：可調 reward、LearningRate、BrainMode、Hybrid 權重、ConnectomePath。關閉程式後修改；下次啟動讀取。
- logs/app.log：輪替診斷紀錄，不包含活動歷史。

每 60 秒、正常關閉及 suspend 保存；重啟與 resume 計算 capped offline，最多 8 小時，不會永久死亡。
首次從舊版 pet-state.json / learning.json 遷移，保留原檔；之後以 organism.json 為準。未知/損壞 schema 拒絕載入，不自動重設。
同一資料目錄只允許一個 instance。

## Build / Tests / Publish

專案使用本機 .NET SDK 10.0.401（tools/dotnet），不修改系統 SDK。
新環境由 Microsoft dotnet-install.ps1 以 -Channel 10.0 -InstallDir tools/dotnet -NoPath 安裝。

    powershell -ExecutionPolicy Bypass -File scripts/build.ps1
    powershell -ExecutionPolicy Bypass -File scripts/smoke.ps1 -RequireAllSensors
    tools/data-python/Scripts/python.exe scripts/test_flywire_adapter.py
    powershell -ExecutionPolicy Bypass -File scripts/publish.ps1

目前 116 .NET tests + 2 Python adapter tests 通過；真實 subset 七種 drive 獎懲方向測試通過。
Smoke 使用獨立 %TEMP%/DesktopLifeSmoke/<id>，不更動你的角色。
效能量測與資料工具見 docs/PERFORMANCE.md、docs/CONNECTOME_DATA.md。

## Privacy

沒有鍵盤內容監控、clipboard/文件/瀏覽器內容讀取、截圖、錄影或活動上傳。
游標位置只供當下互動與位移計算；不保存路徑。只有生理/偏好/學習結果持久化。
桌寵不連雲端；手動開發用資料下載腳本只下載公開 connectome，不上傳資料。

## 已知限制

主螢幕工作區、簡易幾何 placeholder；Wander 為水平往返，Explore 為二維目的地移動。
玩具使用虛擬物件；啟用真實快捷圖示互動後，另透過 Windows Shell 讀取圖示位置並先備份再移動。溫度未啟用；網路可能含 VPN/虛擬介面重複計數，滑鼠統計為取樣端點位移。
一般 borderless fullscreen 已加入幾何偵測，但 exclusive game、混合 DPI、實際睡眠/休眠與跨程式 click-through 仍需人工驗收。
45 秒短測不等於長期無記憶體洩漏；批次學習測試不等於數天/數週人格養成證明。
Connectome 只支援已處理 JSON 與 FlyWire adapter，MaleCNS 專用 adapter 尚未提供。來源、授權與簡化界線見 docs/CONNECTOME_DATA.md。


