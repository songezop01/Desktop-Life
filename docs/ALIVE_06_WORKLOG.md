# V0.6 開發紀錄

## M0：實際架構盤點

正式入口 App → MainWindow → UtilityBrain/ActionSelection → PetAction → PetWindow。BodyAction 為高階意圖，BehaviorDecoder 使用既有性格與偏好產生參數。PetWindow 33 ms 更新物理、導航、姿態；MainWindow 250 ms 選擇行為、1 秒需求更新。FelineVisual 為向量四足骨架，接地基準 144 DIP。RoomNavigation 為平台 BFS，PetWindow.Navigation 執行路線。玩具目前靠近便施力，Sleep 原地切姿，照顧沒有階段。存檔 v2 與三代備份保持。

將擴充 Core 的 BehaviorSequence/Attention/Interest/RestSpot 與微行為排程，App 新增 PetWindow.Behavior partial，修改 Brain/Care 入口、导航失敗恢復、FelineVisual 的姿態輸入、效能與診斷。保留 BodyAction、IPetAction、平台 BFS、重力、DPI、hit testing 及 Explorer 策略。

影片 7.4 秒顯示角色在靠右的桌下重複姿態而未移動。程式檢查發現導航不可達狀態沒有離開／超時策略，地面靠近平台亦缺少整段無進展監控。將增加可重現的桌下／靠牆／中途失效回歸。

Legacy：App 不參考 ExperimentalConnectome，正式感測僅游標與閒置。舊文件與測試保留，不重新接入神經或硬體餵養。

## M1
BehaviorSequence、SequenceContext、SequenceStyle、ToyInterest 已建立；不增加 BodyAction 或存檔欄位。221 項測試通過，含五類行為完成、中斷、目標失效與 bounded personality／bond 節奏。

## M2
玩具鏈已接入實際視窗；每次擊球前有觀察與準備，接觸點與爪尖一致。WPF regression 抓到靠近容差過大而碰不到球，已修正為 1.5 DIP；完整 smoke 通過。

## M3
休息地點由可達性、家具種類、既有個性、親密度與近期使用選擇；無可用地點安全原地休息。真正入睡才累積睡眠恢復。224 項 tests 與 WPF 睡前／睡後階段檢查通過。

## M4
摸摸依低／中／高親密度呈現猶豫、接受、靠近與呼嚕差異；自主理毛與手動梳毛分流。224 項測試與包含梳子／手顯示時機的 WPF smoke 通過。

## M5
228 項測試與 WPF 桌下導航回歸通過：右牆桌下登上桌面、下桌、失敗後走出遮蓋區、半空失去平台自然落地；每步位移受限且無瞬移。

## M6–M7
233 項測試通過；新增非週期眨眼、耳動與小幅重心變化，接觸時固定爪尖；隱藏／靜止睡眠降頻，吊球靜止停止更新。16 姿態 100%／150% 支撐像素及 WPF smoke 通過。進階／診斷折疊、關係文字簡化、CURRENT_PRODUCT_DIRECTION 與 legacy 告示完成。詳見 UPDATE_06 的證據與限制。
