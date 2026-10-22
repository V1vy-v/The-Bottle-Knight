# 空瓶骑士 / The Bottle Knight

> 2026 聚光灯 GameJam 参赛作品 · 2D 俯视角动作游戏 · PC

你是一个被喝干的塑料瓶。盖子和水都没了，于是你套上拳套，从冰箱一路打回超市。

---

## 玩法

核心循环只有一句话：**打赢瓶子，把它的液体吸掉，变成它的形态，再去打更强的瓶子。**

液体不只是掉落物。吸进主槽后，你会变成那瓶饮料的形态，并获得它的招式与被动：

| 液体 | 定位 | 技能 / 被动 |
| --- | --- | --- |
| 国窖 | 主动形态 | 命中积攒红温，满值后 K 放出范围火伤；释放后进入冷静，攻速与减速加成 |
| 快乐水 | 主动形态 | K 进入涌动：攻击力、移速、范围提升并附带击退 |
| 雷碧 | 主动形态 | K「你跑不过我」：移速暴涨、带残影，可穿过敌人并造成伤害 |
| 三拳 | 主形态被动 | 普攻模组换为三板斧，第二段带位移、第三段蓄力突进 |
| 料酒 | 备用槽被动 | 自身攻速提升，周围敌人有概率命中失败 |
| 冰美式 | 备用槽被动 | 攻速提升 |

- 有主动形态的液体不再提供备用槽被动；只提供被动的液体不能切换为主形态。
- 两个液体槽，Tab 切换（10 秒冷却），只能倾倒备用槽，且必须脱离战斗。

## 内容

- **三个区域**：冰箱 → 厨房 → 超市，单场景一条流程走到底，中途不读条（43 个流程节点）
- **18 种敌人**：小怪 / 精英 / 小 BOSS / BOSS / 剧情单位五级，血量 30 到 1000
- **6 种液体形态** + 近战 / 远程两套攻击模组（吸管解锁远程）
- **10 组战斗配置**、场景遭遇、无盖者商店（瓶盖兑换道具与质变，20 种道具）
- 剧情对话与任务引导（左上角单行任务文字 + 场景箭头）

## 操作

| 按键 | 功能 |
| --- | --- |
| WASD | 移动 |
| J | 普通攻击 |
| K | 技能（由装配的液体决定） |
| Q | 切换近战 / 远战模组（需吸管解锁） |
| Tab | 切换液体形态（击败快乐水后解锁） |
| F | 交互 / 吸取 / 兑换 |
| 长按空格 | 喷射位移（获得国窖形态后解锁） |
| 长按 X | 倾倒备用槽液体（需脱离战斗） |
| Esc | 菜单 |

---

## 技术栈

- **引擎**：Unity 2022.3.62f3（LTS）
- **渲染**：URP 14.0.12，2D Renderer，Linear 色彩空间；Sprite Lit ShaderGraph（红温材质 `M_Boss_RedHeat`）
- **相机**：Cinemachine 3.1.3（`CameraManager` 按 `CameraConfig` 实例化虚拟相机，靠 Priority 切常态 / 瞄准）
- **输入**：Input System 1.14.2（新旧输入系统同时启用）
- **文本**：TextMeshPro 3.0.7，字体 `LogoSC Unbounded Sans`（Dynamic + 多图集）
- **资源**：YooAsset 3.0.5，按 `UI` / `Prefab` / `Config` 三组采集，地址规则 `<组名>_<文件名>`
- **音频**：Wwise 2022.1.19（SoundBank 已提交到 `Assets/StreamingAssets/`）
- **寻路**：NavMeshPlus（2D NavMesh）
- **架构**：`AppCore` 子系统宿主 + `Global` 服务定位器；ScriptableObject 配置驱动 + 节点驱动的单场景流程

## 如何运行

1. 用 **Unity 2022.3.62f3** 打开仓库根目录（首次导入较久）。
2. 打开 `Assets/Scenes/MainScene.unity`（启动场景），点 Play。

**资源加载有两种模式**，由 `Assets/Resources/AppCoreConfig.asset` 的 `resourceMode` 控制：

