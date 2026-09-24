> 歷史研究文件：此內容不代表目前產品方向。正式產品不再採用果蠅神經網路或硬體餵養；以 [CURRENT_PRODUCT_DIRECTION](CURRENT_PRODUCT_DIRECTION.md) 為準。

你現在是本專案的主要軟體工程師、架構師與技術執行者。

請直接在目前工作目錄內建立、修改、Build、Test、Debug 專案。

不要只提供教學、程式碼範例或開發建議。

我是非專業程式設計使用者。

因此除非確實需要我：

- 提供正式美術素材
- 手動授權 Windows 權限
- 安裝你無法自行處理的外部工具
- 執行你無法完成的 GUI 操作
- 對重大且不可逆的產品方向做決策

否則請自行做出合理技術決策並持續工作。

但是：

不要一次試圖完成整個專案。

本專案必須採用「Milestone 分階段開發」。

每一階段必須：

1. 實作
2. Build
3. Test
4. 修正
5. 確認穩定
6. 更新簡短開發紀錄
7. 再進入下一階段

---

# 01｜專案定位

暫定名稱：

Digital Organism

或：

Desktop Life

這是一款：

Windows 11 桌面數位生命軟體。

最終角色外觀：

可愛的 Q 版小女孩。

目前 V0.1：

可以使用 placeholder 圖片、Sprite、簡易圖形或測試角色。

不要因為缺少正式美術素材而阻塞工程開發。

---

# 02｜這不是普通桌面寵物

這個角色不是單純播放：

Idle
Walk
Sleep
Follow Mouse

動畫。

核心目標是：

建立一個具有：

- 生理狀態
- 環境感知
- 多種行為
- 神經式決策
- 獎勵與懲罰
- 行為學習
- 長期偏好
- 個體差異
- 創作能力
- 桌面探索能力

的數位生命。

不同使用者長時間培養後：

角色應逐漸出現不同的行為傾向。

---

# 03｜核心世界觀

最重要的設定：

「電腦就是她的身體，也是她生存的世界。」

使用者操作電腦：

相當於餵養她。

電腦越活躍：

她越有能量、越開心、越興奮。

例如：

CPU Usage ↑

GPU Usage ↑

RAM Usage ↑

Disk Activity ↑

Network Activity ↑

CPU/GPU Temperature ↑

可以增加：

Energy

Satiety

Mood

Excitement

反之：

使用者長時間不使用電腦

Windows 長時間 Idle

電腦 Sleep / Hibernate

則：

Hunger ↑

Loneliness ↑

Boredom ↑

Mood ↓

Energy ↓

V0.1 不允許角色永久死亡。

---

# 04｜兩套獎勵系統必須分開

必須明確區分：

Environment Reward

與：

Behavioral Reward

---

## Environment Reward

來自：

CPU
GPU
RAM
Disk
Network
Temperature
Idle Time
Sleep Duration

它代表：

角色現在生活得好不好。

主要影響：

Energy
Hunger
Mood
Loneliness
Excitement
Boredom

不要直接因此強化當下 Action。

例如：

角色正在 Sleep 時使用者突然開始玩遊戲

不能因此判定：

Sleep 是一個值得強化的行為。

---

## Behavioral Reward

這才代表：

「主人認為我剛才的行為好不好。」

它影響：

Neural Plasticity
Synaptic Weight
Action Preference
Category Preference
Context Association
Learned Bias

---

# 05｜使用者手動調教

角色本體具有 Hitbox。

只有：

Mouse Cursor 位於 Pet Hitbox

並發生點擊時：

才產生行為獎勵。

定義：

左鍵角色：

Strong Positive Reward

預設：

+3

右鍵角色：

Positive Reward

預設：

+1

中鍵角色：

Punishment

預設：

-2

以上數值應可在 Settings / Config 調整。

禁止將：

使用者平常點擊 Windows UI 的左鍵、右鍵、中鍵

誤當成獎勵。

---

# 06｜Eligibility Trace

建立：

Eligibility Trace

因為：

使用者通常是在角色做完某件事後才給予獎勵。

Reward 不應只作用於點擊瞬間。

保存最近約：

2～5 秒

的：

