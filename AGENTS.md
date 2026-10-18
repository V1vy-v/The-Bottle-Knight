# AGENTS.md

Unity 2022.3.62f3 (LTS) 2D 游戏项目（TapTap「聚光灯」GameJam 参赛项目，Product Name: `SpotLight`，Company: `DefaultCompany`，`bundleVersion: 0.1.0`）。日常开发通过 Unity Editor 进行；仓库内**没有** CLI 构建 / 测试 / lint 命令。

## 工具链

- **渲染**：URP 14.0.12，2D Renderer，Linear 色彩空间（`ProjectSettings/ProjectSettings.asset: m_ActiveColorSpace: 1`）；管线资产在 `Assets/Settings/`（`URP-Balanced` / `URP-HighFidelity` / `URP-Performant` 各带同名 `-Renderer`，2D 用 `URP-2D-Renderer.asset`），全局设置在 `Assets/UniversalRenderPipelineGlobalSettings.asset`。
- **输入**：Input System 1.14.2，`activeInputHandler: 2`（新旧输入系统同时启用）；`Assets/InputActions/PlayerInputActions.inputactions` 与其生成的 `PlayerInputActions.cs`（**生成物，不要手改**）。`Player` 映射：`PrimaryAttack → J`、`Skill → K`、`Interact → F`、`SwitchLiquid → Tab`。
- **相机**：Cinemachine 3.1.3，预设 `Assets/Prefabs/Cameara/Third Person Camera.prefab`、`Third Person Aim Camera.prefab`（注意目录名就是拼错的 `Cameara`）。
- **资源与打包**：YooAsset 3.0.5（`Assets/Scripts/Framework/SubSystems/Resource/Provider/YooAsset/YooAssetProvider.cs`）。采集配置 `Assets/Resources/BundleCollectorSetting.asset`，三组均为 `CollectorType: 0`（MainAsset）+ `PackRule: PackDirectory` + `AddressRule: AddressByGroupAndFileName`：
  - `UI` ← `Assets/Prefabs/UI`
  - `Prefab` ← `Assets/Prefabs/Player`、`Assets/Prefabs/Enemy`
  - `Config` ← `Assets/Configs`

  **地址规则是 `<组名>_<文件名（无扩展名）>`**（如 `Prefab_Player`、`Prefab_Enemy_Water`、`Config_国窖对话`）。地址**大小写敏感**，改文件名就等于改地址。**场景不走 YooAsset**，只走 Build Settings；Wwise SoundBank 走 StreamingAssets。
- **`AppCoreConfig.resourceMode = 1`，即 `EPlayMode.OfflinePlayMode`** —— 运行期读的是**打好的 bundle**（构建输出在 `Bundles/<平台>/`），不是 `Assets/` 里的原资产。**改资产地址 / 文件名后要在游戏里生效，必须重新构建一次 YooAsset bundle**（编辑器里跑同一份 bundle 的情况除外，自己确认）。另外：`Assets/StreamingAssets/` 下目前**没有** `yoo` 目录，只有 Wwise 的 `Audio/` 和 `desc.txt`，如果进游戏报资源初始化失败，先查这里。
- **寻路**：NavMeshPlus（`com.h8man.2d.navmeshplus`，2D 版 NavMesh）；烘焙数据在 `Assets/Scenes/GameScene/NavMesh-Navigation*.asset`。
- **音频**：Wwise 2022.1.19.8584 深度集成；作者工程在**本仓库内** `Taptap_Wwise/Taptap_Wwise.wproj`（与 `Assets/` 同级，所以 `WwiseProjectPath=../Taptap_Wwise/Taptap_Wwise.wproj` 才解析得到）。两个场景都有 `WwiseGlobal`。
- **UI 文本**：TextMeshPro 3.0.7；中文字体源文件 `Assets/Art/Fonts/AaHouDiHei-2.ttf`（详见末尾「中文文本」）。
- **编辑器内自动化**：Unity MCP（`com.coplaydev.unity-mcp`，HTTP `http://127.0.0.1:8080/mcp`）；`.cursor/mcp.json` 与 `~/.codex/config.toml` 的 `[mcp_servers.unityMCP]` 都指向该地址。**只有 Unity Editor 打开本工程且 MCP 桥接启动后该端口才可用**，否则工具调用直接连接失败。

