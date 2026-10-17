using Framework;
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

        #region 生命周期
        public override void Update(float deltaTime) => _modeFsm?.Update(deltaTime);
        public override void FixedUpdate(float fixedDeltaTime) => _modeFsm?.FixedUpdate(fixedDeltaTime);
        public override void LateUpdate() => _modeFsm?.LateUpdate();
        #endregion

        #region 对局生命周期

        /// <summary>
        /// 开始新游戏。头节点由外部传入。
        /// </summary>
        public void StartNewGame()
        {
            AssetHandle handle = Global.Load<NodeConfig>("Assets/Configs/Node/0_StartNode.asset");
            NodeConfig startNode = handle.AssetObject as NodeConfig;
            if (startNode == null)
            {
                Debug.LogError("[GameplayRoot] StartNewGame 传入的 startNode 为空");
                return;
            }

            _storyContext = new();
            _battleContext = new();
            _dialogContext = new();

            _storyContext.Reset(startNode);

            ApplyNodeContext(startNode, isFirstNode: true);

            BuildModeFsm();
            _player = Global.InstantiateUnpooled(
                GlobalConstants.PlayerPrefabLocation,
                new InstantiateOptions(true));

            _modeFsm.ChangeState(startNode.mode);
            Global.Get<CameraManager>()?.SetTarget(_player.transform);
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
            _player = Global.InstantiateUnpooled(
                GlobalConstants.PlayerPrefabLocation,
                new InstantiateOptions(true));

            _modeFsm.ChangeState(savedNode.mode);
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

        #endregion

        private void BuildModeFsm()
        {
            _modeFsm = new StateMachine<GameplayMode>();
            _modeFsm.OnStateChange += OnModeChanged;
            _modeFsm.RegisterState(new GameplayFreeRoamState(this, _modeFsm));
            _modeFsm.RegisterState(new GameplayDialogueState(this, _modeFsm));
            _modeFsm.RegisterState(new GameplayCutsceneState(this, _modeFsm));

            // 留住引用，好在阶段推进 / 结束对局时回收暂留尸体
            _battleState = new GameplayBattleState(this, _modeFsm);
            _modeFsm.RegisterState(_battleState);

            _modeFsm.RegisterState(new GameplayPauseState(this, _modeFsm));
            _modeFsm.RegisterState(new GameplayGameOverState(this, _modeFsm));
        }

        #region 局内切换

        public void ChangeMode(GameplayMode mode) => _modeFsm?.ChangeState(mode);

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
        }

        #endregion

        #region 剧情推进（单场景，不走场景加载）        
        /// <summary>交互物 / 触发器宣告「本节点目标已完成」，由当前模式状态决定是否推进</summary>
        public event Action<string> OnGoalReached;

        /// <summary>本节点目标已达成</summary>
        public void ReportGoalReached(string goalId = null) => OnGoalReached?.Invoke(goalId);

        /// <summary>推进到下一个节点。</summary>
        public void AdvanceNode()
        {
            if (_storyContext == null)
            {
                Debug.LogWarning("[GameplayRoot] StoryContext 为空，无法推进");
                return;
            }

            if (!_storyContext.TryMoveNext(out var next))
            {
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
            Debug.Log($"切换节点至: {node.name}");
            _storyContext?.JumpTo(node);

            ApplyNodeContext(node, isFirstNode: false);

            ChangeMode(node.mode);
        }

        /// <summary>按节点声明刷新 Context。只刷数据，不切模式。</summary>
        private void ApplyNodeContext(NodeConfig node, bool isFirstNode)
        {
            _battleState?.ReleasePendingCorpses();   // 节点推进，上一节点的暂留尸体统一回收

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
    }
}