Action
Action Category
Action Activation
Neural Output
Context
Timestamp

使用 exponential decay 或其他合理衰減方法。

例如：

0.5 秒前行為：

高 credit

2 秒前：

中等 credit

5 秒前：

低 credit

Behavioral Reward 應依 Eligibility Trace：

回溯強化或抑制相關行為。

---

# 07｜技術平台

目標：

Windows 11 x64

優先：

C#

.NET 10

若 .NET 10 在當前環境不適合：

可以使用最新穩定且適合 Windows Desktop 的 .NET。

UI 優先：

WPF

除非有充分技術理由：

不要使用 Unity。

---

# 08｜專案核心架構

建議資料流：

Windows / User Activity
↓
Sensors
↓
EnvironmentState
↓
Homeostasis
↓
SensoryEncoder
↓
Brain
↓
ActionSelection
↓
Action
↓
Animation / Desktop Interaction

另一條：

User Reward
↓
Dopamine / Reward System
↓
Eligibility Trace
↓
Plasticity
↓
Future Behavior

---

# 09｜BrainMode

建立：

BrainMode

至少支援：

Utility

FlyInspired

RealConnectomeExperimental

Hybrid

預設：

Hybrid

---

## Utility Mode

負責：

穩定、可預測的基本行為。

主要作為：

Fallback

Debug

Safety Controller

---

## FlyInspired Mode

使用我們自行建立的：

果蠅大腦啟發式小型神經架構。

包含：

Sensory Encoding

Sparse Representation

Mushroom Body-inspired Layer

Kenyon Cell-inspired Population

MBON-inspired Outputs

Dopamine Modulation

Plastic Synapses

Eligibility Trace

---

## RealConnectomeExperimental Mode

使用：

公開果蠅 connectome 真實子網路資料。

注意：

這不是要求 V0.1 模擬完整果蠅大腦。

V0.1 只要求：

建立真正可載入公開 connectome subset 的架構。

優先研究：

Mushroom Body

Kenyon Cells

MBON

Dopaminergic Neurons / DAN

以及它們必要的上下游 connectivity。

---

## Hybrid

綜合：

Utility Safety Layer

+

Neural Decision Bias

+

Connectome Experimental Output

Hybrid 應允許設定各來源權重。

---

# 10｜ExperimentalConnectome 必須從 V0.1 存在

建立獨立：

ExperimentalConnectome

模組。

不能只是 TODO。

V0.1 至少應有：

IConnectomeSource

IConnectomeLoader

ConnectomeGraph

ConnectomeNeuron

ConnectomeEdge

ConnectomeSubgraph

ConnectomeMetadata

ConnectomeSimulationAdapter

ConnectomeBehaviorDecoder

---

# 11｜公開 Connectome 資料來源設計

ExperimentalConnectome：

必須能從本機檔案載入公開果蠅 connectome 資料。

支援格式可以從：

CSV

TSV

JSON

Parquet

或其他適合格式

選擇。

資料來源 abstraction：

不能綁死單一 dataset。

預留：

MaleCNS

FlyWire

其他公開果蠅 connectome

未來擴充能力。

---

# 12｜非常重要：Connectome Token 節省規則

禁止將：

大型 connectome 原始資料

直接貼入 Codex Context。

禁止讓 Codex：

逐行閱讀數百萬至數千萬條 synapse / edge。

正確方式：

Codex 負責寫：

Loader

Parser

Graph Processor

Subgraph Extractor

Statistics Tool

Simulator

然後：

由本機程式處理大型資料。

Codex 只閱讀：

摘要。

例如：

Loaded Neurons: 4,281

Edges: 182,442

KC: 2,130

MBON: 31

DAN: 19

Memory Usage: 122 MB

Load Time: 1.6 sec

而不是：

將 180,000 條 edge 全部送入 AI Context。

這條規則非常重要。

---

# 13｜Connectome 資料快取

大型資料：

解析一次後應建立：

Cache

或：

Processed Dataset

例如：

compact binary

SQLite

compressed format

indexed representation

避免每次啟動重新解析巨大原始檔。

---

# 14｜Connectome 子網路抽取

建立：

SubgraphExtractor

支援：

按 neuron type

按 region

