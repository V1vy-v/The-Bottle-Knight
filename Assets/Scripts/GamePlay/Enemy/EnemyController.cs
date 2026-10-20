using Framework;
using GamePlay.Configs;
using GamePlay.Player;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.Enemy
{
    /// <summary>
    /// 怪物总控制器
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        public event Action<float, float> OnWound;//受伤事件，供UI订阅
        [Header("配置表")]
        public EnemyConfig config;

        [Header("运行时状态")]
        public EnemyRealState state = new EnemyRealState();

        [Header("剧情配置")]
        [Tooltip("勾上则开局处于非战斗状态：只走 Idle/Patrol，不追人也不打人，等战斗系统接管")]
        [SerializeField] private bool _passiveAtStart;

        [Tooltip("玩家靠近时要报的目标（和 FlowTrigger 的 goalId 同一套，比如 Node_1）。填了就会自动当成剧情敌人")]
        [SerializeField] private string _talkGoalId;

        [Tooltip("剧情单位：开局就躺在地上（被击晕的中三拳）。勾上后不会发现玩家、不能被打，只能被 F 吸取；" +
                 "掉落瓶盖改成吸取时结算")]
        [SerializeField] private bool _startDefeated;

        /// <summary>开局即倒地（剧情单位）</summary>
        public bool StartedDefeated => _startDefeated;

        [Tooltip("对话触发半径。会生成一个这个大小的交互层碰撞体（吸取用的是预制体上那个小的 InteractPoint）")]
        [SerializeField] private float _talkZoneRadius = 8f;

        /// <summary>上一次播受击音的时间，用来做最小间隔</summary>
        private float _lastHurtSfxTime = -10f;

        /// <summary>Interactable 层。该层碰撞矩阵为空，只能用 Physics2D 查询扫到</summary>
        private const int InteractableLayer = 11;

        /// <summary>
        /// 能不能被对话触发：非战斗状态 且 填了对话目标。
        /// 检测由 PlayerInteractor 用 Interactable 层的查询做 ——
        /// 那层碰撞矩阵是空的，OnTriggerEnter2D 不会回调，只能查询。
        /// </summary>
        public bool NeedsTalk => !CombatEnabled && !string.IsNullOrEmpty(_talkGoalId);

        /// <summary>战斗是否已开启。关闭时 EnemyDetectZone 不登记玩家，敌人不会进 Chase/Attack</summary>
        public bool CombatEnabled { get; private set; } = true;

        /// <summary>场上所有还没进入战斗的敌人，战斗开始时按 config 接管</summary>
        private static readonly List<EnemyController> _passives = new();
        public static IReadOnlyList<EnemyController> PassiveEnemies => _passives;

        private float currentHP;

        public float CurrentHP => state.CurrentHP;

        //AI
        public float idleDuration;    //待机持续时间
        public float patrolDuration;//巡逻持续时间

        public float fadeDuration; //死亡渐隐时间

        //检测状态
        [HideInInspector] public bool playerInDetectRange;
        [HideInInspector] public bool playerInAttackRange;

        // 死亡状态
        public bool IsDefeated { get; private set; }
        public event Action<EnemyController> OnDefeated;
        private Transform _player;            // 持有的玩家对象

        private SpriteRenderer _sprite;
        private Animator animator;
        private EnemyMoveController moveController;
        private EnemyAttackController attackController;

        private StateMachine<EnemyState> _fsm;

        // ================= 【新增】受伤闪烁相关字段 =================
        private Color _originalColor;
        private Coroutine _hitFlashCoroutine;
        // =============================================================

        private void Awake()
        {
            InitFromConfig();
            // 填了对话目标 = 剧情敌人，自动进非战斗状态，省得两个地方都勾
            CombatEnabled = !_startDefeated && !_passiveAtStart && string.IsNullOrEmpty(_talkGoalId);
            animator = GetComponent<Animator>();
            _sprite = GetComponent<SpriteRenderer>();
            moveController = GetComponent<EnemyMoveController>();
            attackController = GetComponent<EnemyAttackController>();

            // 【新增】缓存初始颜色
            if (_sprite != null)
                _originalColor = _sprite.color;

            ForceBodySetup();

            if (moveController == null) return;
            if (animator == null) return;
        }

        /// <summary>
        /// 敌人本体碰撞体保持实体（继续和 Obstacle 挡住墙），
        /// 只把「玩家本体 ↔ 敌人本体」这一对碰撞体设为互相忽略。
        /// 这样玩家和敌人不会互相顶开，但敌人照样撞墙、照样和玩家发生 Trigger 侦测。
        /// </summary>
        private void ForceBodySetup()
        {
            Transform body = transform.Find("MoveCollider");
            if (body != null)
            {
                _bodyCollider = body.GetComponent<Collider2D>();
                if (_bodyCollider != null)
                {
                    _bodyCollider.isTrigger = false;   // 曾经误设成 Trigger 的预制体（Enemy_OnLong）也纠正回来
                    _bodyRadius = Mathf.Max(0.2f, Mathf.Min(_bodyCollider.bounds.extents.x, _bodyCollider.bounds.extents.y));
                    if (moveController != null) moveController.BodyRadius = _bodyRadius;
                }
            }

            ApplyBodyIgnore();      // 玩家此刻可能还没生成，Update 里会继续重试
        }

        /// <summary>让玩家本体和这个敌人的本体互相穿过（其余碰撞关系不变）</summary>
        private void ApplyBodyIgnore()
        {
            if (_bodyIgnored) return;

            Collider2D playerBody = ResolvePlayerBody();
            if (playerBody == null) return;

            if (_bodyCollider == null)
            {
                _bodyIgnored = true;
                return;
            }

            if (!Physics2D.GetIgnoreCollision(playerBody, _bodyCollider))
                Physics2D.IgnoreCollision(playerBody, _bodyCollider, true);

            _bodyIgnored = true;
        }

        /// <summary>玩家本体碰撞体（Player/MoveCollider）。缓存起来，掉帧时不用每帧找。</summary>
        private static Collider2D ResolvePlayerBody()
        {
            if (_playerBody != null) return _playerBody;

            var root = Framework.Global.Get<GameplayRoot>();
            GameObject player = root != null ? root.Player : null;
            if (player == null) return null;

            Transform body = player.transform.Find("MoveCollider");
            if (body == null)
            {
                Collider2D[] cols = player.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    if (cols[i] != null && !cols[i].isTrigger) { body = cols[i].transform; break; }
                }
            }

            _playerBody = body != null ? body.GetComponent<Collider2D>() : null;
            return _playerBody;
        }

        private static Collider2D _playerBody;
        private Collider2D _bodyCollider;
        private bool _bodyIgnored;
        private float _bodyRadius = 0.8f;

        /// <summary>本体碰撞半径，直线追击时用来探墙</summary>
        public float BodyRadius => _bodyRadius;

        private void InitFromConfig()
        {
            if (config == null)
            {
                return;
            }

            state.baseHP = config.MaxHp;
            state.CurrentHP = state.MaxHP;
            state.baseAtk = config.Damage;
            state.baseMoveSpeed = config.MoveSpeed;
            //state.baseAttackRange = config.AttackRange;
            state.baseAttackCooldown = config.AttackCooldown;
            state.baseHurtDuration = config.HurtStunTime;

            idleDuration = config.IdleDuration;
            patrolDuration = config.PatrolDuration;
            fadeDuration = config.FadeDuration;
        }

        private void Start()
        {
            _fsm = new StateMachine<EnemyState>();
            _fsm.RegisterState(new EnemyIdleState(this, _fsm));
            _fsm.RegisterState(new EnemyPatrolState(this, _fsm));
            _fsm.RegisterState(new EnemyChaseState(this, _fsm));
            _fsm.RegisterState(new EnemyAttackState(this, _fsm));
            _fsm.RegisterState(new EnemyHurtState(this, _fsm));
            _fsm.RegisterState(new EnemyDeadState(this, _fsm));

            _fsm.ChangeState(EnemyState.Idle);

            // 剧情单位：开局就躺在地上，直接走死亡通路（尸体保留、可被吸取、不掉瓶盖）
            if (_startDefeated)
            {
                ForceDefeated();
                return;
            }

            // 剧情敌人：登记进被动列表，等战斗接管
            if (!CombatEnabled)
            {
                _passives.Remove(this);
                _passives.Add(this);
                CreateTalkZone();
            }
        }

        private void Update()
        {
            if (!_bodyIgnored) ApplyBodyIgnore();     // 玩家比敌人晚生成时，这里补上
            if (_aiPaused) return;
            TickSenses();
            _fsm?.Update(Time.deltaTime);
        }

        /// <summary>战败重试时把场上的敌人回满血（已经倒下/被清掉的不管）</summary>
        public void ResetToFull()
        {
            if (IsDefeated) return;

            state.CurrentHP = state.MaxHP;
            OnWound?.Invoke(state.CurrentHP, state.MaxHP);
        }

        #region 感知（距离兜底）

        [Header("感知距离")]
        [Tooltip("发现玩家的距离。预制体上的侦测碰撞体是一条约 1 单位高的细带，玩家站在地面上常常碰不到，" +
                 "所以这里按距离再判一次，两个结果取并集")]
        [SerializeField] private float _detectRadius = 25f;

        [Tooltip("进入攻击状态的距离")]
        [SerializeField] private float _attackRadius = 13f;

        /// <summary>攻击距离，攻击结算时用</summary>
        public float AttackRadius => _attackRadius;

        /// <summary>最近一次攻击结算的时间（防止同一次攻击既吃距离结算又吃碰撞体结算）</summary>
        public float LastAttackTime { get; private set; } = -10f;

        private Transform _cachedPlayer;

        /// <summary>当前锁定的玩家（给攻击结算用）</summary>
        public Transform PlayerTarget => _cachedPlayer;

        private void TickSenses()
        {
            if (!CombatEnabled)
            {
                playerInDetectRange = false;
                playerInAttackRange = false;
                return;
            }

            Transform p = _cachedPlayer;
            if (p == null)
            {
                var root = Framework.Global.Get<GameplayRoot>();
                p = root != null && root.Player != null ? root.Player.transform : null;
            }

            if (p == null)
            {
                playerInDetectRange = false;
                playerInAttackRange = false;
                return;
            }

            _cachedPlayer = p;
            SetPlayerTarget(p);          // 追击状态要靠它拿目标；碰撞体感知没触发时这里是唯一来源

            float d = Vector2.Distance(transform.position, p.position);
            playerInDetectRange = d <= _detectRadius;
            playerInAttackRange = d <= _attackRadius;
        }

        /// <summary>
        /// 攻击命中结算（距离版）。预制体上的 Attack 碰撞体只有 21×1，玩家站地面时基本碰不到，
        /// 由 EnemyAttackController 在出手那一帧直接调这里。
        /// </summary>
        public void TryDealDirectHit()
        {
            LastAttackTime = Time.time;

            Transform p = _cachedPlayer;
            if (p == null) return;

            if (Vector2.Distance(transform.position, p.position) > _attackRadius + 1f) return;

            var pc = p.GetComponent<GamePlay.Player.PlayerController>();
            if (pc?.PlayerStats == null) return;

            pc.PlayerStats.ApplyDamage((int)state.baseAtk);
        }

        #endregion

        private void FixedUpdate()
        {
            if (_aiPaused) return;
            _fsm?.FixedUpdate(Time.fixedDeltaTime);
        }

        /// <summary>AI 是否被冻结（对话 / 商店 / 暂停时由 GameplayRoot 统一开关）</summary>
        public bool IsAiPaused => _aiPaused;
        private bool _aiPaused;

        /// <summary>
        /// 冻结 / 解冻 AI。对话与商店期间玩家动不了，如果巡逻怪还在追，
        /// 就会出现「隔着对话框被打死」。只冻结活着、还在战斗状态的敌人，
        /// 已经倒地的不碰（尸体还在渐隐）。
        /// </summary>
        public void SetAiPaused(bool paused)
        {
            if (_aiPaused == paused) return;
            if (IsDefeated) return;                 // 尸体不受影响，让渐隐照常播完

            _aiPaused = paused;
            if (paused) StopMove();                 // 清速度与路径，冻住就别再滑
        }

        /// <summary>
        /// 剧情敌人被玩家靠近：报目标推进流程，并把自己登记成这次遭遇的刷怪点。
        /// </summary>
        public void NotifyTalked()
        {
            if (CombatEnabled || string.IsNullOrEmpty(_talkGoalId)) return;

            var root = Global.Get<GameplayRoot>();
            if (root == null) return;

            root.SetEncounter(null, new Transform[] { transform });
            root.ReportGoalReached(_talkGoalId);

            Debug.Log($"[敌人] {name} 触发目标 {_talkGoalId}");
        }

        /// <summary>
        /// 给剧情敌人生成一个大的对话触发区
        /// </summary>
        private void CreateTalkZone()
        {
            if (transform.Find("TalkZone") != null) return;

            var go = new GameObject("TalkZone");
            go.layer = InteractableLayer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 3f, 0f);

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = _talkZoneRadius;

            go.AddComponent<EnemyTalkZone>();
        }

        private void OnDestroy()
        {
            _passives.Remove(this);
        }

        /// <summary>是否由实例门面生成。直接摆在场景里的剧情敌人是 false</summary>
        public bool SpawnedByFacade { get; private set; }

        public void MarkSpawnedByFacade()
        {
            SpawnedByFacade = true;
        }

        public void ReleaseSelf()
        {
            if (SpawnedByFacade) Global.Release(gameObject);
            else Destroy(gameObject);
        }

        public void EnterCombat()
        {
            CombatEnabled = true;
            _passives.Remove(this);

            Debug.Log($"[敌人] {name} 进入战斗");

            StartCoroutine(RecheckDetectZones());
        }

        private IEnumerator RecheckDetectZones()
        {
            var zones = GetComponentsInChildren<EnemyDetectZone>(true);
            var cols = new List<Collider2D>();

            for (int i = 0; i < zones.Length; i++)
            {
                var col = zones[i].GetComponent<Collider2D>();
                if (col == null) continue;
                col.enabled = false;
                cols.Add(col);
            }

            yield return new WaitForFixedUpdate();

            for (int i = 0; i < cols.Count; i++)
                if (cols[i] != null) cols[i].enabled = true;
        }

        //给状态类调用的工具方法

        public void PlayAnimation(string animationName)
        {
            print("执行" + animationName + "动画");
            if (animator == null) return;
            animator.CrossFade(Animator.StringToHash(animationName), 0.1f);
        }

        public void MoveToPlayer()
        {
            if (_player == null) return;
            moveController?.MoveTo(_player.transform.position);
        }

        public void ChangeSpeed(float speed = 0)
        {
            moveController?.SetSpeed(speed);
        }

        /// <summary>立即停住（清速度与路径）。死亡时用，避免尸体惯性滑走</summary>
        public void StopMove()
        {
            moveController?.Stop();
        }

        public void RandomMove(float distance)
        {
            moveController.MoveToRandomDirection(distance);
        }

        public void SetAlpha(float alpha)
        {
            if (_sprite == null) return;
            var c = _sprite.color;
            c.a = alpha;
            _sprite.color = c;
        }

        public void ApplyDamage(float damage)
        {
            if (_fsm == null) return;
            if (_fsm.CurrentState == EnemyState.Dead) return;

            state.CurrentHP -= damage;
            OnWound?.Invoke(state.CurrentHP, state.MaxHP);
            Debug.Log($"受到{damage}点伤害");

            // 受击音：三个变体随机；0.12 秒内只响一次，避免多段攻击糊成噪音
            if (Time.time - _lastHurtSfxTime >= 0.12f)
            {
                _lastHurtSfxTime = Time.time;
                int pick = UnityEngine.Random.Range(1, 4);
                Framework.Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("SFX_Enemy_Hurt" + pick);
            }

            // ================= 【新增】触发受伤变红闪烁 =================
            if (gameObject.activeInHierarchy)
            {
                if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
                _hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
            }
            // =============================================================

            if (state.CurrentHP <= 0)
            {
                state.CurrentHP = 0;
                _fsm.ChangeState(EnemyState.Dead);
            }
            //else if (config != null && config.SurrenderHpRatio > 0f &&
            //         state.CurrentHP <= state.MaxHP * config.SurrenderHpRatio)
            //{
            //    // 剧情让步：国窖血量到 30% 就红温认输（不能击杀），按战败收场
            //    Debug.Log($"[Enemy] {name} 血量 {state.CurrentHP:F0}/{state.MaxHP:F0}，认输");
            //    ForceDefeated();
            //}
            else
            {
                _fsm.ChangeState(EnemyState.Hurt);
            }
        }

        // 受伤闪烁协程与对象池清理
        private IEnumerator HitFlashRoutine()
        {
            if (_sprite == null) yield break;

            // 变红（保留当前的透明度，防止打断死亡渐隐）
            Color flashColor = Color.red;
            flashColor.a = _sprite.color.a;
            _sprite.color = flashColor;

            // 等待 0.1 秒（可以根据手感调整）
            yield return new WaitForSeconds(0.1f);

            // 恢复原色（同样保留当前透明度）
            Color restoreColor = _originalColor;
            restoreColor.a = _sprite.color.a;
            _sprite.color = restoreColor;

            _hitFlashCoroutine = null;
        }

        private void OnDisable()
        {
            if (_hitFlashCoroutine != null)
            {
                StopCoroutine(_hitFlashCoroutine);
                _hitFlashCoroutine = null;
            }

            if (_sprite != null)
            {
                Color restoreColor = _originalColor;
                restoreColor.a = _sprite.color.a; // 保留当前的透明度
                _sprite.color = restoreColor;
            }
        }

        /// <summary>
        /// 剧情单位（中三拳被击晕）用：走和战死同一条通路
        /// </summary>
        public void ForceDefeated()
        {
            Debug.LogError("【死亡触发】ForceDefeated 被调用了！看看报错堆栈是谁调的");
            if (_fsm == null) return;
            state.CurrentHP = 0;
            _fsm.ChangeState(EnemyState.Dead);
        }

        public void SetPlayerTarget(Transform player)
        {
            _player = player;
        }
        public void MissPlayer()
        {
            _player = null;
        }

        public void NotifyDefeated()
        {
            if (IsDefeated) return;
            IsDefeated = true;

            // 掉落瓶盖。开局即倒地的剧情单位（中三拳）不走这里，改成吸取时结算
            if (!_startDefeated && config != null && config.CapDrop > 0)
            {
                GameObject playerGo = Global.Get<GameplayRoot>()?.Player;
                PlayerController pc = playerGo != null ? playerGo.GetComponent<PlayerController>() : null;
                if (pc != null && pc.PlayerStats != null)
                {
                    pc.PlayerStats.Caps += config.CapDrop;
                    Debug.Log($"[瓶盖] 获得 {config.CapDrop} 个，共 {pc.PlayerStats.Caps}");

                    // 掉瓶盖音：金属叮当
                    Framework.Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("SFX_Player_CapPickup");
                }
            }

            OnDefeated?.Invoke(this);
        }
        public void Attack()
        {
            attackController.OnAttack();
        }
        public void OnAttackEnd()
        {
            attackController.OnAttackEnd();
        }
    }
}