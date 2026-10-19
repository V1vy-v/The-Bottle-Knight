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

        [Tooltip("对话触发半径。会生成一个这个大小的交互层碰撞体（吸取用的是预制体上那个小的 InteractPoint）")]
        [SerializeField] private float _talkZoneRadius = 8f;

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

        public float CurrentHP => currentHP;

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
            CombatEnabled = !_passiveAtStart && string.IsNullOrEmpty(_talkGoalId);
            animator = GetComponent<Animator>();
            _sprite = GetComponent<SpriteRenderer>();
            moveController = GetComponent<EnemyMoveController>();
            attackController = GetComponent<EnemyAttackController>();

            // 【新增】缓存初始颜色
            if (_sprite != null)
                _originalColor = _sprite.color;

            if (moveController == null) return;
            if (animator == null) return;
        }

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
            _fsm?.Update(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _fsm?.FixedUpdate(Time.fixedDeltaTime);
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

            // 掉落瓶盖
            if (config != null && config.CapDrop > 0)
            {
                GameObject playerGo = Global.Get<GameplayRoot>()?.Player;
                PlayerController pc = playerGo != null ? playerGo.GetComponent<PlayerController>() : null;
                if (pc != null && pc.PlayerStats != null)
                {
                    pc.PlayerStats.Caps += config.CapDrop;
                    Debug.Log($"[瓶盖] 获得 {config.CapDrop} 个，共 {pc.PlayerStats.Caps}");
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