> 歷史研究文件：此內容不代表目前產品方向。正式產品不再採用果蠅神經網路或硬體餵養；以 [CURRENT_PRODUCT_DIRECTION](CURRENT_PRODUCT_DIRECTION.md) 為準。

# Connectome 資料與重現

## 已支援

本機 normalized JSON `ConnectomeGraph`；獨立 `IConnectomeSource` / `IConnectomeLoader` 可擴充其他公開 dataset。FlyWire v783 提供實際 Python Arrow adapter；MaleCNS 尚未提供專用 adapter，不能把其欄位直接當作 FlyWire。

## 官方來源

- [FlyWire Whole-brain Connectome Connectivity Data / Zenodo 10676866](https://zenodo.org/records/10676866)：connectivity 授權 CC-BY-4.0。
- [flyconnectome/flywire_annotations](https://github.com/flyconnectome/flywire_annotations)：使用 `Supplemental_file1_neuron_annotations.tsv`，固定 commit `8587524c1748ce5ef2080822a2fc890fc03bf597`。該 repository 未找到獨立授權條款，本專案只作本機研究處理，未將原始資料加入 Git。
- 來源作者/工作：Dorkenwald et al., 2024 的 FlyWire connectome 與 Schlegel et al., 2024 的 annotations；詳細歸屬、方法与來源鏈見上述官方頁面。

`proofread_connections_783.feather` 為每個 pre/post neuron pair 與 neuropil 的彙總連線，使用 `pre_pt_root_id`, `post_pt_root_id`, `neuropil`, `syn_count`；不是逐一 synapse 座標檔。原始檔 852,022,274 bytes，官方 MD5 `f48f972d262323a102aed49af1396b8a`，本機已驗證。

## 本機目錄

- `data/raw/neuron_annotations.tsv`（約 31.7 MB）
- `data/raw/proofread_connections_783.feather`（約 852 MB）
- `data/flywire-mb.json`（約 1.18 MB，正式使用的 subset）
- `data/flywire-mb.summary.json`（摘要）
- `data/cache/<sha256>.schema1.json.gz`（快取）

以上均排除 Git。桌寵只載 subset，不會載完整 852 MB 檔，也不會啟動 Python 或自動下載。

## 重建

專案已有 `tools/data-python` 虛擬環境。若需重建環境：`python -m venv tools/data-python`，再 `tools/data-python/Scripts/python.exe -m pip install pyarrow`。
執行 `tools/data-python/Scripts/python.exe scripts/prepare_flywire.py --download` 可下載並處理；已有原始檔則省略 `--download`。中斷的大型下載可用 `scripts/download_flywire.py` 接續，採四個驗證 Content-Range 的請求，完成後驗 MD5。
Adapter 預設 `--side left --threshold 5`，串流 record batches，只保留 KC/MBON/DAN 節點之間的連接，彙總 neuropil 後套 threshold；不列印原始 rows。

輸出：2,794 neurons（2,580 KC、48 MBON、166 DAN）、9,836 edges、7,344 KC→MBON 可塑候選；共在本機掃過 16,847,997 個 source rows。行為標籤為工程 decoder，參見 NEURAL_DESIGN。

## C# 工具與抽取

`tools/dotnet/dotnet.exe run --project src/DesktopLife.ConnectomeTool -c Release -- data/flywire-mb.json`
輸出只有載入/快取/神經元/邊/活動/tick/可塑性摘要。工具中的 reward 測試只在記憶體運作，不改原始資料或角色存檔。
可加 `--extract data/smaller.json --types KC,MBON --regions MB --ids id1,id2 --hops 1 --threshold 10`。Seed 使用 type/region/ID 的聯集；hops 遍歷上游與下游，threshold 先過濾 edges。空結果/過大結果拒絕。
標準預算：5,000 neurons、100,000 edges、估計 128 MB、20 ms/tick；parser 原始 JSON 上限 32 MB（由記憶體預算限制）。不適合直接輸入完整 connectome。
控制台「載入 Connectome subset」只開本機 normalized JSON。預設使用專案的 `data/flywire-mb.json`；檔案缺失顯示 N/A/Utility fallback。
