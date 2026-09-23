# Windows Sensors — M3

所有數值來自本機；不讀檔案內容、不記錄按鍵、不解析網路內容、不截圖。應用只顯示最新快照，不保存活動歷史。

| 項目 | 實作與單位 | 限制 |
|---|---|---|
| CPU | GetSystemTimes 差分，百分比；kernel 已包含 idle | 初始/重設為 N/A；超過 64 邏輯 CPU 的 processor groups 尚未驗收 |
| GPU | PDH GPU Engine wildcard array，同一硬體引擎先合併 process 值，取最忙引擎 % | 支援與否依 Windows/GPU driver；多 GPU 為最忙引擎，不是平均；與 Task Manager 取樣時點不相同 |
| RAM | GlobalMemoryStatusEx，實體記憶體使用 % | 不含 VRAM |
| Disk | PhysicalDisk(_Total) Read/Write Bytes/sec | 包含所有實體磁碟，不讀檔名；counter 不可用為 N/A |
| Network | 啟用的非 loopback 介面 BytesSent/Received 差分 | 包含虛擬/VPN 介面，可能重複計數；不是純網際網路速率；介面加入或 counter reset 重新暖機 |
| Idle | GetLastInputInfo，秒 | 僅目前使用者 session，32-bit tick wrap 計算；不取得按了什麼 |
| Mouse | GetCursorPos 每秒位置差的直線距離 / 秒 | 是取樣位移，不是完整路徑；移出再移回可能為 0；不計點擊 |
| Temperature | 未啟用 | 沒有安裝硬體 driver，CPU/GPU/RAM 溫度不假設可讀 |

PDH 以 PdhAddEnglishCounterW 加入語系無關路徑；wildcard 以 PdhGetFormattedCounterArrayW 讀取整個動態 array，不逐一新增 instance。只接受有效 counter status。GPU buffer 上限 16 MB，instance 變動導致失敗時下一秒重試。

取樣由單一 worker 使用 PeriodicTimer，每秒一次，不阻塞 WPF dispatcher。不做 busy loop。首次及間隔超過 10 秒的速率資料為 N/A，下一個正常樣本恢復。關窗 cancellation 後釋放 PDH query。

## 驗證

執行 `powershell -ExecutionPolicy Bypass -File scripts/smoke.ps1`；可加 `-RequireAllSensors` 強制要求本機 9 項全部可用。
平常單元測試不要求硬體可用；核心測試涵蓋 CPU idle 算法、rate reset、休眠 gap、tick wrap、負螢幕座標、GPU 合併和 missing/zero 區分。

## 官方 API 來源

- [PdhAddEnglishCounterW：語系無關路徑](https://learn.microsoft.com/en-us/windows/win32/api/pdh/nf-pdh-pdhaddenglishcounterw)
- [PdhGetFormattedCounterArrayW：wildcard 陣列與 buffer 流程](https://learn.microsoft.com/en-us/windows/win32/api/pdh/nf-pdh-pdhgetformattedcounterarrayw)
- [GetLastInputInfo：目前 session 的閒置資訊](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlastinputinfo)
- [Microsoft GPU 使用量與引擎概念](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/)