## 项目结构

- `Assets/Scripts/` —— 项目自有代码 **164 个 `.cs`**，全部编译进 `Assembly-CSharp`。**目录名与 namespace 并不一一对应，以代码为准**：
  - `Core/`（`namespace Core`）—— `AppCore`（`MonoSingleton<AppCore>`，全局子系统宿主）、`AppCoreConfig`、`AppModel`、`AppModelProxy`。启动顺序是：**先**注册 `ResourceManager` 并 `SetProvider(new YooAssetProvider(...))`，在 `OnResourceReady` 回调里才 `InitSubSystems()`（`StorageManager` → `InstantiateManager` → `PoolManager` → `TimerManager` → `DataProxyManager` → `UIManager` → `SceneLoader` → `LocalInputManager` → `AudioManager` → `CameraManager` → `GameplayRoot`），再 `InitDataProxy()`（`AppModelProxy`、`GameSaveDataProxy`），最后置 `IsReady` 并触发 `OnAppReady`。**订阅 `OnAppReady` 不必关心 Awake 顺序：已就绪后再订阅会立刻收到一次回调。**
  - `Debug/`（`namespace Core`）—— `ConsoleToScreen.cs`，把 Runtime 日志显示到屏幕。
  - `Framework/`（`namespace Framework`；少数为 `Framework.SubSystems`，编辑器侧为 `Framework.Editor`）—— 与玩法无关的通用框架：
    - `Global.cs` —— 服务定位器。业务层通过 `Global.Get<T>()` / `Global.Register<T>()` / `Global.RegisterDataProxy<T>()` 取子系统，**不要直接引用 `AppCore`**。另有 `Global.Load<T>(path)`（返回 `AssetHandle`）、`Instantiate/InstantiateUnpooled`、`Release`。
    - `GlobalConstants.cs` —— 场景名与预制体地址：`MENU_SCENE_NAME = "MainScene"`、`GAME_SCENE_NAME = "GameScene"`、`PlayerPrefabLocation = "Prefab_Player"`、`PLAYER_SPAWN_NAME = "[PlayerSpawn]"`。
    - `Common/` —— `Singleton/`（`MonoSingleton`，子类覆写 `Init()`／`Destroy()`，**不要自己再写 `Awake`**；`ScriptableObjectSingleton` 用 `Resources.Load<T>(typeof(T).Name)` 取 `Resources/<类名>.asset`）、`StateMachine/`、`SubSystemManager/`（`ISubSystem`、`SubSystemBase`、`SubSystemPriority`、`SystemManager`）。
    - `Event/EventBus.cs`；`Input/`（`LocalInputManager`、`LocalInputProvider`、`InputState`、`IInputStateProvider`、`LocalInputType`；`LocalInputManager.SwitchMap(LocalInputType)` 切映射，内部 `GameObject.Find("[GameRoot]")`）。
    - `SubSystems/` —— 各子系统实现：`Audio/`（接 Wwise）、`Camera/`、`DataProxy/`、`Instantiation/`（`InstantiateManager`，partial 拆分 `.Async` / `.Debug` / `.Entries` / `.Pool` / `.Recycle`）、`ObjectPool/`、`Resource/`、`SceneControl/`、`Storage/`（JSON 存档）、`Timer/`、`UI/`（`UIManager` + `Core/` `Panel/` `Window/` `Scene/` 三层 + `UIAnimation/`：`AnimationView`、`AnimComponent`、`FadeAnimation`）。
  - `GamePlay/` —— 玩法层：
    - `Procedure/`（`namespace GamePlay.Procedure`）—— **会话层**流程状态机。`ProcedureCore`（`MonoSingleton`，挂在 MainScene 的 `[GameRoot]` 上）持有 `StateMachine<ProcedureState>`：`Menu` / `Loading` / `Playing`。对外 `StartUp()`、`RequestPlay()`、`RequestMenu()`、`RequestState(...)`、`RequestScene(...)`、`ClearNewGameTag()`、`FailTransition(...)`。**换场景只能走 `RequestPlay()` / `RequestMenu()`，绝不要直接调 `Global.LoadScene`**。
    - `Session/`（`namespace GamePlay.Session`，惟 `GameplayRoot.cs` 是 `namespace GamePlay`）—— 一局对局的上下文：`GameplayRoot`（`SubSystemBase`）、`StoryContext`、`StoryStage`、`BattleContext`、`DialogContext`、`GameplayState.cs`（内含 **`GameplayMode`** 枚举：FreeRoam / Dialogue / Cutscene / Battle / Pause / GameOver；文件名与类型名不一致）、`States/` 六个局内状态 + `GameplayModeStateBase`（全部在 `GamePlay.Session`；文件名与类名一致）。
    - `Configs/`（`namespace GamePlay.Configs`）—— `ConfigManager` + `ConfigBase` + `Content/`：`BattleConfig`、`BuffConfig`、`EnemyConfig`、`LiquidConfig`、`NodeConfig`、`PlayerConfig`、`StoryConfig`。详见下方「配置系统」。
    - `Interaction/`（`namespace GamePlay.Interaction`）—— 交互层：`IInteractable`、`AbsorbSource`（吸取敌人尸体）、`FlowTrigger`（流程触发器 / 遭遇锚点）、`InteractionPrompt`（F 提示，跟随 `PromptAnchor`）。
    - `Player/`（`namespace GamePlay.Player`）—— `PlayerController`（输入入口 + 编排）、`PlayerCombat`、`PlayerLiquids`、`PlayerStats`、`PlayerInteractor`（`OverlapCircleNonAlloc` 扫交互物，半径 1.5，`_interactMask` = 2048）、`PlayerView`（**唯一**持有 Animator / SpriteRenderer 的表现层）。
    - `Liquid/`（`namespace GamePlay.Liquid`）—— `LiquidBase`（纯 C# 抽象基类，管技能时序：`TryUseSkill` / `TickSkill` / `ForceEndSkill` / `CanUseSkill` / `OnPassiveTick` / `OnSkillBegin/Tick/End` / `OnSkillShouldEnd`）、`LiquidFactory`、`Liquids/`（`Cola`、`GuoJiao`、`Leibi`、`SanQuan`、`StandbyPassiveLiquid`）。`LiquidConfig` 里另有 `LiquidKind`（`Active` / `MainPassive` / `StandbyPassive`）。
    - `Enemy/`（9 个 `.cs`）—— `EnemyController`、`EnemyMoveController`（封 `NavMeshAgent`）、`EnemyStates` / `EnemyBaseState` / `EnemyRealState`、`EnemyDetectZone`、`EnemyAtkDetect`、`EnemyAttackController`。**注意 `EnemyDetectZone`、`EnemyAtkDetect`、`EnemyMoveController`、`EnemyAttackController`、`EnemyRealState` 在 `<global>` 命名空间**，`EnemyController`、`EnemyStates` 等在 `GamePlay.Enemy`。
    - `Buff/`（4 个）、`Obstacle/`（5 个）—— **这两个目录全部是 `<global>` 命名空间**（既不是 `Framework` 也不是 `GamePlay.*`）。
    - `Dialog/` —— `CharacterDatabase`、`CharacterEntry`、`DialogController`（`SetDialogContent(TextAsset)`）。**三个文件全部是 `<global>` 命名空间**（不是 `GamePlay.DataProxy`，别按目录猜）。
    - `GameSave/`（`namespace GamePlay.GameSave`）、`DataProxy/`（`namespace GamePlay.DataProxy`）。
    - 根目录散着三个文件：`ScreenFader.cs`（黑屏转场，`namespace GamePlay`）、`TestEnemySpawner.cs`（临时测试）、`test.cs`（临时）。
  - `UI/`（`namespace UI`）—— 界面按 `Binding/`（自动生成）、`Logic/`（`partial class`，手写）、`Properties/`（对外参数）三层拆分，按场景分子目录。绑定代码由 `Assets/Scripts/Editor/UIFramework/UIViewBindingEditor.cs` 在 Inspector 中生成；新增界面请沿用该拆分并保留 `// <ui-bind:add>` 标记块。
  - `Editor/`（`namespace Framework.Editor`）—— `InstantiateManagerDebugWindow`、`ProtoCompilerWindow`、`UIViewBindingEditor`。