按 ID list

按 hops

按 edge weight threshold

抽取子網路。

例如：

Extract:

Mushroom Body

+

Kenyon Cells

+

MBON

+

DAN

+

1-hop upstream

+

1-hop downstream

不要讓桌寵每次啟動都載入完整 MaleCNS。

---

# 15｜Connectome 模式 V0.1 的目標

RealConnectomeExperimental 的目標不是：

「完整重現果蠅意識。」

V0.1 只需要證明：

真實 connectome topology

可以被載入

↓

神經活動可以在子網路傳播

↓

部分連接可進行 plasticity

↓

Reward 可以改變部分有效權重

↓

輸出可映射成：

Behavior Drives

例如：

Explore

Approach

Avoid

Rest

Create

Play

Interact

---

# 16｜Neural Interface 必須統一

建立：

IBrainController

例如：

Evaluate(EnvironmentContext)

返回：

BrainOutput

BrainOutput 不直接操作 UI。

包含：

BehaviorDrive Scores

例如：

Approach = 0.72

Explore = 0.31

Create = 0.15

Rest = 0.10

這樣：

FlyInspired

RealConnectomeExperimental

Utility

都可以使用同一接口。

---

# 17｜Homeostasis

建立：

PetState

至少：

Energy

Hunger

Mood

Boredom

Curiosity

Loneliness

Excitement

Fatigue

範圍：

0～100

所有值：

可保存

可 Debug

可測試

---

# 18｜Environment Sensors

V0.1 必須支援：

CPU Usage

GPU Usage

RAM Usage

Disk Read

Disk Write

Network Upload

Network Download

User Idle Time

Mouse Activity

若可靠：

CPU Temperature

GPU Temperature

RAM Temperature

其中：

RAM Temperature

不是必要條件。

可評估：

LibreHardwareMonitor

但請檢查授權與維護狀況。

感測更新頻率不宜過高。

---

# 19｜隱私要求

禁止：

Keylogger

記錄鍵盤內容

記錄密碼

記錄 Clipboard

讀取私人文件

讀取瀏覽內容

截圖

錄製螢幕

上傳活動紀錄

所有核心資料：

Local Only。

若未來加入 Keyboard Activity：

只允許：

按鍵頻率統計。

不能保存按了什麼。

V0.1 暫時不要實作鍵盤監控。

---

# 20｜Affordance System

建立：

EnvironmentContext

+

AffordanceProvider

不要讓每個 Action：

自行亂掃 Windows。

Affordance 是：

目前環境允許角色做什麼。

例如：

Mouse Nearby：

ObserveCursor

ChaseCursor

AvoidCursor

Desktop Empty Area：

Walk

Sit

Sleep

Draw

Write

Desktop Icon Nearby：

ObserveDesktopIcon

PseudoPushIcon

SitNearIcon

Screen Corner：

RestInCorner

Hide

Toy：

PlayToy

---

# 21｜Action Library

建立：

IPetAction

至少：

Id

Category

CanExecute()

EvaluateUtility()

Start()

Update()

Stop()

以及：

BaseUtility

NeuralBias

LearnedBias

FinalScore

---

# 22｜V0.1 Action

至少實作：

Idle

Walk

Wander

Sit

Sleep

Stretch

ObserveCursor

ChaseCursor

AvoidCursor

ObserveDesktopIcon

PseudoPushIcon

RestInCorner

PlayToy

DrawDoodle

WriteNote

Hide

Explore

---

# 23｜Action Category

至少：

Basic

Explore

Social

Create

Play

Rest

Mischief

允許 Learning：

不只學某個 Action。

還可以學：

某種 Category 整體偏好。

---

# 24｜桌面圖示互動

V0.1：

不要真的修改 Windows Desktop Icon Layout。

不要直接拖動 Explorer 真圖示。

先使用：

Pseudo Desktop Interaction。

例如：

角色靠近捷徑

↓

做推動動畫

↓

Overlay 顯示圖示副本輕微移動 / 晃動

↓

真正 Windows Shortcut 保持原位。

未來真實桌面修改：

必須：

Opt-in

可完全關閉

可復原

---

# 25｜桌面物件感知

建立：

IDesktopObjectProvider

