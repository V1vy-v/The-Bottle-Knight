using Framework;
using System.Collections.Generic;
using GamePlay.Configs;
using GamePlay.Enemy;
using GamePlay.Session;
using GamePlay.Story;
using System;
using UnityEngine;
using YooAsset;

namespace GamePlay
{
    public sealed class GameplayRoot : SubSystemBase
    {
        public override int Priority => (int)SubSystemPriority.GameplayRoot;

        private StateMachine<GameplayMode> _modeFsm;
        private StoryContext _storyContext;
        private BattleContext _battleContext;
        private DialogContext _dialogContext;
        private GameplayMode _resumeMode = GameplayMode.FreeRoam;
        private GameObject _player; 
        private GameplayBattleState _battleState;

        public StoryContext Story => _storyContext;
        public DialogContext Dialog => _dialogContext;
        public BattleContext Battle => _battleContext;
        public GameplayMode CurrentMode => _modeFsm?.CurrentState ?? default;

        /// <summary>当前局内的主角实例（可能为空：未生成或已销毁）</summary>
        public GameObject Player => _player;

        #region 生命周期
        public override void Update(float deltaTime)
        {
            _modeFsm?.Update(deltaTime);

            // Esc：打开 / 关掉菜单（Esc 没进 PlayerInputActions，直接读键盘）
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (CurrentMode == GameplayMode.Pause) Resume();
                else Pause();
            }
        }
        public override void FixedUpdate(float fixedDeltaTime) => _modeFsm?.FixedUpdate(fixedDeltaTime);
        public override void LateUpdate() => _modeFsm?.LateUpdate();
        #endregion

        #region 对局生命周期

        /// <summary>
        /// 开始新游戏。头节点由外部传入。
        /// </summary>
        public void StartNewGame()
        {
            TutorialTips.Clear();
            ResetProps();      // 道具的「已购买」存在 SO 上，会跨局残留，新开一局先清掉
            GamePlay.Enemy.EnemyController.ResetCapRewards();   // 瓶盖奖励按「一局一次」重新算
            var configs = ConfigManager.Instance;
            if (configs == null || !configs.TryGetStartNode(out NodeConfig startNode))
            {
                Debug.LogError("[GameplayRoot] 找不到起始节点，检查 ConfigManager 的起始节点 Id");
                return;
            }

            _storyContext = new();
            _battleContext = new();
            _dialogContext = new();

            _storyContext.Reset(startNode);

            ApplyNodeContext(startNode, isFirstNode: true);

            BuildModeFsm();
            SpawnPlayer();

            // 起始节点配了传送点就立刻传送（跳过前面流程时必须）
            if (!string.IsNullOrEmpty(startNode.teleportTargetName))
                TeleportPlayer(startNode.teleportTargetName);

            _modeFsm.ChangeState(startNode.mode);

            // 首个节点不走 EnterNodeImmediate，提示要在这里单独补一次
            TutorialTips.Show(startNode.tipText);
            Global.Get<CameraManager>()?.SetTarget(_player.transform);
        }

        /// <summary>
        /// 清掉道具的「已拥有」标记。
        /// PropConfig 是 ScriptableObject，isHold 写在资产上，通关回菜单再开新局时会残留，
        /// 表现为「一开局 HUD 上就有上一局买齐的道具」。新局开始重置一次即可。
        /// </summary>
        private static void ResetProps()
        {
            PropConfigs props = ConfigManager.Instance != null ? ConfigManager.Instance.Props : null;
            if (props == null) return;

            for (int i = 0; i < props.allProps.Count; i++)
                if (props.allProps[i] != null) props.allProps[i].isHold = false;
        }

        /// <summary>
        /// 读档继续。存档节点由外部传入。
        /// </summary>
        public void ContinueGame()
        {
            NodeConfig savedNode = Resources.Load<NodeConfig>("");
            if (savedNode == null)
            {
                Debug.LogError("[GameplayRoot] ContinueGame 传入的 savedNode 为空");
                return;
            }

            _storyContext = new();
            _battleContext = new();
            _dialogContext = new();

            _storyContext.Reset(savedNode);

            ApplyNodeContext(savedNode, isFirstNode: true);

            BuildModeFsm();
            SpawnPlayer();

            _modeFsm.ChangeState(savedNode.mode);

            TutorialTips.Show(savedNode.tipText);
        }