- `Assets/Scenes/` —— `MainScene.unity`（菜单，**启动场景**：`[GameRoot]`、`WwiseGlobal`、`Global Volume`、`Light 2D`）与 `GameScene.unity`（对局：`[SceneRoot]`、`WwiseGlobal`、`Ground`、`BK`、`Navigation`、`Items`、`[Chicken]`、`Light 2D` 及场景道具）。两个场景都在 Build Settings 且不进 YooAsset。`[GameRoot]` 由 `GameObject.Find` 定位，**改名会断掉 `LocalInputManager` 与 `InstantiateManager`**。
- `Assets/Configs/` —— 配置资产：`Player/`(1) `Enemy/`(9) `Battle/`(7) `Liquid/`(6) `Node/`(24) `BuffEffect/`(1) `CharacterDataBase_Dialog/`(1) `Text/`(5 个对话 `.txt`)。
- `Assets/Prefabs/` —— `Cameara/`、`Player/Player.prefab`、`Enemy/`（6 个：`Enemy_Water` / `Enemy_GuoJiao` / `Enemy_LeiBi` / `Enemy_Capsicol` / `Enemy_CookingWine` / `Enemy_OrangeJuice`，每个都带 `EnemyController` + `EnemyMoveController` + `AbsorbSource` + `EnemyAttackController` + `EnemyDetectZone`×2 + `EnemyAtkDetect`，子物体 `MoveCollider` / `Hitbox` / `Detect` / `AttackZone` / `Attack` / `InteractPoint`）、`GameScene/`、`Scene/`、`Text/`、`UI/`。
- `Assets/Art/`、`Assets/Animations/`、`Assets/ThirdParty/`（**视为只读**）、`Assets/Plugins/`（空）、`Assets/Audio/`（空占位）、`Assets/Editor/SpineSettings.asset`（Spine 残留，工程内无 Spine 运行时，不要被误导）。
- `Assets/Resources/` —— 7 个 `ScriptableObject` 单例：`AppCoreConfig`、`AudioConfig`、`CameraConfig`、`ConfigManager`、`ResGroupPolicy`、`YooAssetSettings`、`BundleCollectorSetting`。**Resources 下的内容会被无条件打进包，且不经 YooAsset 管理。**
- `Taptap_Wwise/` —— Wwise 作者工程；`Assets/Wwise/` 是插件本体（自带 8 个 asmdef），由安装器生成，**不要手改**。
- `docs/` —— 策划文档 `空瓶骑士_01流程` / `_02敌人` / `_03规则` / `_04剧情` / `_05数值`（5 个 `.docx`）+ `美术需求清单.xlsx`。
- `Library/`、`Temp/`、`obj/`、`Logs/`、`UserSettings/`、`Bundles/`、`Build/` 是生成目录，已被 `.gitignore` 忽略。

