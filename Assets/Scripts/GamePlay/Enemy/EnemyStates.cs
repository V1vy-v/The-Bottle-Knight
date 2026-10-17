/*
 * 怪物状态机状态
 */
using Framework;
using UnityEngine;
namespace GamePlay.Enemy
{
    /// <summary>
    /// 敌人待机状态：原地不动，等待条件触发
    /// </summary>
    public class EnemyIdleState : EnemyStateBase
    {
        private float _idleTimer;

        public override EnemyState StateKey => EnemyState.Idle;

        public EnemyIdleState(EnemyController enemy, StateMachine<EnemyState> sm)
            : base(enemy, sm) { }

        public override void Enter()
        {
            Enemy.ChangeSpeed();
            Enemy.PlayAnimation("Idle");
            _idleTimer = 0f; 
        }

        protected override void Tick(float deltaTime)
        {
            _idleTimer += deltaTime;
        }

        protected override void CheckStateChange()
        {
            if (Enemy.playerInAttackRange)
                _stateMachine.ChangeState(EnemyState.Attack);
            else if (Enemy.playerInDetectRange)
                _stateMachine.ChangeState(EnemyState.Chase);
            else if (_idleTimer >= Enemy.idleDuration)
                _stateMachine.ChangeState(EnemyState.Patrol); 
        }
    }

    /// <summary>
    /// 敌人巡逻状态：沿路径点移动
    /// </summary>
    public class EnemyPatrolState : EnemyStateBase
    {
        private float _patrolTimer;

        public override EnemyState StateKey => EnemyState.Patrol;

        public EnemyPatrolState(EnemyController enemy, StateMachine<EnemyState> sm)
            : base(enemy, sm) { }

        public override void Enter()
        {
            Enemy.ChangeSpeed(Enemy.state.MoveSpeed);
            Enemy.PlayAnimation("Move");
            _patrolTimer = 0f; // 进入时清零
            Enemy.RandomMove(12f);//test
        }

        protected override void Tick(float deltaTime)
        {
            _patrolTimer += deltaTime;
        }

        protected override void CheckStateChange()
        {
            if (Enemy.playerInAttackRange)
                _stateMachine.ChangeState(EnemyState.Attack);
            else if (Enemy.playerInDetectRange)
                _stateMachine.ChangeState(EnemyState.Chase);
            else if (_patrolTimer >= Enemy.patrolDuration)
                _stateMachine.ChangeState(EnemyState.Idle); 
        }
    }

    /// <summary>
    /// 敌人追击状态：朝玩家移动
    /// </summary>
    public class EnemyChaseState : EnemyStateBase
    {
        public override EnemyState StateKey => EnemyState.Chase;

        public EnemyChaseState(EnemyController enemy, StateMachine<EnemyState> sm)
            : base(enemy, sm) { }

        public override void Enter()
        {
            Enemy.ChangeSpeed(Enemy.state.MoveSpeed);
            Enemy.PlayAnimation("Move");
        }

        protected override void Tick(float deltaTime)
        {
            // 朝玩家移动
            Enemy.MoveToPlayer();
        }

        protected override void CheckStateChange()
        {
            if (Enemy.playerInAttackRange)
                _stateMachine.ChangeState(EnemyState.Attack);
            else if (!Enemy.playerInDetectRange)
                _stateMachine.ChangeState(EnemyState.Patrol);
        }

    }

    /// <summary>
    /// 敌人攻击状态：对玩家造成伤害
    /// </summary>
    public class EnemyAttackState : EnemyStateBase
    {
        private float _attackTimer;     // 攻击冷却计时
        private float _preAttackTimer;  // 前摇/停留计时
        private bool _hasAttacked;      // 是否已经发起过第一次攻击

        public override EnemyState StateKey => EnemyState.Attack;

        public EnemyAttackState(EnemyController enemy, StateMachine<EnemyState> sm)
            : base(enemy, sm) { }

        public override void Enter()
        {
            _attackTimer = 0f;
            _preAttackTimer = 0f;
            _hasAttacked = false;

            //Enemy.PlayAnimation("Idle");
        }