如果可以安全可靠取得：

Desktop Icon Bounds

則使用。

若 Windows Explorer internal API 過於脆弱：

先使用：

approximation

或：

virtual objects。

不能讓這部分卡住 V0.1。

---

# 26｜DrawDoodle

角色可以：

找到桌面空白區

↓

進入 Draw 行為

↓

在自己的 Overlay 中畫畫。

V0.1 使用程序生成：

Heart

Star

Smile

Sun

Circle

Simple Face

Random Lines

不要修改：

Windows Wallpaper。

Doodle 可以：

Clear

Hide

Keep

---

# 27｜WriteNote

角色可以寫：

短句。

V0.1：

不使用 LLM。

建立：

INoteGenerator

採：

Template + State。

例如：

Happy：

「今天好開心！」

Hungry：

「有點餓了……」

Lonely：

「主人去哪裡了？」

Excited：

「好多能量！」

Bored：

「好無聊……」

未來再替換生成系統。

---

# 28｜Personality

建立：

PersonalityProfile

至少：

Curiosity

Playfulness

Social

Creativity

Mischief

Independence

Timidity

Laziness

0～1。

影響：

Action Evaluation。

例如：

Creativity 高：

Draw / Write 更常出現。

Mischief 高：

PseudoPush / Hide 等增加。

Social 高：

Approach / Chase / ObserveCursor 增加。

V0.1 Personality：

可固定初始值。

但必須預留：

Personality Drift。

---

# 29｜Learning System

至少三層：

ActionPreference

CategoryPreference

ContextAssociation

例如：

主人常獎勵 DrawDoodle：

DrawDoodle ↑

Create Category ↑

QuietDesktop → Create ↑

主人常懲罰 ChaseCursor：

ChaseCursor ↓

Social-Chase Association ↓

---

# 30｜學習速度

學習必須慢。

一次 Reward：

只造成微小變化。

持續數天：

產生明顯偏好。

持續數週：

形成不同個性。

需要：

LearningRate

Decay

Clamp

Normalization

防止權重爆炸。

---

# 31｜FlyInspiredBrain

建立小型神經核心。

不追求生物學完整精確。

但應具有：

SensoryEncoder

SparseInputLayer

KenyonCellInspiredPopulation

MBONInspiredOutput

DopamineSignal

PlasticSynapses

EligibilityTrace

BehaviorDecoder

規模：

數百至數千單元即可。

優先：

低資源

可 Debug

可視化

可塑性。

---

# 32｜RealConnectomeExperimental Plasticity

真實 Connectome topology：

不代表所有 synapse 都可以學習。

V0.1 應建立：

PlasticEdgeMask

明確指定：

哪些 edge 可修改。

優先讓：

與 Mushroom Body / Reward Learning 相關的部分：

具 plasticity。

保留：

OriginalWeight

LearnedDelta

EffectiveWeight

避免破壞原始資料。

---

# 33｜Dopamine Reward

Behavioral Reward：

轉換為：

DopamineSignal

例如：

+3

→ strong positive dopamine

+1

→ positive dopamine

-2

→ negative / punishment modulation

實際數學模型：

可採簡化工程版本。

但請在：

NEURAL_DESIGN.md

明確說明：

哪些部分是神經科學啟發。

哪些不是生物學精確實作。

---

# 34｜Utility Safety Layer

即使 Neural 模式存在：

仍保留最低限度安全規則。

例如：

Energy <= critical：

Sleep / Rest 優先。

Action 卡住：

Abort。

角色離開螢幕：

Recover。

Fullscreen：

Hide。

不要讓 Neural Brain 決定：

是否遵守 Windows 安全限制。

---

# 35｜Animation Abstraction

建立：

IAnimationController

Brain 只知道：

Walk

Sleep

Draw

Write

Push

Play

不能知道：

PNG filename

Sprite implementation

未來允許：

Live2D

Spine

SpriteSheet。

---

# 36｜Pet Window

要求：

透明

無邊框

角色外區域盡可能 Click Through

角色 Hitbox 可互動

DPI aware

不妨礙正常 Windows 操作

支援未來 Multi Monitor。

---

# 37｜Fullscreen Detection

當：