## 节点驱动的对局流程

**整个对局是单场景（`GameScene`），中途不加载场景**，靠 24 个 `NodeConfig` 串成一条链推进。

- 起始节点由 `ConfigManager._startNodeId`（默认 `Node_0`）指定，`GameplayRoot.StartNewGame()` 调 `TryGetStartNode()` 取。
- 推进链路：
  ```
  GameplayModeStateBase.CheckStateChange()
    if (Finished && AutoAdvanceOnFinish) { Finished = false; Root.AdvanceNode(); }
  AdvanceNode()        → StoryContext.TryMoveNext() → EnterNode(next)
  EnterNode()          → [node.fadeTransition ? ScreenFader.Play(...) : 直接] → EnterNodeImmediate()
  EnterNodeImmediate() → StoryContext.JumpTo() + ApplyNodeContext() + ChangeMode(node.mode)
  ```
- ⚠️ **`TryMoveNext()` 有副作用（会写 `CurrentNode`）**。所以 `AdvanceNode()` 和 `EnterNode()` 都用 `_transitioning` 挡重复请求 —— 黑屏转场要 0.95 秒，期间旧状态每帧都会 `MarkFinished()` 再请求推进，没有守卫就会**一次把整条链走穿**。
- ⚠️ `MarkFinished()` 只置一个布尔；必须同时 `AutoAdvanceOnFinish == true` 才会真的推进。重写 `CheckStateChange()` 时**一定要调 `base.CheckStateChange()`**，否则表现为「打赢了但切不出去」。
- **目标信号**：`GameplayRoot.ReportGoalReached(goalId)` → `OnGoalReached` 事件。`GameplayFreeRoamState` 订阅后只认与 `CurrentNode.goalId` 匹配的报告（节点 `goalId` 为空则接受任意报告）。报告方只有两个：`FlowTrigger`（场景触发器）和 `AbsorbSource`（吸取，报 `"absorb"`）。
- **场景里 `FlowTrigger._goalId` 填的是「要进入的那个节点的 Id」**（如 `Node_4`），不是节点自己的 Id；对应地，该 FreeRoam 节点的 `goalId` 必须等于下个节点的 Id。