        public void EndGame()
        {
            if (_modeFsm != null)
            {
                _modeFsm.OnStateChange -= OnModeChanged;
                _modeFsm.Stop();
                _modeFsm = null;
            }

            _battleState?.ReleasePendingCorpses();   // 结束对局，尸体全清
            _battleState = null;

            if (_player)
            {
                Global.Release(_player);
                _player = null;
            }

            _storyContext = null;
            _battleContext = null;
            _dialogContext = null;
        }

        /// <summary>取场景里的全局出生点；找不到就退回预制体自身坐标</summary>
        private static InstantiateOptions PlayerSpawnOptions()
        {
            GameObject marker = GameObject.Find(GlobalConstants.PLAYER_SPAWN_NAME);
            if (marker == null)
            {
                Debug.LogWarning($"[GameplayRoot] 场景里找不到 {GlobalConstants.PLAYER_SPAWN_NAME}，主角落在预制体自身坐标");
                return new InstantiateOptions(true);
            }

            return new InstantiateOptions(true, marker.transform.position, marker.transform.rotation);
        }

        /// <summary>生成主角并交给相机。StartNewGame / ContinueGame 共用，避免两处漂移</summary>
        private void SpawnPlayer()
        {
            _player = Global.InstantiateUnpooled(
                GlobalConstants.PlayerPrefabLocation,
                PlayerSpawnOptions());

            Global.Get<CameraManager>()?.SetTarget(_player.transform);
        }

        #endregion

        private void BuildModeFsm()
        {
            _modeFsm = new StateMachine<GameplayMode>();
            _modeFsm.OnStateChange += OnModeChanged;
            _modeFsm.RegisterState(new GameplayFreeRoamState(this, _modeFsm));
            _modeFsm.RegisterState(new GameplayDialogueState(this, _modeFsm));
            _modeFsm.RegisterState(new GameplayShopState(this, _modeFsm));
            _modeFsm.RegisterState(new GameplayCutsceneState(this, _modeFsm));

            // 留住引用，好在阶段推进 / 结束对局时回收暂留尸体
            _battleState = new GameplayBattleState(this, _modeFsm);
            _modeFsm.RegisterState(_battleState);

            _modeFsm.RegisterState(new GameplayPauseState(this, _modeFsm));
            _modeFsm.RegisterState(new GameplayGameOverState(this, _modeFsm));
        }

        #region 局内切换

        /// <summary>
        /// 正在切换到的模式。状态机是先 Exit 旧状态再进新状态，
        /// 旧状态在 Exit 里拿不到「我要去哪」，只能从这里问。
        /// </summary>
        public GameplayMode PendingMode { get; private set; }

        public void ChangeMode(GameplayMode mode)
        {
            PendingMode = mode;
            _modeFsm?.ChangeState(mode);
        }

        /// <summary>
        /// 进入新节点时用：即使模式没变也强制重入一次状态。
        /// 否则「对话 → 对话」这种连续同模式节点会沿用旧状态，新节点内容不会刷新（表现为卡在上一段对话）。
        /// </summary>
        public void ChangeModeForNode(GameplayMode mode)
        {
            PendingMode = mode;
            _modeFsm?.RestartState(mode);
        }

        /// <summary>暂停。只在自由行动与战斗中允许。</summary>
        public void Pause()
        {
            if (CurrentMode is GameplayMode.FreeRoam or GameplayMode.Battle)
                _modeFsm.ChangeState(GameplayMode.Pause);
        }

        /// <summary>恢复到暂停前的模式（而不是硬编码回 FreeRoam）。</summary>
        public void Resume() => _modeFsm?.ChangeState(_resumeMode);

        private void OnModeChanged(GameplayMode from, GameplayMode to)
        {
            if (to == GameplayMode.Pause && from != GameplayMode.Pause)
                _resumeMode = from;

            // 对话 / 商店 / 暂停 / 结算期间玩家动不了，敌人也必须停手 ——
            // 否则超市里巡逻怪会在看对话时把人打死。
            bool freeze = to is GameplayMode.Dialogue or GameplayMode.Shop
                             or GameplayMode.Pause or GameplayMode.GameOver or GameplayMode.Cutscene;
            SetAllEnemiesPaused(freeze);
        }

        /// <summary>统一冻结 / 解冻场上所有敌人的 AI（尸体不受影响，渐隐照常播）</summary>
        private static void SetAllEnemiesPaused(bool paused)
        {
            EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
                enemies[i].SetAiPaused(paused);
        }

        #endregion

