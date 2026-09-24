> 歷史研究文件：此內容不代表目前產品方向。正式產品不再採用果蠅神經網路或硬體餵養；以 [CURRENT_PRODUCT_DIRECTION](CURRENT_PRODUCT_DIRECTION.md) 為準。

# 神經設計與科學界線

本軟體不是完整果蠅大腦、意識或生物精確行為的模擬器。

## 神經科學啟發

果蠅 mushroom body 的 KC、MBON 與 dopaminergic neurons 所形成的學習架構，是本專案採用「較多感覺表徵單元 → 少量輸出 → reward 調節可塑性」的概念來源。研究亦探討 MBON 輸出與行為選擇的關係。[Aso et al., 2014: architecture](https://elifesciences.org/articles/4577)、[Aso et al., 2014: output neurons](https://elifesciences.org/articles/4580)。

## FlyInspired：自行建立的工程模型

- 16 維輸入包含八項生理、CPU/GPU/RAM、presence、部分 personality 與 bias；未知硬體輸入採中性值。
- 256 個 KC-inspired 單元，固定種子 1729 的稀疏隨機投影，每單元 6 個感覺連接；只保留最高 16 個非零活動。
- 256 × 7 個有界可塑輸出權重，以 sigmoid 轉為 Approach/Explore/Avoid/Rest/Create/Play/Interact。
- 左 +3、右 +1、中 -2 轉為簡化 dopamine 調節，按過去活動 credit 改變對應行為 drive 的權重；clamp [-1,1]。顯示 dopamine 是工程指標，不是生物濃度。
- 沒有 spike timing、STDP、真實神經時間常數、嗅覺受體或完整 DAN 生理機制。

## Eligibility trace / 慢速學習

4 Hz 保存最近 5 秒的 action、category、情境、activation、Fly activity/output 與 connectome activity，不保存游標路徑。
credit = .25 × activation × exp(-age/1.5)，總 credit 超過 1 時才正規化；過期 entry 刪除，最多 24 筆。舊的單筆行為不會被重新放大到完整 credit。
ActionPreference 增量 = LearningRate × reward × credit，category 使用 .4 倍，context association 使用 .6 倍；各偏好限制 ±.75、每日緩慢衰減約 1%。預設 LearningRate .002。
0.15 秒 debounce 避免重複 routed event。單次獎勵變化很小；大量短時間測試只能證明學習方向，不能證明數週人格形成。

## RealConnectomeExperimental：真拓樸，簡化動態

載入實際 FlyWire v783 左侧 KC/MBON/DAN 子網路，保留 neuron IDs、direction、跨 neuropil 彙總的 synapse counts（至少 5）。原始連接數與神經元類別來自公開資料；輸入注入與輸出解讀由本專案自行定義。

每個腦 tick 執行 3 次 bounded rate propagation：前次活動 .2、KC 感覺注入 .3、依 target 原始 incoming 總權重正規化的傳播 .65，clamp 0–1。使用 unsigned 權重，未重現抑制性 neurotransmitter、真正突觸電流或所有 compartment dynamics。
MBON 以 ID SHA256 決定七個 drive 的工程分組；**這不代表該真實 MBON 在生物體中負責畫畫、追游標等行為**。DAN 在拓樸中存在，但 reward 由人工按鍵調節，不宣稱重現 DAN 的生理活動。
只有 source=KC 且 target=MBON 的 PlasticEdgeMask 可學習，且只強化對應 drive 的 target。`OriginalWeight` 不變、`LearnedDelta` 限制為原始值 ±50%、`EffectiveWeight = OriginalWeight + LearnedDelta`。
Delta 保存時附 dataset SHA256，換圖不套用舊 delta。Budget 超限停止本次傳播、UI 提醒、決策 fallback。

## 決策與安全

IBrainController 只回傳分數。Utility、Fly、Real 都可單獨使用；Hybrid 依設定混合，缺失來源重新正規化。
ActionSelection 加入生理 utility 與三層 learned bias，再以 temperature .25 的 softmax 選擇。低能量/極高疲勞優先 Sleep、恢復 hysteresis、螢幕邊界 clamp 不受神經權重覆寫。
Environment Feeding 只更新生理，不會呼叫 reward 或修改神經權重。神經輸出不能執行 shell、讀文件、調整 registry 或移動真桌面圖示。