### StoryStage 与尸体回收

`StoryStage`：`Fridge=0` / `Kitchen=1` / `Road=2`（未用）/ `Supermarket=3`。节点按阶段分组：`Node_0`~`Node_7` 是 Fridge，`Node_8`~`Node_21` 是 Kitchen，`Node_22` 是 Supermarket。

战斗中「可吸取」的敌人尸体在 `GameplayBattleState.Exit()` 时移入 `_pendingCorpses`，**只在 `node.currentStage` 变化或结束对局时**才 `ReleasePendingCorpses()`。**不能每个节点都回收** —— 否则「战斗 → 战败对话 → 吸取环节」中间那一步就把尸体清掉了，玩家还没走到就没了。

### 遭遇（encounter）

场景里放占位贴图 + `FlowTrigger`（**Layer 必须是 `Obstacle`**，勾 `_asEncounterAnchor`）→ 触发后报 `_goalId`，同时 `FlowTrigger.RegisterEncounter()` 调 `Root.SetEncounter(占位物, 刷怪位置列表)` → 节点推进到对话 → 对话结束进战斗节点 → `GameplayBattleState.SpawnEnemy()` 按序号在各刷怪位置生成敌人 → `Root.HideEncounterPlaceholder()` 隐藏整组占位贴图 → `Root.ClearEncounter()`。这样对话期间场上没有敌人（不会隔着对话框挨打），且敌人是门面实例化的，`Global.Release` 合法。

**刷怪位置规则**：`FlowTrigger._spawnPoints` 有填就用填的；留空则用「自己 + 所有子物体」—— 也就是把多余的占位贴图拖成子物体，一张贴图 = 一个刷怪位置。因为隐藏的是父物体，子物体里的贴图会一起消失。敌人数量超过位置数量时，多出来的围绕最后一个位置各偏移 1.5 单位，避免叠在一起。

⚠️ **NavMeshAgent 陷阱**：直接 `go.transform.position = anchor.position` 会被 Agent 覆盖 —— 敌人会回到预制体保存的坐标（所有敌人预制体根节点都是 `(0,0)`），表现为「BOS 生成在场景原点、尸体不在死亡地点」。必须 `agent.enabled = false` → 改位置 → `agent.enabled = true`。