        #region 剧情推进（单场景，不走场景加载）        
        /// <summary>交互物 / 触发器宣告「本节点目标已完成」，由当前模式状态决定是否推进</summary>
        public event Action<string> OnGoalReached;

        /// <summary>本节点目标已达成</summary>
        public void ReportGoalReached(string goalId = null) => OnGoalReached?.Invoke(goalId);
        /// <summary>本次遭遇的占位物（战斗开始时整组隐藏）。为空表示没有占位贴图</summary>
        private GameObject _encounterPlaceholder;

        /// <summary>本次遭遇的刷怪位置，按顺序分配给敌人。为空则用预制体自身坐标</summary>
        private readonly List<Transform> _encounterAnchors = new();

        public IReadOnlyList<Transform> EncounterAnchors => _encounterAnchors;

        /// <summary>
        /// 登记一次遭遇。placeholder 是战斗开始时整组隐藏的占位物（一般是那组占位贴图的父物体），
        /// anchors 是刷怪位置（一张占位贴图一个位置），按顺序分配给敌人。
        /// </summary>
        public void SetEncounter(GameObject placeholder, IReadOnlyList<Transform> anchors)
        {
            _encounterPlaceholder = placeholder;
            _encounterAnchors.Clear();

            if (anchors == null) return;
            foreach (var a in anchors)
                if (a != null && !_encounterAnchors.Contains(a)) _encounterAnchors.Add(a);
        }

        /// <summary>隐藏本次遭遇的占位物（战斗开始时调用）</summary>
        public void HideEncounterPlaceholder()
        {
            HidePlaceholder(_encounterPlaceholder);
        }

        /// <summary>
        /// 只关渲染，不要 SetActive(false)：占位物自己往往就是触发器，
        /// 关掉 GameObject 会让它不再响应 OnTriggerEnter2D —— 擂台要打 4 轮就会卡在第 2 轮。
        /// </summary>
        public void HidePlaceholder(GameObject placeholder)
        {
            if (placeholder == null) return;

            foreach (var sr in placeholder.GetComponentsInChildren<SpriteRenderer>(true))
                sr.enabled = false;
        }

        /// <summary>清掉本次遭遇的登记，避免下一场复用</summary>
        public void ClearEncounter()
        {
            _encounterPlaceholder = null;
            _encounterAnchors.Clear();
        }

        /// <summary>转场进行中：挡住旧状态每帧重复请求推进</summary>
        private bool _transitioning;

        /// <summary>上一次回收暂留尸体时的剧情阶段</summary>
        private bool _stageInitialized;
        private StoryStage _releasedStage;

        /// <summary>推进到下一个节点。</summary>
        public void AdvanceNode()
        {
            if (_transitioning) return;      // 转场中：旧状态可能每帧请求推进，必须挡住

            if (_storyContext == null)
            {
                Debug.LogWarning("[GameplayRoot] StoryContext 为空，无法推进");
                return;
            }

            if (!_storyContext.TryMoveNext(out var next))
            {
                // 最后一个节点：标了 endOfStory 就收尾回主菜单
                if (_storyContext.CurrentNode != null && _storyContext.CurrentNode.endOfStory)
                {
                    EndStory();
                    return;
                }

                Debug.Log("[GameplayRoot] 节点流程已到末端");
                return;
            }

            EnterNode(next);
        }

        /// <summary>直接进入指定节点</summary>
        public void EnterNode(NodeConfig node)
        {
            if (node == null)
            {
                Debug.LogWarning("[GameplayRoot] EnterNode 传入空节点");
                return;
            }

            if (_transitioning) return;

            Debug.Log($"切换节点至: {node.name}");

            // 需要黑屏转场的节点：等黑幕全黑之后再传送 + 真正切换，玩家看不到瞬移
            if (node.fadeTransition && ScreenFader.Instance != null)
            {
                _transitioning = true;
                ScreenFader.Instance.Play(() =>
                {
                    _transitioning = false;
                    TeleportPlayer(node.teleportTargetName);
                    EnterNodeImmediate(node);
                });
                return;
            }

            // 没有黑屏转场的节点不传送 —— 中途强行拉玩家位置会看到瞬移。
            // 起始节点的传送由 StartNewGame 单独处理（方便跳过前段直接测某个节点）。
            EnterNodeImmediate(node);
        }

        /// <summary>上一个节点（进节点时记录，用于「刚离开吸取节点」这类判断）</summary>
        private NodeConfig _previousNode;