真正全螢幕遊戲

或：

Full-screen Application

啟動時：

Hide Pet。

退出：

Restore Pet。

不要因為：

普通最大化 Chrome / Explorer

就隱藏。

---

# 38｜System Tray

至少：

Show Pet

Hide Pet

Pause AI

Resume AI

Debug Panel

Clear Doodles

Reset Position

Brain Mode

Exit

Brain Mode：

Utility

FlyInspired

RealConnectomeExperimental

Hybrid

---

# 39｜Debug Panel

這是核心功能。

必須顯示：

SYSTEM

CPU

GPU

RAM

Disk

Network

Idle Time

Mouse Activity

---

HOMEOSTASIS

Energy

Hunger

Mood

Boredom

Curiosity

Loneliness

Excitement

Fatigue

---

PERSONALITY

Curiosity

Playfulness

Social

Creativity

Mischief

Independence

Timidity

Laziness

---

ACTION

Current Action

Current Category

Top Candidate Actions

Base Utility

Neural Bias

Learned Bias

Final Score

---

LEARNING

Last Reward

Reward Type

Eligibility Trace

Most Reinforced Actions

Most Punished Actions

Category Preference

Context Associations

---

FLY-INSPIRED BRAIN

Active Units

Mean Activation

Dopamine

Plastic Synapses

Recent Weight Change

Behavior Outputs

---

REAL CONNECTOME

Dataset

Subgraph Name

Neuron Count

Edge Count

Plastic Edge Count

Active Nodes

Average Activity

Simulation Tick Time

Memory Usage

Top Behavior Outputs

---

# 40｜Brain Comparison

Debug Panel 應能比較：

FlyInspired Output

vs

RealConnectomeExperimental Output

例如：

Behavior             FlyInspired   RealConnectome

Explore              0.72          0.51

Approach              0.34          0.61

Create                0.40          0.19

Rest                  0.12          0.11

這是非常重要的研究功能。

---

# 41｜持久化

保存：

PetState

Personality

ActionPreference

CategoryPreference

ContextAssociation

FlyInspiredWeights

RealConnectomeLearnedDeltas

Settings

LastSaveTime

TotalRuntime

LongTermStats

不要覆寫：

原始 Connectome Weight。

只保存：

Learned Delta。

---

# 42｜資料格式版本

Persistent Data：

必須包含：

SchemaVersion

並支援未來 migration。

---

# 43｜Sleep / Hibernate

Windows Sleep / Hibernate 前：

保存。

醒來：

計算 offline duration。

增加：

Hunger

Loneliness

但要有：

Cap。

使用者一週沒開電腦：

不能導致不可恢復狀態。

---

# 44｜效能要求

桌寵 Idle：

CPU 必須很低。

不要：

busy loop。

建議：

PetBrain：

4～10 Hz

SystemMonitor：

1 Hz

Temperature：

0.2～0.5 Hz

FlyInspired：

合理固定 tick。

RealConnectome：

可低於動畫更新率。

允許：

5～20 Hz

依子網路規模動態調整。

如果 Connectome Simulation 太慢：

Debug Panel 應警告。

---

# 45｜效能保護

建立：

SimulationBudget

例如：

MaxTickMilliseconds

MaxNeuronCount

MaxEdgeCount

MaxMemoryMB

如果資料超過限制：

不要硬載。

提示：

Subgraph too large。

建議：

縮小子網路。

---

# 46｜Token 節省策略

這條對 Codex 開發非常重要。

請遵守：

不要每個任務重新閱讀整個 repository。

優先閱讀：

與當前 Milestone 有關的檔案。

不要反覆全文讀取：

README

所有 Tests

所有 Brain Modules

除非當前任務需要。

---

# 47｜建立 Codex 專案摘要

在 repository root 建立：

CODEX_CONTEXT.md

保持精簡。

內容：

目前架構

核心 interfaces

重要 decisions

目前 Milestone

已完成項目

下一步

已知問題

檔案位置索引

限制在合理長度。

每完成 Milestone：

更新 CODEX_CONTEXT.md。

未來 Codex 優先讀：

CODEX_CONTEXT.md

而不是重新探索整個專案。

---

# 48｜Decision Log

建立：

