# Desktop Life：目前產品方向

Desktop Life 是 Windows 本機陪伴寵物。重點是連續、可理解的行為，個性與關係逐步影響互動，讓桌面像寵物居住的房間。

正式管線為 UtilityBrain → ActionSelection／PetAction → BehaviorSequence → PetWindow 物理與導航 → FelineVisual。BodyAction 代表意圖，序列管理注意力與階段，物理接觸決定實際施力。微動作使用既有更新迴圈，不以每幀重新抽選高階行為。

果蠅神經、FlyWire、Connectome、硬體負载餵養已停止用於正式產品。ExperimentalConnectome、資料工具、相關測試與舊設計文件保留為歷史研究；不能用其測試數量聲稱產品具有生物大腦。本版本不增加雲端、LLM、鍵盤內容讀取或資料上傳。

維持 WPF、四足向量骨架、144 DIP 支撐基準、滑鼠命中區、既有重力與顯示器座標。存檔維持 schema v2；短期行為、注意力、玩具冷卻與微動作不寫入存檔。桌面圖示互動預設關閉，啟用先備份，提供恢復；不修改原本安全策略。

驗證要區分單元測試、WPF 真實視窗檢查、短時間效能採樣與人工長期體驗。水彩毛流、多螢幕實機驗收、完整家具碰撞體尚未完成，不以向量姿態更新冒充高解析素材工程。
