# Home Update 開發紀錄

## M0：V0.6 Audit
基線 233 tests PASS，Release 0 warnings/errors。檢查 BehaviorSequence、RestSpots、NavigationProgress、MicroBehavior、PetWindow 行為／房間／導航、RoomNavigation、RoomWindow、ToyWindow、FelineVisual、HangingToy、DirectInteraction、Companion、Personality、Learning、OrganismStore 及 README／COMPANION／UPDATE_05／UPDATE_06／ALIVE_06_WORKLOG／CURRENT_PRODUCT_DIRECTION。

家具已具有持久 GUID，enum 只能附加新值。現有序列是 runtime-only；RestSpotPreference 目前 deterministic argmax + 短期加分，需改為可注入亂數的 soft weighting 並加入小型持久習慣。RoomWindow 以獨立 HWND 呈現，箱內採角色局部 clipping 配合現有深度排序，不另建 renderer。現有路線可重用；編輯模式目前沒有阻擋行為導航，需修正。正式管線維持 UtilityBrain，研究模組只保留歷史測試。

預計修改：RoomPhysics／FurnitureAffordance、BehaviorSequence、RestSpots、Learning／OrganismStore；PetWindow.Behavior／Room／Navigation 與新增 Home partial、RoomWindow、FelineVisual、MainWindow.Room／Brain／Learning／XAML／Tray、UiText；新 Home tests、WPF diagnostics／stress、發行版本腳本與說明文件。

## M1–M2
用途映射與穩定 GUID 測試通過；242 tests、WPF 紙箱全鏈及 100/150% 遮擋檢查 PASS。Smoke 發現離地 6 DIP 平台被導航誤認為同一支撐，已將支撐辨識容差收至 4 DIP，與重力接觸容差一致。

## M3
244 tests 與抓板 WPF 實際爪尖接觸／有限反覆／收尾 PASS。沿用四足骨架與支撐面，女孩以坐姿觀察替代貓抓動作。

## M4
246 tests 與睡窩 WPF 接地／踩奶／睡醒全鏈 PASS。睡窩直接使用原 Sleep Sequence，只增加 Settle／Knead 階段。

## M5
255 tests PASS。家具習慣最多 32 筆，近期使用降權、soft weighting、新鮮感與七日偏好衰減；刪除可清理，進階區可只清家具習慣。v2→v3 保留資料；升版阻止舊 EXE 覆寫新家具，原子保存與三代備份不變。壞掉的 optional habits 局部預設，外層 JSON 及重大資料錯誤仍拒絕讀取。

## M6
261 tests PASS。既有 brain clock 低頻機會評估；Box Peek、非床睡點、WatchBall、高處觀察、三種 Wake 銜接。固定種子一小時模擬驗證種類分布與至少 30 秒機會間隔，Play 完成後暫時降頻。

## M7
261 tests 與全 WPF smoke PASS。移動／刪除目標、安全落地、編輯模式零導航，以及固定目標不重複 pathfind 已覆蓋。快取靜態平台，僅幾何變動重建；Home Affordance 僅低頻選擇時計算。

## M8
最終建置 0 warnings/errors、270 tests PASS。最終 WPF smoke 2026-09-25T10:25:45Z PASS；箱內遮擋／點擊、床／抓板接觸、睡醒、目標移除／編輯模式與 100/150% 離屏渲染通過，已檢視家具與探頭畫面。隔離原生視窗壓測實跑 607.99 秒，0 exceptions／0 phase stalls，13 navigation failures/recoveries；詳細資源數據與限制見 UPDATE_07。

## M9
自含式單檔 win-x64 0.7.0-20260925-182641 EXE 完成，SHA256 EA492F65076C586E1FD2A47C8535B95C286EC0C9FF3F0B048A3F83E5FD3C478F。Packaged WPF smoke 2026-09-25T10:27:34Z PASS。安裝前透過外部程序鎖定並備份真實 profile；安裝後獨立程序啟動成功，原有 8 個房間 GUID 保留，桌面圖示備份 SHA256 不變。真實存檔 schema 2→3 已驗證。桌面／開始選單捷徑已更新，程式不依賴 Codex 存活。未執行 2–8 小時 soak 或混合 DPI 多螢幕人工驗收。
