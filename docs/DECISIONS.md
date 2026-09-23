# 架構決策

- 2026-09-11：採 .NET 10 / WPF；專案本機 SDK 10.0.401，避免修改既有 .NET 安裝。WPF 可提供 Windows 透明視窗，無需 Unity。
- Core 不依賴 WPF；OS/UI 實作留在 App，神經輸出未來只提供分數。
- Environment Reward 只改生理；Behavioral Reward 才改學習。後續分別實作，不混用。
- Connectome 後續只載入有預算限制的 subset；大型檔案排除 Git；不把原始資料送入模型上下文。
- 真桌面圖示不移動；後續使用虛擬物件與 overlay。
- 未知設定 schema 拒絕載入，不自動重置使用者資料。未來新增版本時必須補 migration。
- M3：獨立 DesktopLife.Windows，使用 Win32 / PDH，避免為溫度安裝核心驅動。溫度明確未啟用。
- M3：GPU 用 wildcard array 動態取得引擎，合併同一硬體引擎的 process contributions，取最忙引擎；不是把所有引擎相加。
- M3：失敗／暖機為 null + reason，無假數據；背景 1 Hz 取樣。大於 10 秒取樣間隔重設速率基線，避免休眠後假尖峰。
- M4：純 Core 生理模型以每分鐘速率 + 經過時間更新，輸入僅為有效環境值與是否休息；不引入 dopamine/learning。
- M4：依要求提前保存生理狀態，完整神經與偏好持久化仍留 M15。離線最多 8 小時、即刻 checkpoint，不會因一週未開機永久失去角色。
- M4：smoke 資料改用獨立 temp；正式資料用 instance.lock 防多開。寫入失敗不默默關閉，損壞/未知 schema 不自動 reset。

- M5–M7：三層慢速偏好、5秒trace與Fly稀疏網路，環境供能不呼叫dopamine。
- M10–M14：使用真實FlyWire v783左側MB subset；unsigned propagation與MBON drive映射屬工程簡化，不宣稱生物功能對應。
- M15：完整狀態統一schema2 atomic checkpoint，明確legacy migration。
- M16–M18：不靠神經遵守OS限制；fullscreen/tray由Windows白名單層處理。debug hidden停重繪，release本機self-contained，正式驗收限制透明記錄。