### 黑屏转场

`ScreenFader`（`MonoSingleton<ScreenFader>`，挂在「Canvas + 全屏黑色 Image」那个物体上，`SortingOrder` 要高于 `UIManager` 的 0）。`NodeConfig.fadeTransition` 勾上即触发，目前只有 `8b_传送进厨房`（传送带→厨房）和 `22_进入超市`（厨房→超市）。

`NodeConfig.teleportTargetName` 填一个场景物体名（如 `[KitchenSpawn]`），**在全黑之后**把主角传送到该物体处；留空则不传送。必须挂在带 `fadeTransition` 的节点上，否则玩家会看到瞬移。

⚠️ **只用 alpha 控制显隐，不要 `SetActive`** —— 脚本和黑幕 Image 在同一物体上时，`SetActive(false)` 等于关掉自己，之后 `StartCoroutine` 直接报 "game object is inactive"。黑幕 Image 的 `Raycast Target` 必须取消。

## 配置系统

- `ConfigManager`（`ScriptableObjectSingleton`，资产在 `Assets/Resources/ConfigManager.asset`）按类型分 7 个 `List`：主角 / 敌人 / 战斗 / 液体 / Buff / 剧情 / 流程节点。`OnEnable` 建 `Dictionary<Type, Dictionary<Id, ConfigBase>>`，**重复 Id 只在同类型内报错**。
- 对外 API（`ConfigManager.Instance`）：
  - `bool TryGetConfig<T>(string id, out T cfg) where T : ConfigBase` —— 按类型 + Id 查
  - `bool TryGetNode(string nodeId, out NodeConfig node)`
  - `bool TryGetStartNode(out NodeConfig node)`
- **Id 命名规范**：`Player_001` / `Enemy_00N_Name` / `Battle_00N_Name` / `Liquid_00N_Name` / `Node_N`。资产文件名与 `Id` 保持一致，且**资产内部 `m_Name` 必须和文件名一致**（否则 Unity 弹黄框）。
- `ConfigBase` 的 `Id` 带 `[FormerlySerializedAs("nodeId")]` —— `NodeConfig` 原来叫 `nodeId`。
- **引用规则按「是否跨 YooAsset 组」划界**：
  - **同组内 config → config 用直接资源引用**（Inspector 拖拽、不会打错、改名自动跟着走）：`NodeConfig.battleConfig`（→ `BattleConfig`）、`BattleConfig.Enemies[].Config`（→ `EnemyConfig`）、`EnemyConfig.AbsorbLiquid`（→ `LiquidConfig`）。这些资产同属 `Config` 组，用引用不会多拖任何东西。
  - **跨组引用用字符串地址**：`EnemyConfig.PrefabLocation`（→ `Prefab` 组的敌人预制体，YooAsset 地址形如 `Prefab_Enemy_Water`）。用引用会把预制体连同整条依赖链拖进 `Config` bundle。
- **`NodeConfig.dialogConfig` 也是直接引用**（`TextAsset`）：对话 txt 与节点资产同在 `Config` 组，不产生跨组依赖，且只有 200–700 字节；改成地址反而要处理 `AssetHandle` 的生命周期与释放时机。**前提：这 5 个 txt 必须留在 `Assets/Configs/Text/` 下**，挪走就会变空引用。
- 新增配置类型的步骤：类继承 `ConfigBase` → `ConfigManager` 加一个 `List<T>` 字段和一行 `AddGroup(...)` → 把资产拖进 Inspector 的对应列表。**拖漏了运行时只会打一条 `[ConfigManager] 找不到 XXX：id`，功能静默失效**（例如漏拖 `PlayerConfig` 会让 `PlayerController` 直接 `enabled = false`）。

## 层与碰撞矩阵

层（索引）：0 Default、5 UI、6 Player、7 PlayerHitbox、8 Enemy、9 EnemyHitbox、10 Obstacle、11 Interactable、12 PlayerBullet、13 EnemyBullet。**`Interactable` 的 LayerMask 位是 2048。**

