using Framework;
using GamePlay;
using GamePlay.Procedure;
using GamePlay.Session;

namespace GamePlay.Session
{
    public abstract class GameplayModeStateBase : StateBase<GameplayMode>
    {
        protected readonly GameplayRoot Root;
        protected ProcedureCore Procedure => ProcedureCore.Instance;

        /// <summary>本状态是否已完成任务</summary>
        protected bool Finished { get; private set; }

        /// <summary>完成后是否自动推进。默认 false，子类按需覆写。</summary>
        protected virtual bool AutoAdvanceOnFinish => false;

        protected GameplayModeStateBase(GameplayRoot root, StateMachine<GameplayMode> stateMachine)
            : base(stateMachine)
        {
            Root = root;
        }

        protected virtual LocalInputType InputMap => LocalInputType.Player;

        public override void Enter()
        {
            Global.Get<LocalInputManager>().SwitchMap(InputMap);
            Finished = false;  
        }

        /// <summary>子类在"本状态任务完成"时调用</summary>
        protected void MarkFinished()
        {
            Finished = true;
        }

        protected override void CheckStateChange()
        {
            base.CheckStateChange();

            if (Finished && AutoAdvanceOnFinish)
            {
                Finished = false;
                Root.AdvanceNode();
            }
        }
    }
}