        /// <summary>上一个节点是不是「等吸取」节点</summary>
        public bool PreviousNodeWasAbsorb => _previousNode != null && _previousNode.goalId == "absorb";

        /// <summary>真正的切换：刷 Context + 切模式</summary>
        private void EnterNodeImmediate(NodeConfig node)
        {
            _previousNode = _storyContext?.CurrentNode;

            _storyContext?.JumpTo(node);

            ApplyNodeContext(node, isFirstNode: false);

            ChangeModeForNode(node.mode);

            // 拿到吸管：解锁远战模组（Q 才能真正切换）
            if (node.unlockRangedAttack && _player != null)
                _player.GetComponent<GamePlay.Player.PlayerCombat>()?.UnlockRangeModule();

            // 节点的教学提示
            if (!string.IsNullOrEmpty(node.tipText))
                TutorialTips.Show(node.tipText);      // 进队列，避免被下一条瞬间顶掉

            // 战斗节点由 GameplayBattleState 自己切战斗曲，其它节点按阶段放 BGM
            if (node.mode != GameplayMode.Battle)
                PlayStageBgm();

            SaveCheckpoint(node);

            // 战斗节点记下开打位置，战败重试就地从这里重开这一场
            if (node.mode == GameplayMode.Battle && _player != null)
                _battleEntryPos = _player.transform.position;
        }

        /// <summary>
        /// 按当前剧情阶段播放背景音乐。
        /// 战斗结束后也靠它把 BGM 从战斗曲切回来。
        /// </summary>
        public void PlayStageBgm()
        {
            NodeConfig node = _storyContext?.CurrentNode;
            string key = node == null ? "BGM_Kitchen" : node.currentStage switch
            {
                StoryStage.Fridge => "BGM_Tutorial",
                StoryStage.Supermarket => "BGM_stealth",
                _ => "BGM_Kitchen",
            };

            var audio = Framework.Global.Get<Framework.SubSystems.AudioManager>();
            audio?.PlayBGM(key);

            // 环境音和 BGM 分层叠放（冰箱冷气 / 厨房市井 / 超市空调）
            string ambience = node == null ? "AMB_Kitchen" : node.currentStage switch
            {
                StoryStage.Fridge => "AMB_ColdStorage",
                StoryStage.Supermarket => "AMB_Supermarket",
                _ => "AMB_Kitchen",
            };
            audio?.PlayAmbience(ambience);
        }

        /// <summary>
        /// 把主角移到场景里的命名标记处。黑屏转场用 —— 必须在全黑之后调用，
        /// 否则玩家会看到瞬移。名字为空或找不到就跳过。
        /// </summary>
        private void TeleportPlayer(string markerName)
        {
            if (string.IsNullOrEmpty(markerName))
            {
                Debug.Log("[GameplayRoot] 本节点没配 teleportTargetName，不传送");
                return;
            }

            if (!_player)
            {
                Debug.LogWarning($"[GameplayRoot] 要传送到 {markerName}，但主角不存在");
                return;
            }

            GameObject marker = FindMarker(markerName);
            if (marker == null)
            {
                Debug.LogWarning($"[GameplayRoot] 场景里找不到传送目标 {markerName}");
                return;
            }

            Vector3 pos = marker.transform.position;

            var rb = _player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;   // 别把旧速度带过去
                rb.position = pos;            // Rigidbody2D 要用 position，直接改 transform 会和物理不同步
            }
            else
            {
                _player.transform.position = pos;
            }

            Debug.Log($"[GameplayRoot] 主角已传送到 {markerName} -> {pos}");