docs/DECISIONS.md

記錄重大架構決定。

例如：

為什麼 WPF

為什麼不用 Unity

為什麼 Environment Reward 與 Behavioral Reward 分離

為什麼 Connectome 使用 subset

為什麼真實桌面 icon 暫不移動

避免未來重新討論同一決策並浪費 Token。

---

# 49｜不要大量輸出 log 給 Codex

Build / Test：

若成功：

只需摘要。

若失敗：

擷取：

相關 Error

相關 Stack Trace

不要把數 MB build log 全部送回分析。

同樣：

Connectome parser：

只輸出摘要和錯誤附近資訊。

---

# 50｜大型資料不得進 Git

Connectome 原始 dataset：

不要 commit 到 repository。

建立：

data/

並適當加入：

.gitignore。

提供：

data/README.md

說明：

應該把公開 dataset 放在哪裡。

---

# 51｜Milestone 開發策略

不要一次從 Milestone 1 跑到全部完成。

每個 Milestone：

完成後進行：

Build

Tests

Basic Manual Smoke Test

更新：

CODEX_CONTEXT.md

然後：

再繼續下一 Milestone。

若當前 Codex Session Context 已變得非常巨大：

優先留下：

CODEX_CONTEXT.md

並在後續工作使用精簡上下文。

---

# 52｜Milestone 1：Project Foundation

建立：

Solution

WPF App

Core Project

Tests

Logging

Configuration

最小可執行 App。

完成條件：

Build Pass

Tests Pass

程式可以開啟。

---

# 53｜Milestone 2：Desktop Body

完成：

透明 PetWindow

Placeholder Character

Movement

Screen Bounds

Pet Hitbox

Click Through 基礎

完成：

Idle

Walk

Wander

Sit

Sleep。

---

# 54｜Milestone 3：Windows Sensors

完成：

CPU

GPU

RAM

Disk

Network

Idle

Mouse Activity。

建立：

EnvironmentState。

---

# 55｜Milestone 4：Homeostasis

完成：

Energy

Hunger

Mood

Boredom

Curiosity

Loneliness

Excitement

Fatigue。

加入：

Environment Feeding。

---

# 56｜Milestone 5：Reward & Learning Foundation

完成：

Left +3

Right +1

Middle -2

RewardEvent

Eligibility Trace

ActionPreference

CategoryPreference

ContextAssociation。

---

# 57｜Milestone 6：Utility Brain

完成：

Utility Controller

Safety Rules

Action Selection

Debug Scores。

確保角色：

即使完全沒有 Neural Brain

也能正常生活。

---

# 58｜Milestone 7：FlyInspiredBrain

完成：

SensoryEncoder

Sparse Layer

Kenyon-inspired Population

MBON-inspired Output

Dopamine

PlasticSynapses

Behavior Decoder。

加入：

FlyInspired BrainMode。

---

# 59｜Milestone 8：Affordance & Extended Actions

加入：

ObserveCursor

ChaseCursor

AvoidCursor

ObserveDesktopIcon

PseudoPushIcon

RestInCorner

PlayToy

Hide

Explore。

---

# 60｜Milestone 9：Creative Behavior

加入：

DrawDoodle

WriteNote

Overlay Art Layer

Template Note System。

---

# 61｜Milestone 10：ExperimentalConnectome Framework

建立完整：

ExperimentalConnectome module。

至少完成：

IConnectomeSource

IConnectomeLoader

Graph

Subgraph

Parser

Cache

SimulationAdapter

BehaviorDecoder。

此 Milestone：

即使沒有下載真實 dataset

也可以使用：

tiny synthetic test connectome

測試整套架構。

---

# 62｜Milestone 11：Real Dataset Support

加入：

至少一個公開果蠅 connectome dataset adapter。

優先：

可以合理取得與使用的公開資料。

不要假設資料格式。

先閱讀官方 dataset documentation。

若需要網路下載大型 dataset：

可以建立下載說明或 downloader。

但不要將整個資料送入 Codex Context。

---

# 63｜Milestone 12：Real Connectome Subgraph

成功載入：

Mushroom Body / KC / MBON / DAN

相關 subset。

完成：

Subgraph Statistics