碰撞矩阵里 **`Interactable` 和谁都不碰**。`PlayerInteractor` 用 `Physics2D.OverlapCircleNonAlloc`（查询只看 mask，不看矩阵）所以能扫到 `InteractPoint`；但**任何依赖 `OnTriggerEnter2D` 的东西必须放在 `Obstacle` 层**。`Enemy × Player` 已勾上（`EnemyDetectZone` 靠它认玩家）。

## 已知现状 / 待办

- **战斗还没真打**：`NodeConfig.autoWinBattle` 打开时，敌人生成 0.4 秒后被 `ForceDefeated()` 判定死亡。等真战斗做完，逐个取消勾选并删掉这个字段以及 `GameplayBattleState` 里的【临时】区块。
- **7 个战斗节点的 `battleConfig` 都已接上**（`Battle_001_Water` / `002_GuoJiao` / `003_Arena1`~`006_Arena4` / `007_Cola`），且 `autoWinBattle` 全部勾上。
- **`EnemyConfig` 现有 9 个**：水 / 国窖 / 雷碧 / 辣椒 / 料酒 / 酱油 / 可乐 / 醋 / 橙汁。**酱油、醋、橙汁的 `CanAbsorb = 0`**（没有对应的 `LiquidConfig`，留着会卡住吸取环节）。
- **辣椒、橙汁的数值和吸取配置都是占位**；矿泉水 `CanAbsorb = 0`（杂兵不产液体，否则玩家会在 `7_吸取液体环节` 吸到矿泉水尸体、拿错液体）。
- **`GameplayPauseState` / `GameplayCutsceneState` / `GameplayGameOverState` 是空壳**（只有构造函数）；`GameplayRoot.EnterBattle()` / `EndBattle()` / `Resume()` 目前零调用。
- **`ContinueGame()` 不可用**：里面是 `Resources.Load<NodeConfig>("")`，永远返回 null；`GameSaveData` 只有 `PlayerPos` 一个字段。
- **`GameplayRoot` 职责过载**（约 280 行，管对局生命周期 + 模式状态机 + 节点推进 + 黑屏转场 + 刷怪锚点 + 目标信号）。后续可把节点推进 / 转场 / 目标信号抽成独立的 `NodeFlow`。**等战斗接完再拆。**
- **临时调试代码仍在**（有意保留）：`PlayerInteractor` 的 `[交互] 候选/提示`、`PlayerCombat` 的 `[技能] 释放失败`、`GameplayBattleState` 的 `[战斗清理]`、`GameplayRoot` 的 `[阶段]` 与 `Player` 属性、`TestEnemySpawner.cs`、`test.cs`。清理时一起删。
- **大量表现层还是空的**：`AbsorbSource.PlayAbsorbPresentation()`、`Obstacle_*` 的 `OnPlayerHit()`、`PlayerController.OnSprint()`、`GuoJiao` 技能的火伤 / 减速 / 击退、`PlayerCombat.SearchForEnemy()`（返回 `Vector2.zero`）、`DirectionalAttack` 忽略朝向与枪口。

## 注意事项