            // 换区域了：上一片区域登记的刷怪锚点作废，
            // 否则下一场没配锚点的战斗会刷在上一片区域（比如从下路传到冰柜区后还刷在下路）
            ClearEncounter();
        }

        /// <summary>
        /// 按名字找传送标记。
        /// GameObject.Find 只认激活中的物体；找到的是 DontDestroyOnLoad 之外的活动物体。
        /// 找不到时再扫一遍全部（含未激活），避免标记被某个未激活的父物体带着一起失活。
        /// </summary>
        private static GameObject FindMarker(string markerName)
        {
            GameObject go = GameObject.Find(markerName);
            if (go != null) return go;

            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != markerName) continue;
                if (!t.gameObject.scene.isLoaded) continue;    // 跳过预制体资产
                return t.gameObject;
            }

            return null;
        }

        /// <summary>按节点声明刷新 Context。只刷数据，不切模式。</summary>
        private void ApplyNodeContext(NodeConfig node, bool isFirstNode)
        {
            // 只在「剧情阶段」变化时回收暂留尸体。
            // 不能每个节点都回收：否则「战斗 → 战败对话 → 吸取环节」中间那一步就清掉了，
            // 玩家还没走到尸体就没了，吸取环节永远推不动。
            Debug.Log($"[阶段] {_releasedStage} -> {node.currentStage}  节点={node.name}");
            if (!_stageInitialized || _releasedStage != node.currentStage)
            {
                _battleState?.ReleasePendingCorpses();
                ClearEncounter();          // 跨阶段了，上一阶段的刷怪锚点作废
                _releasedStage = node.currentStage;
                _stageInitialized = true;
            }

            // 首个节点必须无条件刷新，否则 CurrentBattle / 对话都是空的
            if (isFirstNode || node.refreshBattle) _battleContext?.RefreshByNode(node);
            if (isFirstNode || node.refreshDialog) _dialogContext?.RefreshByNode(node);
        }

        public void EnterBattle() => ChangeMode(GameplayMode.Battle);

        public void EndBattle()
        {
            _battleContext?.Clear();
            ChangeMode(GameplayMode.FreeRoam);
        }

        #endregion

        #region 战败与结局

        /// <summary>重试请求进行中：挡住重复请求</summary>
        private bool _retrying;

        /// <summary>正在按存档点回退：此时进节点不要覆盖存档点</summary>
        private bool _restoringCheckpoint;

        /// <summary>存档点：最近一次「自由行走」节点的 Id 与当时主角位置</summary>
        private string _checkpointNodeId;
        private Vector3 _checkpointPos;

        /// <summary>进入当前战斗节点时主角的位置（战斗内阵亡就地重打，不用跑回去）</summary>
        private Vector3 _battleEntryPos;

        /// <summary>结局演出进行中</summary>
        private bool _ending;

        /// <summary>
        /// 记录存档点。只在自由行走节点记 —— 战斗中死掉要回到「进战斗之前那一步」，
        /// 比如雷碧那场，回去后要重新踩触发器才会再打一次。
        /// </summary>
        private void SaveCheckpoint(NodeConfig node)
        {
            if (_restoringCheckpoint) return;
            if (node == null || node.mode != GameplayMode.FreeRoam) return;
            if (_player == null) return;

            _checkpointNodeId = node.Id;
            _checkpointPos = _player.transform.position;
        }

        /// <summary>
        /// 主角死亡。由 PlayerController 每帧检查后上报 —— 数值组件是纯 C# 类，
        /// 拿不到子系统，所以「死了以后怎么办」统一放在这里。
        /// </summary>
        public void NotifyPlayerDead()
        {
            if (CurrentMode == GameplayMode.GameOver) return;

            Debug.Log("[GameplayRoot] 主角死亡，进入 GameOver");
            ChangeMode(GameplayMode.GameOver);
        }

        /// <summary>
        /// 战败重试：清战场 → 占位贴图复原 → 场上敌人回满血 → 触发器重新武装 → 主角复活
        /// → 黑屏 → 回到上一个自由行走的节点与位置。
        /// 这样雷碧这种「踩触发器开打」的战斗，死后必须重新踩一次才会再打。
        /// </summary>
        public void RetryFromCheckpoint()
        {
            if (_retrying) return;

            _retrying = true;

            // 1. 收掉上一场刷出来的敌人（含 BOSS 尸体）
            //    ForceClearAll 里只复位「本次战斗接管过的」剧情敌人；
            //    不要在这里无差别复活所有剧情 NPC —— 那样已经打完的矿泉水 / 国窖会重新出现。
            _battleState?.ForceClearAll();

            // 2. 占位贴图复原：开打时整组被关掉了渲染，不复原的话重打时 BOSS 是隐形的
            ShowPlaceholder(_encounterPlaceholder);
            ClearEncounter();

            // 3. 场上敌人回满血
            HealAllEnemies();

            // 4. 触发器重新武装（雷碧那场必须能再踩一次）
            RearmFlowTriggers();

            // 5. 主角复活
            _player?.GetComponent<GamePlay.Player.PlayerController>()?.Revive();

            // 6. 决定回到哪个节点：
            //    死在战斗里 → 只重打「当前这场」，前面的进度和已经打死的敌人都不重来；
            //    死在漫游里（被巡逻怪打死之类）→ 退回上一个自由行走的节点。
            NodeConfig node;
            Vector3 respawnPos;

            bool diedInBattle = _storyContext?.CurrentNode != null
                                && _storyContext.CurrentNode.mode == GameplayMode.Battle;

            if (diedInBattle)
            {
                node = _storyContext.CurrentNode;
                respawnPos = _battleEntryPos;
                Debug.Log($"[GameplayRoot] 战斗内阵亡：只重打当前这场（{node.name}），不回退前面的节点");
            }
            else
            {
                node = ResolveCheckpointNode();
                respawnPos = _checkpointPos;
            }

            if (node == null)
            {
                Debug.LogWarning("[GameplayRoot] 没有可回退的节点，直接回主菜单");
                _retrying = false;
                EndStory();
                return;
            }

            void Apply()
            {
                TeleportTo(respawnPos);

                _restoringCheckpoint = true;
                EnterNodeImmediate(node);
                _restoringCheckpoint = false;

                _retrying = false;
                Debug.Log($"[GameplayRoot] 已回退到节点 {node.name}，位置 {_checkpointPos}");
            }

            if (ScreenFader.Instance != null) ScreenFader.Instance.Play(Apply);
            else Apply();
        }

        /// <summary>兼容旧调用：现在等同于回退到存档点</summary>
        public void RetryCurrentNode() => RetryFromCheckpoint();

        private NodeConfig ResolveCheckpointNode()
        {
            var configs = ConfigManager.Instance;
            if (configs != null && !string.IsNullOrEmpty(_checkpointNodeId) &&
                configs.TryGetNode(_checkpointNodeId, out NodeConfig node) && node != null)
                return node;

            return _storyContext?.CurrentNode;
        }

        /// <summary>把场上还没倒下的敌人血量刷满</summary>
        private static void HealAllEnemies()
        {
            var enemies = UnityEngine.Object.FindObjectsByType<GamePlay.Enemy.EnemyController>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null) enemies[i].ResetToFull();
        }

        /// <summary>把所有流程触发器重置成「未触发」，让回退后能重新踩</summary>
        private static void RearmFlowTriggers()
        {
            var triggers = UnityEngine.Object.FindObjectsByType<GamePlay.Interaction.FlowTrigger>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < triggers.Length; i++)
                if (triggers[i] != null) triggers[i].Rearm();
        }

        /// <summary>恢复占位贴图的渲染（<see cref="HidePlaceholder"/> 的逆操作）</summary>
        public void ShowPlaceholder(GameObject placeholder)
        {
            if (placeholder == null) return;

            foreach (var sr in placeholder.GetComponentsInChildren<SpriteRenderer>(true))
                sr.enabled = true;
        }

        /// <summary>把主角放到指定坐标（黑屏之后调用）</summary>
        private void TeleportTo(Vector3 pos)
        {
            if (_player == null) return;

            var rb = _player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.position = pos;
            }
            else
            {
                _player.transform.position = pos;
            }
        }

        /// <summary>
        /// 终章信件「致后来者」：以弹窗显示（字号自适应），玩家按键 / 点击关闭后才收尾回主菜单。
        /// </summary>
        public void ShowEndingLetter(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                EndStory();
                return;
            }

            _awaitingLetterClose = true;

            // 信件弹出时把对话框收掉，别两层叠着
            Global.HideUI("DialogPanel");

            Global.ShowUI("TipsPanel", new UI.TipsPanelProperties
            {
                tipsText = text,
                holdSeconds = 0f,       // 不自动关，等玩家按键 / 点击
            });

            Debug.Log("[GameplayRoot] 显示终章信件");
        }

        /// <summary>信件被关闭：结束本局</summary>
        public void OnEndingLetterClosed()
        {
            if (!_awaitingLetterClose) return;

            _awaitingLetterClose = false;
            EndStory();
        }

        private bool _awaitingLetterClose;

        /// <summary>流程走到最后一个节点（NodeConfig.endOfStory）：黑屏回主菜单。</summary>
        public void EndStory()
        {
            if (_ending) return;
            _ending = true;

            Debug.Log("[GameplayRoot] 流程结束，回主菜单");
            Framework.Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("SFX_Flow_LevelClear");

            if (ScreenFader.Instance != null)
                ScreenFader.Instance.Play(() => Procedure.ProcedureCore.Instance?.RequestMenu());
            else
                Procedure.ProcedureCore.Instance?.RequestMenu();
        }

        #endregion
    }
}