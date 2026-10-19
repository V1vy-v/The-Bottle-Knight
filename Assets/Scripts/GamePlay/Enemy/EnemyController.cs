using Framework;
using GamePlay.Configs;
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
        public event Action<float,float> OnWound;//受伤事件，供UI订阅
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

        private void Awake()
        {
            InitFromConfig();
            // 填了对话目标 = 剧情敌人，自动进非战斗状态，省得两个地方都勾
            CombatEnabled = !_passiveAtStart && string.IsNullOrEmpty(_talkGoalId);
            animator = GetComponent<Animator>();
            _sprite = GetComponent<SpriteRenderer>();
            moveController = GetComponent<EnemyMoveController>();
            attackController = GetComponent<EnemyAttackController>();

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
        /// 故意不登记 placeholder —— 战斗要接管的就是这只，不能把它隐藏掉。
        /// 不做一次性锁：目标不匹配时白白烧掉，下次真需要就触发不了了。
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
        /// 给剧情敌人生成一个大的对话触发区：和 InteractPoint 一样在 Interactable 层，
        /// 但半径独立，避免吸取半径和对话半径绑死在一起。
        /// 用运行期生成而不是写进预制体，是为了所有敌人预制体/场景实例都自动拥有，不用逐个手加。
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

        /// <summary>由 Global.Instantiate* 生成时调一次，之后 ReleaseSelf 才能交回门面</summary>
        public void MarkSpawnedByFacade()
        {
            SpawnedByFacade = true;
        }

        /// <summary>
        /// 统一销毁口。门面生成的交回门面（可能还池），场景里直接摆的直接 Destroy。
        /// 场景实例没在 InstantiateManager 注册过，Release 只会打一条
        /// 「not created by the instance facade」然后什么都不做。
        /// InstantiateManager 也没有对外的手动注册 API，所以只能在这里分流。
        /// </summary>
        public void ReleaseSelf()
        {
            if (SpawnedByFacade) Global.Release(gameObject);
            else Destroy(gameObject);
        }

        /// <summary>
        /// 切进战斗状态。战斗系统接管场上摆好的剧情敌人时调用。
        /// 非战斗期间 EnemyDetectZone 的进入事件被忽略了，而玩家这时可能已经站在检测范围里 ——
        /// 触发器不会重新回调，所以把检测碰撞体关掉再打开，让物理重新发一次进入事件。
        /// </summary>
        public void EnterCombat()
        {
            CombatEnabled = true;
            _passives.Remove(this);

            Debug.Log($"[敌人] {name} 进入战斗");

            StartCoroutine(RecheckDetectZones());
        }

        /// <summary>
        /// 隔一个物理步关掉再打开检测碰撞体，让 OnTriggerEnter2D 重新发一次。
        /// 同一帧内 disable+enable 物理不保证重发，所以中间要 yield 一个 WaitForFixedUpdate。
        /// </summary>
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

        /// <summary>
        /// 播放动画
        /// </summary>
        /// <param name="animationName"></param>
        public void PlayAnimation(string animationName)
        {
            print("执行"+animationName+"动画");
            if (animator == null) return;
            animator.CrossFade(Animator.StringToHash(animationName), 0.1f);
        }

        /// <summary>移动至目标点</summary>
        public void MoveToPlayer()
        {
            if(_player == null) return;
            moveController?.MoveTo(_player.transform.position);
        }

        public void ChangeSpeed(float speed = 0)
        {
            moveController?.SetSpeed(speed);
        }
        /// <summary>
        /// 随机移动(巡逻)
        /// </summary>
        /// <param name="distance"></param>
        public void RandomMove(float distance)
        {
            moveController.MoveToRandomDirection(distance);
        }

        /// <summary>
        /// 改变透明度，用于死亡渐隐
        /// </summary>
        /// <param name="alpha"></param>
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
            OnWound?.Invoke(state.CurrentHP,state.MaxHP);
            Debug.Log($"受到{damage}点伤害");

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