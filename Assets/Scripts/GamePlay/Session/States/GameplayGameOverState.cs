using Framework;

namespace GamePlay.Session
{
    /// <summary>
    /// 阵亡。目前没有单独的结算界面：停一下（<see cref="RetryDelay"/> 秒）后黑屏重打当前节点，主角回满血。
    /// 以后要做正式的「失败 / 重试」界面，把 <see cref="Tick"/> 里的请求换成界面按钮即可。
    /// </summary>
    public sealed class GameplayGameOverState : GameplayModeStateBase
    {
        public override GameplayMode StateKey => GameplayMode.GameOver;
        protected override LocalInputType InputMap => LocalInputType.UI;

        /// <summary>死亡停顿，给死亡音效与反应留时间</summary>
        private const float RetryDelay = 1.2f;

        private float _remain;
        private bool _requested;

        public GameplayGameOverState(GameplayRoot root, StateMachine<GameplayMode> sm) : base(root, sm)
        {
        }

        public override void Enter()
        {
            base.Enter();

            _remain = RetryDelay;
            _requested = false;

            // 死亡音已经在 PlayerStats.ApplyDamage 里播过 SFX_Player_Death，
            // 这里不再叠一条，避免两段声音打架
        }

        protected override void Tick(float deltaTime)
        {
            if (_requested) return;

            _remain -= deltaTime;
            if (_remain > 0f) return;

            _requested = true;
            Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("SFX_Flow_Retry");

            // 回到上一个自由行走的节点 + 上一处出生点：踩触发器开打的战斗（如雷碧）要重新踩一次
            Root.RetryFromCheckpoint();
        }
    }
}
