using Framework;
using GamePlay.Configs;
using System;
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
        }

        private void Update()
        {
            _fsm?.Update(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _fsm?.FixedUpdate(Time.fixedDeltaTime);
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