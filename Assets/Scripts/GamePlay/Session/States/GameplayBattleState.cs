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

        /// <summary>本场接管的「场景剧情敌人」（不能销毁，重试时要复位）及其原始位置</summary>
        private readonly List<GameObject> _adoptedEnemies = new();
        private readonly Dictionary<GameObject, Vector3> _adoptedHome = new();


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

            // 战斗曲：BOSS 战在配置里填 Combat_Boss，留空就用通用战斗曲
            string bgmKey = string.IsNullOrEmpty(config.BgmKey) ? "Combat_Light" : config.BgmKey;
            var audio = Global.Get<Framework.SubSystems.AudioManager>();
            if (bgmKey == "Combat_Boss")
                audio?.PlaySfx("SFX_Boss_Intro");      // BOSS 登场吼声，和战斗曲一起起，制造压迫感
            audio?.PlayBGM(bgmKey);

            // 刷怪位置按顺序分配：一张占位贴图对应一只敌人；位置不够时围绕最后一个位置散开。
            // 来源优先级：BattleConfig.SpawnAnchorName（场景锚点）> 剧情触发器登记的遭遇点 > 围绕主角散开
            var anchors = ResolveAnchors(config);
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

        /// <summary>一个锚点都没有时，敌人绕主角散开的半径</summary>
        private const float FallbackRadius = 6f;

        /// <summary>
        /// 解析本场战斗的刷怪位置，优先级：
        /// 1. BattleConfig.SpawnAnchorName 指定的场景锚点（自己 + 所有子物体 = 一张贴图一个位置，同时隐藏贴图）；
        /// 2. 剧情触发器 / 剧情 NPC 登记的遭遇点（FlowTrigger / EnemyController.NotifyTalked）；
        /// 3. 都没有 —— 返回空，由 ResolveSpawnPosition 围绕主角散开。
        /// 第 3 条是保险：以前没有锚点会刷在预制体原点 (0,0)，表现为「BOSS 生成在场景原点」。
        /// </summary>
        private List<Transform> ResolveAnchors(BattleConfig config)
        {
            if (config != null && !string.IsNullOrEmpty(config.SpawnAnchorName))
            {
                GameObject marker = FindSceneObject(config.SpawnAnchorName);
                if (marker != null)
                {
                    var points = new List<Transform> { marker.transform };
                    for (int i = 0; i < marker.transform.childCount; i++)
                        points.Add(marker.transform.GetChild(i));

                    Root.HidePlaceholder(marker);      // 锚点下面通常是占位贴图，开打就藏
                    return points;
                }

                Debug.LogWarning($"[GameplayBattleState] {config.Id} 找不到刷怪锚点 {config.SpawnAnchorName}，" +
                                 "按主角周围散开（不用上一场残留的遭遇点）");
                return null;
            }

            if (Root.EncounterAnchors.Count > 0)
                return new List<Transform>(Root.EncounterAnchors);

            return null;
        }

        /// <summary>按名字找场景里的物体（只认已加载场景，不会找到预制体资产）</summary>
        private static GameObject FindSceneObject(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go != null) return go;

            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != objectName) continue;
                if (!t.gameObject.scene.isLoaded) continue;
                return t.gameObject;
            }

            return null;
        }

        /// <summary>按序号取刷怪位置；没有锚点时围绕主角散开（黄金角，数量多也不会叠）</summary>
        private Vector3? ResolveSpawnPosition(IReadOnlyList<Transform> anchors, int index)
        {
            if (anchors == null || anchors.Count == 0)
            {
                Vector3 center = Root.Player != null ? Root.Player.transform.position : Vector3.zero;
                float angle = index * 137.5f * Mathf.Deg2Rad;
                return center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * FallbackRadius;
            }

            if (index < anchors.Count)
            {
                var a = anchors[index];
                return a != null ? a.position : (Vector3?)null;
            }

            var last = anchors[anchors.Count - 1];
            if (last == null) return null;

            int extra = index - anchors.Count + 1;
            float spread = Mathf.PI * 2f * extra / Mathf.Max(2, extra + 1);
            return last.position + new Vector3(Mathf.Cos(spread), Mathf.Sin(spread), 0f) * SpreadRadius;
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
                _adoptedEnemies.Add(e.gameObject);
                _adoptedHome[e.gameObject] = e.transform.position;
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
        /// 战败重试用：把本场生成的敌人连同暂留的尸体一起清干净，
        /// 让重进战斗时是「第一次打」的状态，而不是上一把的残局。
        /// </summary>
        public void ForceClearAll()
        {
            ReleasePendingCorpses();

            for (int i = 0; i < _spawnedEnemies.Count; i++)
            {
                GameObject go = _spawnedEnemies[i];
                if (!go) continue;

                // 场景剧情敌人（国窖这种）：不能销毁，复位成被动 + 满血 + 原位，
                // 否则玩家战败重进后场上没有它，对话触发不了，流程卡死
                if (_adoptedEnemies.Contains(go))
                {
                    var adopted = go.GetComponent<EnemyController>();
                    if (adopted != null)
                    {
                        adopted.OnDefeated -= HandleEnemyDefeated;
                        Vector3 home = _adoptedHome.TryGetValue(go, out Vector3 pos) ? pos : go.transform.position;
                        adopted.RestoreAsStoryNpc(home);
                        continue;
                    }
                }

                ReleaseEnemy(go);
            }

            _spawnedEnemies.Clear();
            _adoptedEnemies.Clear();
            _adoptedHome.Clear();
            _autoWinRemain = 0f;
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