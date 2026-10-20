using Core;
using Framework;
using UnityEngine;

namespace GamePlay.Procedure
{
    /// <summary>
    /// 玩法流程核心：界面意图入口，并持有一次对局的寿命（名册、关卡、NetClient / NetServer）。
    /// 状态只持有自己那一段的东西，对局在离开菜单时打开、回到菜单的那条边上拆除。
    /// </summary>
    public sealed class ProcedureCore : MonoSingleton<ProcedureCore>
    {
        private StateMachine<ProcedureState> _fsm;
        public bool IsNewGame { get; private set; } = true;

        #region 属性
        /// <summary>
        /// 当前这一次带任务的切换。同时只有一个。
        /// </summary>
        public ProcedureTransition PendingTransition { get; private set; }
        #endregion

        /// <summary>
        /// 由 <see cref="AppCore"/> 在子系统就绪之后调用，流程从主菜单开始。
        /// </summary>
        public void StartUp()
        {
            // 初始化需要的局内数据
            // InitDataProxy();

            // 直接切换到主菜单状态
            _fsm.ChangeState(ProcedureState.Menu);
        }

        private void InitDataProxy()
        {
            // TODO: 注册局内数据  
        }

        /// <summary>
        /// 清除 isNewGame 标记
        /// </summary>
        /// <param name="isNewGame"></param>
        public void ClearNewGameTag()
        {
            IsNewGame = false;
        }

        #region 状态切换

        /// <summary>
        /// 请求切换新的状态。没有任务时立刻切换；有任务时先进入加载状态，任务完成后再进入目标状态。
        /// 加载中再次请求会停掉当前任务，换成这一次。
        /// </summary>
        public void RequestState(ProcedureState state, ILoadTask task = null)
        {
            if (task == null)
            {
                _fsm.ChangeState(state);
                return;
            }

            PendingTransition = new ProcedureTransition(state, task);
            _fsm.ChangeState(ProcedureState.Loading);
        }

        /// <summary>
        /// 回菜单：换回菜单场景，对局在进入菜单的那条边上拆掉。
        /// </summary>
        public void RequestMenu()
        {
            RequestScene(ProcedureState.Menu, GlobalConstants.MENU_SCENE_NAME);
        }

        /// <summary>
        /// 经由加载闸门换场景。当前状态立刻退出，场景就绪之后才进入目标状态。
        /// </summary>
        public void RequestScene(ProcedureState nextState, string sceneName)
        {
            RequestState(nextState, new SceneLoadTask(sceneName));
        }

        /// <summary>
        /// 进入游戏：切入游戏场景
        /// </summary>
        public void RequestPlay()
        {
            RequestScene(ProcedureState.Playing, GlobalConstants.GAME_SCENE_NAME);
        }

        /// <summary>
        /// 开始新的一局。菜单里的「开始」必须走这里 ——
        /// 直接 RequestPlay 的话，通关回菜单后 IsNewGame 已经是 false，
        /// 会走进 ContinueGame（目前还没有存档，等于开局失败）。
        /// </summary>
        public void RequestNewGame()
        {
            IsNewGame = true;
            RequestPlay();
        }

        #endregion

        #region 生命周期

        protected override void Init()
        {
            _fsm = new StateMachine<ProcedureState>();
            _fsm.RegisterState(new ProcedureMenuState(_fsm, this));
            _fsm.RegisterState(new ProcedureLoadingState(_fsm, this));
            _fsm.RegisterState(new ProcedurePlayingState(_fsm, this));

            // 子系统就绪之后才启动流程，已经就绪时立刻回调
            AppCore.OnAppReady += StartUp;
            AppCore.OnAppQuit += ShutDown;
        }

        private void Update()
        {
            _fsm.Update(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _fsm.FixedUpdate(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            _fsm.LateUpdate();
        }

        protected override void Destroy()
        {
            AppCore.OnAppReady -= StartUp;
            AppCore.OnAppQuit -= ShutDown;

            // 退出时不走回菜单那条业务路径，只拆对局，避免在退出过程中发起场景加载
            _fsm.Stop();
        }

        #endregion

        #region 事件回调
        
        /// <summary>
        /// 加载任务失败，没有进入目标状态。目标不是菜单时再请求回菜单；
        /// 已经是菜单则直接进入菜单状态，避免停在加载态。
        /// </summary>
        public void FailTransition(ProcedureState nextState)
        {
            if (nextState == ProcedureState.Menu)
            {
                _fsm.ChangeState(ProcedureState.Menu);
                return;
            }

            RequestMenu();
        }

        #endregion
    }
}