Cache

Basic Neural Propagation。

---

# 64｜Milestone 13：Connectome Plasticity

加入：

PlasticEdgeMask

OriginalWeight

LearnedDelta

EffectiveWeight

Dopamine Modulation

Eligibility Trace Integration。

確認：

Reward 可以改變未來輸出。

---

# 65｜Milestone 14：Brain Comparison

Debug Panel：

比較：

Utility

FlyInspired

RealConnectomeExperimental

Hybrid。

加入：

Behavior Output Comparison。

---

# 66｜Milestone 15：Persistence

保存：

全部 Pet State

Learning

Neural Weights

Connectome Learned Delta。

重啟後：

學習存在。

---

# 67｜Milestone 16：Fullscreen / Tray / UX

完成：

Fullscreen Detection

System Tray

Pause AI

Brain Mode switch

Clear Doodles

Reset Position。

---

# 68｜Milestone 17：Performance Pass

測試：

Idle CPU

RAM

Brain Tick Time

Connectome Tick

Startup Time

Dataset Load

Cache。

找出：

不必要高頻輪詢

Allocation

Memory Leak。

---

# 69｜Milestone 18：V0.1 Validation

最後驗證：

使用者持續獎勵 Chase：

Chase 機率逐步增加。

持續懲罰 Chase：

Chase 降低。

持續獎勵 Draw：

Create / Draw 增加。

持續獎勵安靜行為：

Rest / Sit 增加。

FlyInspired：

能學習。

RealConnectomeExperimental：

能因 Reward 改變部分有效連接並影響 Behavior Output。

---

# 70｜自動測試

至少：

HomeostasisTests

RewardTests

EligibilityTraceTests

ActionPreferenceTests

CategoryPreferenceTests

ContextAssociationTests

UtilityTests

FlyInspiredPlasticityTests

ConnectomeParserTests

SubgraphExtractorTests

ConnectomePlasticityTests

PersistenceTests

AffordanceTests。

---

# 71｜Synthetic Connectome Tests

不要為了測試 RealConnectome：

每次載入巨大 dataset。

建立：

TinyConnectome fixtures。

例如：

10

50

100

neurons。

用於：

CI

Unit Tests

Plasticity Tests。

大型真實資料只用於：

Integration Test / Manual Research Test。

---

# 72｜README

建立：

README.md

使用繁體中文。

包括：

專案目的

如何 Build

如何 Run

目前功能

Reward 操作

Brain Modes

Privacy

Connectome Dataset Setup

Debug Panel

已知限制。

---

# 73｜ARCHITECTURE.md

詳細說明：

模組

Data Flow

PetState

Reward

Eligibility Trace

Brain Interface

Utility

FlyInspired

ExperimentalConnectome

Affordance

Action Library

Persistence。

---

# 74｜NEURAL_DESIGN.md

說明：

Mushroom Body

Kenyon Cells

MBON

DAN

Plasticity

Dopamine

Eligibility Trace

FlyInspired Brain

Real Connectome Mode。

必須明確標示：

哪些設計具有神經科學依據。

哪些是為桌寵工程需求做出的簡化。

不要誤導成：

這是完整果蠅大腦模擬。

---

# 75｜CONNECTOME_DATA.md

說明：

目前支援哪些 dataset

下載方式

授權

來源

格式

如何放置

如何產生 cache

如何抽取 subset

資料大小

不要將大型資料加入 repository。

---

# 76｜PERFORMANCE.md

記錄：

Idle CPU

Idle RAM

FlyInspired tick

RealConnectome tick

Loaded Neurons

Loaded Edges

Memory

Dataset parsing time

Cache load time。

---

# 77｜目前不要做

不要：

大型語言模型

ChatGPT API

Cloud AI

語音模型

讀使用者文件

讀瀏覽器內容

Keylogger

螢幕截圖

真正重新排列桌面圖示

完整 16 萬 neuron 每次常駐模擬

完整 MaleCNS 全圖 plasticity

3D Engine

複雜遊戲引擎。

---

# 78｜未來預留

未來可以研究：

完整 MaleCNS

更多 connectome region

真實 Spiking Neural Network

STDP

Dopamine-modulated STDP

