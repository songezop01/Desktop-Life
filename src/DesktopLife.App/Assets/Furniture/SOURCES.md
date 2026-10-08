# Desktop Life 0.11 家具与步态插画素材

生成日期：2026-10-07。生成工具为 Codex 内建 `image_gen.imagegen`，真实透明 PNG；提示全文在本目录 `prompts.json`。参考为使用者提供的女孩、橘猫、边牧设定图，用于造型／质感方向。

正式素材已复制进专案：

- `Furniture/furniture-v1.png`：19 件家具／玩具统一奶油木、淡粉、灰紫插画。人床和沙发由以下侧面稿替换，余下 17 件取自此图集。
- `Furniture/rest-v1.png`：长侧面人床与正面宽沙发，与原有横向接触平面及尺寸相容。
- `Companions/girl-walk-v2.png`、`orange-cat-walk-v2.png`、`border-collie-walk-v2.png`：每角色 8 张真实连续步态画面。
- `furniture-manifest.json` 明列固定来源矩形、支撑线与前景遮挡线；`Companions/walk-manifest.json` 明列 8 帧顺序、尺度参考、时长及步幅。载入时 alpha 只清理边距和矩形内的相邻片段，不用于猜测动画顺序。

没有用程序重绘生成 PNG。WPF 在载入时以固定矩形读取 alpha 轮廓、建立独立冻结 bitmap、保留透明命中区域，并将支撑和前景资料转换到相同世界坐标。家具为等比例缩放。猫跳台原画的静态吊球在绘制时裁除，再绘制有物理状态的吊球，避免出现两个球。

自动检查命令：`tools/dotnet/dotnet.exe run --project tests/DesktopLife.ArtSmoke/DesktopLife.ArtSmoke.csproj -c Release -- artifacts/verification/0.11/visuals`。

该检查覆盖 3×8 个不同的实际渲染步态、脚底接触、透明边角与点选穿透、角色比例 3.2／1.3／1、停止移动不继续踏步，以及全部 19 种家具的插画和支撑资料。输出 `asset-checks.json`、`walk-cycles.png`、`furniture-supports.png`，不代替真实多荧幕、长期稳定性或用户美术验收。

坐下／起身後續已接入三角色各 4 張新原畫：`girl-sit-v1.png`、`orange-cat-sit-v1.png`、`border-collie-sit-v1.png` 與 `Companions/transition-manifest.json`。每段 0.32 秒，正向坐下、反向起身；常態站／坐沿用同一圖集的端點，避免切換時突然變大。實際 runtime 渲染檢查見 `sit-stand-checks.json` 與 `sit-stand-cycles.png`，涵蓋 4 幀正／反順序、透明點擊區、接地、端點大小一致與起身後接步態。生成提示完整保存在 `artifacts/design/0.11/transition-candidates/candidate-manifest.json`；該目錄保留產製過程，最新接入狀態以 runtime 檢查為準。

睡眠及其他活動仍沿用 v1 姿態圖集，切換有 0.18 秒腳底為支點的 ease；本輪未聲稱這些片段具有新過渡原畫。左右移動共用鏡像，女孩不對稱髮飾會隨鏡像交換，尚未製作分別校正的左向配件圖層。