- **没有项目自有的 `.asmdef`**：`Assets/Scripts/` 全部编译进单个 `Assembly-CSharp`（`/Editor/` 进 `Assembly-CSharp-Editor`）；`Assets/Wwise/**` 自带 8 个 asmdef 除外。不要随意新增 asmdef。
- **编码与换行**：`Assets/Scripts/` 下 164 个 `.cs` **全部是 UTF-8 + CRLF**（0 个裸 LF，无 GBK）。其中 **23 个带 BOM**（Unity 自己生成的那批，如 `Framework/Global.cs`、`Gameplay/Session/GameplayRoot.cs`、`Configs/Content/NodeConfig.cs`、`Enemy/EnemyController.cs`、`Player/PlayerController.cs` 等）。**硬性要求只有一条：不能是 GBK（cp936）** —— 那会让中文注释在 UTF-8 工具链下乱码。BOM 有无都可以，**不要**为了统一 BOM 去做全仓批量改动，会产生巨大无意义 diff。非 `.cs` 资产的 EOL / BOM 未统一，同样不要顺手做全文件 EOL 转换。
- `*.sln` / `*.csproj` 由 Unity 生成，**且已被 `.gitignore` 忽略**（未跟踪），只能由编辑器重新生成，**绝不手改**。`.vscode/settings.json` 的 `dotnet.defaultSolution` 仍指向旧仓库名，已失效。
- **`.asset` / `.prefab` / `.unity` 的 YAML 只有在 Unity 关闭时才能安全手改** —— Unity 开着会用它内存里的版本覆盖回去。
- `.meta` 必须与资产一起提交；**重命名资产时 `.asset` 与 `.asset.meta` 必须同步改**，只有文件名和 `m_Name` 都一致才不会弹框。**不要手改任何文件里的 GUID。**
- **Wwise 配置与本机强绑定**：`Assets/WwiseSettings.xml` 里 `WwiseInstallationPathWindows` 是机器绝对路径（当前值在本机不存在，换机必须重设）；`WwiseProjectPath=../Taptap_Wwise/Taptap_Wwise.wproj`；WAAPI 端口 `8081`；`CopySoundBanksAsPreBuildStep=true`（SoundBank 已提交在 StreamingAssets，所以运行期音频不受路径失效影响）。
- 代码注释与文档沿用中文（中文 XML 注释 + `#region` 分区）。
- **这是一个 git 仓库** —— 两个远程：`origin` = https://github.com/V1vy-v/2026-TapTap-Spotlight-GameJam-Project.git，`gitee` = https://gitee.com/hyyye/2026-TapTap-Spotlight-GameJam-Project.git；默认分支 `main`。没有 CI、没有测试基础设施。破坏性 git 操作（reset、强推、回退、丢弃未提交改动）必须先经用户同意。
- `docs/` 下有 5 个 `~$*.docx` 锁文件（Word 打开时生成的），**不要提交**。`Temp/UnityLockfile` 也可能是崩溃后的残留锁文件，导致下次打开 Unity 提示锁定。
- `.gitignore` 里的 `.curosr/` 是 `.cursor/` 的拼写错误，**`.cursor/` 实际未被忽略**。

## 中文文本

- 字体源文件 `Assets/Art/Fonts/AaHouDiHei-2.ttf`：Dynamic 导入、`Include Font Data` 已开，覆盖 GB2312 全集 6763 汉字 —— 项目现有文案零缺字；GB2312 之外的字（生僻字、繁体）会缺。
- 制作 TMP 字体资产：右键 `.ttf` → `Create > TextMeshPro > Font Asset` 得到 **Dynamic** 资产，务必勾上 `Multi Atlas Textures`（中文必需）；`Sampling Point Size` ≥ 40（约 60）、`Padding` ≈ 点大小 × 0.1、`Render Mode` 用 `SDFAA`。
- 字体资产在 UI 预制体上引用即可（作为依赖随 `UI` 组 bundle 走）；**不要**轻易设成 `TMP Settings` 的默认字体 —— 那会让它走 `Resources` 无条件进包。

## AI 协作环境

- 当前通过 **DeepSeek Harness（DSH）** 在本工程下工作，本文件即工作区指令。
- 该工程同时注册在 Codex 桌面端：`~/.codex/config.toml` 中 `model_provider = "opencode-go"`，`base_url = "https://opencode.ai/zen/go/v1"`（`wire_api = "responses"`），凭据取自环境变量 `OPENCODE_GO_API_KEY`；`model = "deepseek-flash"`；`[mcp_servers.unityMCP]` 注册了 Unity MCP。`trust_level = "trusted"`。
- Unity Editor 安装位置（手动注册在 Unity Hub）：`D:\Programdata\UnityHub\Editor\Unity2022.3\Editor\Unity.exe`（2022.3.62f3）。