        protected override void Tick(float deltaTime)
        {
            // 处理前摇停留逻辑
            if (!_hasAttacked)
            {
                _preAttackTimer += deltaTime;
                if (_preAttackTimer >= 1.0f) // 停留 1 秒后发动攻击
                {
                    _hasAttacked = true;
                    _attackTimer = 0f;

                    Enemy.PlayAnimation("Attack");
                    Enemy.Attack();
                }
                return; 
            }

            _attackTimer += deltaTime;

            if (_attackTimer >= Enemy.state.AttackCooldown)
            {
                _attackTimer = 0f;
                Enemy.PlayAnimation("Attack");
                Enemy.OnAttackEnd();
                Enemy.Attack();
            }
        }

        protected override void CheckStateChange()
        {
            // 如果玩家离开攻击范围
            if (!Enemy.playerInAttackRange && Enemy.playerInDetectRange)
            {
                _stateMachine.ChangeState(EnemyState.Chase);
            }
            else if (!Enemy.playerInDetectRange)
            {
                _stateMachine.ChangeState(EnemyState.Patrol);
            }
        }

        public override void Exit()
        {
            base.Exit();
            Enemy.OnAttackEnd();
        }
    }

    /// <summary>
    /// 敌人受击状态：被击中时短暂僵直
    /// </summary>
    public class EnemyHurtState : EnemyStateBase
    {
        private float _hurtTimer;
        public override EnemyState StateKey => EnemyState.Hurt;
        public EnemyHurtState(EnemyController enemy, StateMachine<EnemyState> sm)
            : base(enemy, sm) { }

        public override void Enter()
        {
            Enemy.ChangeSpeed();     
            _hurtTimer = 0f;         
            Enemy.PlayAnimation("Hurt");
        }

        protected override void Tick(float deltaTime)
        {
            _hurtTimer += deltaTime; 
        }

        protected override void CheckStateChange()
        {
            if (_hurtTimer < Enemy.state.HurtDuration) return; 

            if (Enemy.state.CurrentHP <= 0)
                _stateMachine.ChangeState(EnemyState.Dead);
            else if (Enemy.playerInAttackRange)
                _stateMachine.ChangeState(EnemyState.Attack);
            else if (Enemy.playerInDetectRange)
                _stateMachine.ChangeState(EnemyState.Chase);
            else
                _stateMachine.ChangeState(EnemyState.Patrol);
        }
    }

    /// <summary>
    /// 敌人死亡状态。
    /// 可吸取的敌人：渐隐到「倒地」透明度后留在场上，等主角用 F 吸取（由 AbsorbSource 释放）。
    /// 不可吸取的敌人：渐隐到全透明后自行回收。
    /// </summary>
    public class EnemyDeadState : EnemyStateBase
    {
        /// <summary>可吸取敌人倒地后保持的透明度（等倒地美术到位后改成切倒地帧）</summary>
        private const float CorpseAlpha = 0.45f;

        private float _deadTimer;
        private bool _settled;     // 渐隐结束，已静止
        private bool _released;    // 防重复回收

        public override EnemyState StateKey => EnemyState.Dead;

        public EnemyDeadState(EnemyController enemy, StateMachine<EnemyState> sm)
            : base(enemy, sm) { }

        private bool CanAbsorb => Enemy.config != null && Enemy.config.CanAbsorb;

        public override void Enter()
        {
            Enemy.ChangeSpeed();
            Enemy.PlayAnimation("Dead");
            _deadTimer = 0f;
            _settled = false;
            _released = false;

            // 通知外部「这只已经死了」—— GameplayBattleState 订阅后转发给 BattleContext
            Enemy.NotifyDefeated();
        }

        protected override void Tick(float deltaTime)
        {
            if (_settled) return;      // 躺好了就不再改透明度

            _deadTimer += deltaTime;

            float duration = Mathf.Max(0.01f, Enemy.fadeDuration);
            float t = Mathf.Clamp01(_deadTimer / duration);

            float target = CanAbsorb ? CorpseAlpha : 0f;
            Enemy.SetAlpha(Mathf.Lerp(1f, target, t));

            if (t >= 1f) _settled = true;
        }

        protected override void CheckStateChange()
        {
            if (!_settled || _released) return;

            // 可吸取的尸体留给玩家，释放交给 AbsorbSource
            if (CanAbsorb) return;

            _released = true;
            Global.Release(Enemy.gameObject);
        }
    }
}