- **EditorSimulateMode**：直接读 `Assets/` 里的原资产，改完立刻生效，日常开发用这个。
- **OfflinePlayMode**（仓库当前默认）：读 `Assets/StreamingAssets/GameAssets/DefaultPackage/` 里打好的 bundle（已提交一份）。

改过 `Assets/Configs`、`Assets/Prefabs/UI`、`Assets/Prefabs/Player`、`Assets/Prefabs/Enemy` 之后，**必须重新构建 bundle 才能在包里生效**：

- `Tools/打包用：构建资源包 + 切 OfflinePlayMode` —— 出包用
- `Tools/打包用：切回 EditorSimulateMode` —— 开发时切回来

场景不走 YooAsset，只走 Build Settings（`MainScene`、`GameScene`）。

## 编辑器工具（Tools 菜单）

| 菜单 | 用途 |
| --- | --- |
| `Tools/聚光灯/烘焙主角序列帧动画` / `烘焙敌人序列帧动画` | 从序列帧图集批量生成 AnimationClip |
| `Tools/聚光灯/自动填充敌人 Animator 覆盖` | 按命名规则填 AnimatorOverrideController |
| `Tools/聚光灯/敌人血量调参` | 批量调整敌人数值 |
| `Tools/聚光灯/音效配置管理` | 维护 `AudioConfig` |
| `Tools/聚光灯/修复 NavMesh 采集范围（并重烘）` | 场景改动后重烘寻路 |
| `Tools/聚光灯/字体/① 生成 TMP 字体资产并全量替换` | 换字体时一次做完资产创建与引用替换 |
| `Tools/超市流程/一键检查并补齐场景` | 超市区域占位物与刷怪点检查 |

## 目录结构

```
Assets/
  Scripts/
    Core/        AppCore、AppCoreConfig、存档代理（子系统宿主，启动入口）
    Framework/   与玩法无关的通用层：Global 服务定位器、子系统管理、UI 框架、对象池、资源、场景、计时器、输入
    GamePlay/    玩法层：Procedure 流程、Session 对局与模式状态机、Configs 配置、Player、Liquid、Enemy、Buff、Obstacle、Interaction、Dialog
    UI/          界面（Binding / Logic / Properties 三层）
    Editor/      编辑器工具与界面绑定生成器
  Scenes/        MainScene（启动）、GameScene（对局，单场景内推进）
  Configs/       ScriptableObject 配置（Player / Enemy / Battle / Liquid / Node / Prop / Buff / 对话）
  Prefabs/       Player、Enemy、UI、场景道具、相机预设
  Art/           美术资源（角色序列帧、UI、场景）
  Animations/    动画剪辑与 AnimatorController
docs/            策划文档（流程 / 敌人 / 规则 / 剧情 / 数值）+ 美术需求清单.xlsx
Taptap_Wwise/    Wwise 作者工程
```

## 已知限制

- 部分敌人的序列帧动作图集仍在补（进度见 `docs/美术需求清单.xlsx` 的「敌人动画需求」页）
- BOSS 的「红温暴走」阶段目前只到材质与设计，玩法逻辑未接
- 存档只落了字段，`ContinueGame` 不可用
- 暂停 / 过场 / GameOver 三个局内状态仍是空壳

## 第三方

- Unity 官方：Universal RP、Cinemachine、Input System、TextMeshPro、2D Sprite / Tilemap、AI Navigation、Timeline、uGUI
- 运行时库：[YooAsset](https://github.com/tuyoogame/YooAsset)（MIT）、[NavMeshPlus](https://gitee.com/allen66982881/NavMeshPlus)
- 音频：[Audiokinetic Wwise](https://www.audiokinetic.com/)（Integration 2022.1.19）
- 字体：LogoSC Unbounded Sans

## 文档

- `docs/空瓶骑士_01流程.docx` ~ `_05数值.docx`：流程、敌人、规则、剧情、数值
- `docs/美术需求清单.xlsx`：美术需求总表（含敌人动画需求矩阵）
- `AGENTS.md`：工程约定与踩坑记录（面向协作者）

---

聚光灯 GameJam · 2026
