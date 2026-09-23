# Performance — 2026-09-12

## 正常背景模式短測

45 秒，前 5 秒暖機不計，共 41 樣本；16 logical processors。Hybrid、9 項感測、真實 FlyWire subset 開啟，角色固定 Idle、控制台 3 秒後隱藏。

| 指標 | 結果 |
|---|---:|
| 平均整機 CPU | 0.353% |
| 平均單核心等效 CPU | 5.64% |
| Peak working set | 242.3 MB |
| Private memory 首/末 | 152.3 / 105.8 MB |

第一次控制台可見量測平均整機 CPU 1.38%。之後降低 debug 更新至最高 1 Hz、隱藏時停止面板重繪，角色重用 transform。兩次顯示條件不同，不能把全部差異歸因於程式最佳化。
此短測未見持續記憶體增加，但不是長時間 memory leak 驗證。原始結果在 artifacts/performance.json；重跑 scripts/measure-performance.ps1。

## 神經與資料工具

| 指標 | 短測結果 |
|---|---:|
| FlyInspired 平均 tick，1000 次 | 0.105 ms |
| Real connectome 平均 tick，100 次 | 0.396 ms |
| Real connectome 最大 tick | 2.55 ms |
| Cold JSON load + 建立 cache | 155 ms |
| Warm cache load（含 SHA256） | 79 ms |
| Neurons / Edges | 2794 / 9836 |
| Plastic edge candidates | 7344 |
| CLI managed memory（非全程峰值） | 約 11.2 MB |
| 原始 Feather rows | 16,847,997 |
| Python streaming subset extraction | 2.32 sec |
| Normalized JSON | 1,184,220 bytes |

CLI 與桌面工作集是不同量測範圍。實際 tick 受 JIT、系統負載與 GC 影響。工具資料在 artifacts/connectome-performance.json。

## 感測

初期 6 次 sample：平均 16.38 ms、最高 28.27 ms；後續 live smoke 約 17–20 ms/次。每秒一次背景 worker，不阻塞 WPF UI。
動畫約 30 Hz、brain/trace 4 Hz、生理/感測 1 Hz、debug最高1Hz。Connectome每次評估3個內部 propagation steps。

## 尚未量測

跨硬體效能、完整啟動延遲、長時間記憶體洩漏與數小時遊戲負載。這些結果不代表所有 Windows 11 裝置的保證值。
