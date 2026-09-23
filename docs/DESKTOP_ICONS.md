# 真實桌面快捷圖示互動

控制台勾選「允許桌寵移動真實快捷圖示（先備份原始排列）」即可啟用；預設關閉。也可按「與快捷圖示互動一次」。桌寵會靠近主螢幕上的快捷圖示再推動；空間不足或目標位置已有圖示時會停止移動並顯示原因。不會開啟應用程式或修改快捷方式檔案內容。

控制台與托盤提供「恢復桌面圖示排列」。恢復會停用圖示互動，還原首次移動前的位置，以及自動排列／貼齊格線設定。正常結束也會嘗試恢復；若程序崩潰、Explorer 暫時無法使用或關閉逾時，保留備份並於下次啟動重試。未攔截 Windows 右鍵「重新整理」。

備份位於 `%LOCALAPPDATA%/DesktopLife/desktop-icons-backup.json`，長期保存本機圖示識別、座標及排列設定。恢復或正常退出不再刪除或改名基準備份；`.pending` 獨立記錄本程式是否有待恢復的修改。啟動／退出僅處理待恢復修改，不會每次啟動都強制套用基準。

「更新備份為目前排列」會停止圖示互動，將目前排列設為新的長期基準，前一份保留為 `.previous`。請在自己整理好桌面後按此按鈕。舊版留下的 `.json.restored` 會自動轉回可用備份，不會在轉換時移動圖示。互動期間的手動排列會在恢復時回到基準；新的排列請主動更新備份。

已刪除或重新命名的圖示不會被誤認；新加入的圖示保持原位。螢幕範圍或原點改變時拒絕套用舊座標並保留備份，請回到原螢幕配置後重試。此功能不改桌布、圖示大小或檔案內容。

實作使用 Windows Shell 的 [IFolderView.SelectAndPositionItems](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifolderview-selectandpositionitems)，依照 Microsoft 的[桌面圖示操作說明](https://devblogs.microsoft.com/oldnewthing/20211122-00/?p=105948)，不直接操作 Explorer 的 ListView 記憶體。

本機驗證：144 項單元測試通過；實際移動快捷圖示後，核對全部 62 個桌面項目位置與原始排列旗標均已恢復。混合 DPI、其他 Explorer 版本仍需實際使用驗收。
