using Framework;

namespace GamePlay.Procedure
{
    /// <summary>
    /// 加载状态。
    /// 负责执行 PendingTransition 的加载任务，完成后进入目标状态。
    /// 加载期间显示 LoadingPanel；即使场景加载很快，也保证面板至少露 <see cref="MinShowTime"/> 秒。
    /// </summary>
    public sealed class ProcedureLoadingState : ProcedureStateBase
    {
        /// <summary>加载面板的界面 ID（预制体在 Assets/Prefabs/UI/Loading，地址 UI_LoadingPanel）</summary>
        private const string PanelId = "LoadingPanel";

        /// <summary>最短显示时间：加载再快也让加载动画露一下，避免闪一帧就消失</summary>
        private const float MinShowTime = 0.6f;

        private ILoadTask _task;

        /// <summary>面板是否已经显示（没显示就别去 Hide，否则 UI 框架会打警告）</summary>
        private bool _panelShown;

        /// <summary>加载任务已完成，正在等最短显示时间</summary>
        private bool _taskDone;

        private ProcedureState _nextState;
        private float _minRemain;

        public override ProcedureState StateKey => ProcedureState.Loading;

        public ProcedureLoadingState(StateMachine<ProcedureState> stateMachine, ProcedureCore procedure)
            : base(stateMachine, procedure)
        {
        }

        #region 状态周期

        public override void Enter()
        {
            _taskDone = false;
            _minRemain = MinShowTime;

            // 顺序很重要：StartTask() 里的 ClearRes() 会调用 HideAllUI()，
            // 面板必须在这之后显示，否则刚显示就被清掉。
            StartTask();

            // Blocker 优先级：盖在所有面板上面，并把底盘输入挡住
            Global.ShowUI(PanelId, new UI.LoadingPanelProperties(PanelPriority.Blocker));
            _panelShown = true;
        }

        public override void Exit()
        {
            StopTask();

            if (_panelShown)
            {
                _panelShown = false;
                Global.HideUI(PanelId);
            }
        }

        protected override void Tick(float deltaTime)
        {
            _task?.Update(deltaTime);

            if (!_taskDone) return;

            _minRemain -= deltaTime;
            if (_minRemain > 0f) return;

            _taskDone = false;
            _stateMachine.ChangeState(_nextState);
        }

        #endregion

        private void StartTask()
        {
            StopTask();

            var transition = Procedure.PendingTransition;
            _task = transition.Task;

            _task.Finished += OnTaskFinished;
            _task.Start();
        }

        private void OnTaskFinished()
        {
            var transition = Procedure.PendingTransition;
            bool failed = _task != null && _task.IsFailed;

            StopTask();

            if (failed)
            {
                Procedure.FailTransition(transition.NextState);
                return;
            }

            // 不在这里直接切状态：先让 Tick 把最短显示时间走完
            _nextState = transition.NextState;
            _taskDone = true;
        }

        private void StopTask()
        {
            if (_task == null)
                return;

            _task.Finished -= OnTaskFinished;
            _task.Stop();
            _task = null;
        }
    }
}
