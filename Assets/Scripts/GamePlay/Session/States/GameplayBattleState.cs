using Framework;
using GamePlay.Configs;
using GamePlay.Enemy;
using GamePlay.Interaction;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace GamePlay.Session
{
    /// <summary>
    /// 战斗状态：进入时按配置生成敌人，全部击败后推进流程
    /// </summary>
    public sealed class GameplayBattleState : GameplayModeStateBase
    {
        public override GameplayMode StateKey => GameplayMode.Battle;

        /// <summary>敌人全灭后自己切走</summary>
        protected override bool AutoAdvanceOnFinish => true;
        private readonly List<GameObject> _spawnedEnemies = new();


        /// <summary>因可吸取而暂留在场上的尸体，等玩家用 F 吸，或阶段推进时统一回收</summary>
        private readonly List<GameObject> _pendingCorpses = new();
        private bool _hasError;

        public GameplayBattleState(GameplayRoot root, StateMachine<GameplayMode> sm)
            : base(root, sm) { }

        #region 生命周期
        // ===================== 【临时】战斗未完成期间的自动胜利 =====================
        // 切回自由战斗时：删掉这个区块 + Enter 末尾那 3 行 + Tick 方法体
        private const float AutoWinDelay = 0.4f;   // 给敌人 Start 建 FSM 留时间，顺便让 BOSS 露个面
        private float _autoWinRemain;
        // ==========================================================================
        public override void Enter()
        {
            base.Enter();

            // 从暂停回来：敌人、数量统计、暂留尸体都还在，什么都不要重置
            if (_suspended)
            {
                _suspended = false;
                return;
            }

            Global.Get<Framework.SubSystems.AudioManager>()?.PlayBGM("Combat_Light");

            _hasError = false;
            _victoryRewarded = false;
            _spawnedEnemies.Clear();

            BattleConfig config = Root.Battle.CurrentBattle;
            if (config == null)
            {
                Debug.LogError("[GameplayBattleState] 当前节点没有配置 BattleConfig");
                _hasError = true;
                return;
            }

            // 先全部生成，统计真正成功的数量。
            // 不能用 config.TotalCount —— 它只统计 Config != null，
            // PrefabLocation 为空或实例化失败时不会扣掉；计数高于场上敌人，
            // AliveEnemyCount 就归不了零，IsVictory 永远 false，玩家卡在战斗态出不来。
            // 刷怪位置按顺序分配：一张占位贴图对应一只敌人；位置不够时围绕最后一个位置散开
            var anchors = Root.EncounterAnchors;
            int spawned = 0;
            int index = 0;

            foreach (var entry in config.Enemies)
            {
                if (entry.Config == null) continue;

                for (int i = 0; i < entry.Count; i++)
                {
                    // 场上已经摆着这只敌人（剧情 NPC）→ 直接接管，不再重复生成
                    if (TryAdoptPassive(entry.Config))
                    {
                        spawned++;
                        index++;
                        continue;
                    }

                    if (SpawnEnemy(entry.Config, ResolveSpawnPosition(anchors, index))) spawned++;
                    index++;
                }
            }

            if (spawned == 0)
            {
                // 一个都没生成出来：跳过本场战斗，不要卡住
                Debug.LogWarning($"[GameplayBattleState] {config.name} 没有生成出任何敌人，跳过本场战斗");
                MarkFinished();
                return;
            }

            // 贴图任务结束：敌人已就位，整组占位贴图一起隐藏
            Root.HideEncounterPlaceholder();

            //通知敌人数量
            Root.Battle.StartBattle(spawned);

            // 【临时】自动胜利
            if (Root.Story.CurrentNode != null && Root.Story.CurrentNode.autoWinBattle)
                _autoWinRemain = AutoWinDelay;

            // 【刻意不清 EncounterAnchors】擂台是「一个触发器 → 连续多波战斗」，
            // 第 2 波起没有新触发器，清了锚点就只能刷在预制体原点 (0,0)。
            // 锚点改为在剧情阶段切换时由 GameplayRoot 统一清（见 ApplyNodeContext）。
        }

        protected override void Tick(float deltaTime)
        {
            // 【临时】自动胜利倒计时
            if (_autoWinRemain <= 0f) return;

            _autoWinRemain -= deltaTime;
            if (_autoWinRemain > 0f) return;

            AutoDefeatAll();
        }
        /// <summary>
        /// 【临时】把本场生成的敌人逐个判定死亡。
        /// 走的是和真实战死完全相同的通路（ForceDefeated → EnemyDeadState → NotifyDefeated → OnDefeated），
        /// 所以倒地表现、尸体保留、吸取、回收、战斗计数全都不用特殊处理。
        /// </summary>
        private void AutoDefeatAll()
        {
            for (int i = 0; i < _spawnedEnemies.Count; i++)
            {
                var go = _spawnedEnemies[i];
                if (!go) continue;

                var enemy = go.GetComponent<EnemyController>();
                if (enemy != null) enemy.ForceDefeated();
            }
        }
        protected override void CheckStateChange()
        {
            if (_hasError)
            {
                _hasError = false;
                MarkFinished();
            }
            else if (Root.Battle.IsVictory)
            {
                GrantVictoryCaps();
                MarkFinished();
            }

            // 必须调基类：AutoAdvanceOnFinish 的推进逻辑在 GameplayModeStateBase 里。
            // 不调的话 MarkFinished() 只是把一个没人读的布尔值置真，战斗赢了也切不走模式。
            // 对照 GameplayDialogueState —— 它不重写本方法，所以继承基类逻辑、能正常推进。
            base.CheckStateChange();
        }

        /// <summary>本场是否已经发过胜利奖励（防止每帧重复发）</summary>
        private bool _victoryRewarded;

        /// <summary>胜利奖励瓶盖（擂台每轮 1 个）</summary>
        private void GrantVictoryCaps()
        {
            if (_victoryRewarded) return;
            _victoryRewarded = true;

            Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("victory");

            BattleConfig config = Root.Battle.CurrentBattle;
            if (config == null || config.VictoryCaps <= 0) return;

            GameObject playerGo = Root.Player;
            GamePlay.Player.PlayerController pc = playerGo != null
                ? playerGo.GetComponent<GamePlay.Player.PlayerController>() : null;

            if (pc == null || pc.PlayerStats == null) return;

            pc.PlayerStats.Caps += config.VictoryCaps;
            Debug.Log($"[瓶盖] 战斗胜利奖励 {config.VictoryCaps} 个，共 {pc.PlayerStats.Caps}");
        }

        /// <summary>被暂停挂起：场上敌人保留，回来接着打，不要重新生成</summary>
        private bool _suspended;

        public override void Exit()
        {
            base.Exit();

            // 只是被暂停（Esc 菜单）：敌人、计数、尸体统统留着，回来继续这一场
            if (Root.PendingMode == GameplayMode.Pause)
            {
                _suspended = true;
                return;
            }

            ClearAllEnemies();

            // 战斗结束（胜利/失败/跳过）都要把 BGM 切回当前阶段
            Root.PlayStageBgm();
        }

        #endregion

        #region 敌人生成 / 清理

        /// <summary>超出占位贴图数量时，多出来的敌人围绕最后一个位置散开，避免叠在一起</summary>
        private const float SpreadRadius = 1.5f;

        /// <summary>按序号取刷怪位置；没有位置就返回 null，表示用预制体自身坐标</summary>
        private static Vector3? ResolveSpawnPosition(IReadOnlyList<Transform> anchors, int index)
        {
            if (anchors == null || anchors.Count == 0) return null;

            if (index < anchors.Count)
            {
                var a = anchors[index];
                return a != null ? a.position : (Vector3?)null;
            }

            var last = anchors[anchors.Count - 1];
            if (last == null) return null;

            int extra = index - anchors.Count + 1;
            float angle = Mathf.PI * 2f * extra / Mathf.Max(2, extra + 1);
            return last.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * SpreadRadius;
        }

        /// <summary>
        /// 场上如果已经有这个配置的被动敌人（场景里摆好的剧情 NPC），就把它切进战斗并返回 true。
        /// 方案 A：打完的就是站在那儿的那只，不销毁重生成，没有闪现。
        /// </summary>
        private bool TryAdoptPassive(EnemyConfig config)
        {
            var list = EnemyController.PassiveEnemies;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || e.config != config) continue;

                e.EnterCombat();
                e.OnDefeated += HandleEnemyDefeated;

                _spawnedEnemies.Add(e.gameObject);
                Debug.Log($"[战斗] 接管场上被动敌人 {e.name}（{config.Id}）");
                return true;
            }

            return false;
        }

        /// <summary>生成一只敌人；返回是否真的生成成功</summary>
        private bool SpawnEnemy(EnemyConfig enemyConfig, Vector3? spawnPosition)
        {
            if (enemyConfig == null) return false;

            if (string.IsNullOrEmpty(enemyConfig.PrefabLocation))
            {
                Debug.LogWarning($"[GameplayBattleState] {enemyConfig.Id} 没配 PrefabLocation，跳过");
                return false;
            }
            var go = Global.InstantiateUnpooled(enemyConfig.PrefabLocation, new InstantiateOptions(true));

            if (go == null)
            {
                Debug.LogWarning($"[GameplayBattleState] {enemyConfig.Id} 生成失败：{enemyConfig.PrefabLocation}");
                return false;
            }

            if (spawnPosition.HasValue)
            {
                // NavMeshAgent 会覆盖 transform 赋值：先禁用、改位置、再启用（启用时吸附到 NavMesh）
                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;

                go.transform.position = spawnPosition.Value;

                if (agent != null) agent.enabled = true;
            }

            var enemy = go.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.MarkSpawnedByFacade();     // 这是 Global.InstantiateUnpooled 生成的
                enemy.OnDefeated += HandleEnemyDefeated;
            }

            _spawnedEnemies.Add(go);
            return true;
        }

        private void ClearAllEnemies()
        {
            for (int i = 0; i < _spawnedEnemies.Count; i++)
            {
                var go = _spawnedEnemies[i];
                if (!go) continue;

                // 可吸取且还没被吸的尸体，留给剧情阶段 —— 玩家可能还没走过去按 F。
                // 这条分支故意不退订：尸体还活着，后面可能还会触发事件。
                var absorb = go.GetComponent<AbsorbSource>();
                var _ec = go.GetComponent<EnemyController>();
                Debug.Log($"[战斗清理] {go.name} absorb={absorb != null} enemy={_ec != null} defeated={_ec != null && _ec.IsDefeated} cfg={(_ec != null && _ec.config != null ? _ec.config.name : "null")} canAbsorb={_ec != null && _ec.config != null && _ec.config.CanAbsorb} awaiting={absorb != null && absorb.IsAwaitingAbsorb}");
                if (absorb != null && absorb.IsAwaitingAbsorb)
                {
                    _pendingCorpses.Add(go);
                    continue;
                }

                ReleaseEnemy(go);
            }
            _spawnedEnemies.Clear();
        }

        /// <summary>
        /// 回收暂留的可吸取尸体。由 GameplayRoot 在推进剧情阶段和结束对局时调用。
        /// 已经被 AbsorbSource 吸掉的不在了，这里会自然跳过。
        /// </summary>
        public void ReleasePendingCorpses()
        {
            Debug.Log($"[暂留尸体] 回收 {_pendingCorpses.Count} 具");
            for (int i = 0; i < _pendingCorpses.Count; i++)
                ReleaseEnemy(_pendingCorpses[i]);

            _pendingCorpses.Clear();
        }

        /// <summary>
        /// 统一释放口：先退订再回收。
        /// 订阅必须成对 —— GameplayBattleState 是整个对局只 new 一次的常驻实例，
        /// 而尸体可能跨战斗存活；漏退订会让 OnDefeated 一直指着一个再也用不到的战斗态。
        /// </summary>
        private void ReleaseEnemy(GameObject go)
        {
            if (!go) return;

            var enemy = go.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.OnDefeated -= HandleEnemyDefeated;
                enemy.ReleaseSelf();       // 门面生成的还池，场景摆的 Destroy
                return;
            }

            Global.Release(go);
        }
        private void HandleEnemyDefeated(EnemyController e)
        {
            Root.Battle.OnEnemyDead();
        }
        #endregion
    }
}