Sleep Consolidation

Long-Term Memory

Habit Formation

Skill Learning

Developmental Personality

Live2D

Natural Language

多角色

進化

遺傳

真正桌面物件互動。

但 V0.1：

不要提前一次全部做。

---

# 79｜Codex 工作原則

每個 Milestone：

先讀：

CODEX_CONTEXT.md

再讀：

當前相關 source files。

不要每次重新全 repo 掃描。

修改完成：

dotnet build

dotnet test

若成功：

記錄簡短摘要。

若失敗：

處理必要 error。

不要大量輸出無關 log。

---

# 80｜避免無意義重構

如果目前架構：

已乾淨

可測

可擴充

不要因為「可以更漂亮」就不停重構。

優先：

完成產品行為。

---

# 81｜避免一次修改過多檔案

每次 Milestone：

保持變更範圍合理。

尤其 Neural / Connectome：

不要同時修改：

Windows Layer

UI

Persistence

Brain

十幾個無關部分。

降低：

Regression

Debug成本

Token 消耗。

---

# 82｜遇到未知問題

如果某功能：

無法可靠實作

例如：

Desktop Icon position API

Hardware Temperature

Exclusive Fullscreen Detection

不要陷入無限 Debug。

採：

穩定 fallback

並記錄：

Known Limitation。

---

# 83｜安全底線

ExperimentalConnectome：

只能影響：

角色內部狀態

桌寵行為

Overlay。

不得讓神經輸出直接：

執行 shell

刪除檔案

啟動未知程式

修改 registry

控制 Windows 安全設定。

所有 OS-level action：

必須經過明確白名單 Action Layer。

---

# 84｜V0.1 最終完成標準

使用者啟動後：

看到桌面角色。

角色能：

Walk

Wander

Sit

Sleep

Stretch

Observe Cursor

Chase Cursor

Avoid Cursor

Observe Desktop Icon

Pseudo Push Icon

Rest in Corner

Play

Draw

Write

Hide

Explore。

---

系統感知：

CPU

GPU

RAM

Disk

Network

Idle

Mouse。

---

角色會因電腦使用：

獲得能量與幸福感。

長期不用：

飢餓、孤單、無聊。

---

使用者可以：

左鍵：

Strong Reward

右鍵：

Reward

中鍵：

Punishment。

---

Reward：

透過 Eligibility Trace

影響：

Action Preference

Category Preference

Context Association

FlyInspired Synapse

ExperimentalConnectome Learned Delta。

---

BrainMode 可以切換：

Utility

FlyInspired

RealConnectomeExperimental

Hybrid。

---

RealConnectomeExperimental：

能：

載入公開果蠅 connectome subset

執行子網路神經活動

利用部分可塑連接

產生 Behavior Output。

---

Debug Panel：

可以看見：

角色為什麼做這件事。

並比較：

FlyInspired Brain

和：

Real Connectome Brain

的輸出差異。

---

重新啟動：

學習仍然存在。

---

# 85｜現在開始

現在開始 Milestone 1。

步驟：

1. 檢查目前工作目錄。
2. 若沒有既有專案，建立新的 Solution。
3. 建立 src / tests / docs 結構。
4. 建立最小 Windows WPF App。
5. 建立 Core abstraction。
6. 建立 logging。
7. 建立 CODEX_CONTEXT.md。
8. 建立 DECISIONS.md。
9. 執行 dotnet build。
10. 執行 dotnet test。
11. 修正直到通過。
12. 更新 CODEX_CONTEXT.md。
13. 再進入 Milestone 2。

不要跳過基礎工程直接開始做 Connectome。

也不要一次展開全部 Milestone。

逐步完成。

---

本專案最終不是要做：

「會在桌面走來走去的動畫女孩。」

而是：

「一個生活在 Windows 中、以電腦活動為食物、能被主人獎勵與懲罰、能產生不同興趣與行為，並且可以在人工果蠅神經模型與真實公開果蠅 connectome 子網路之間進行實驗的桌面數位生命。」

工程優先順序：

穩定性

＞

可觀察性

＞

學習有效性

＞

Connectome 實驗能力

＞

行為豐富度

＞

美術完成度。

現在開始 Milestone 1。