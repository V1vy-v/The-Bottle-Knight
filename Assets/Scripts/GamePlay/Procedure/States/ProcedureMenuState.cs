using Framework;
using UnityEngine;

namespace GamePlay.Procedure
{
    /// <summary>
    /// 主菜单流程。进入时菜单场景已经就绪，这里只负责呈现菜单；
    /// 上一场对局的拆除由 ProcedureCore 在进入菜单的那条边上完成。
    /// </summary>
    public sealed class ProcedureMenuState : ProcedureStateBase
    {
        public override ProcedureState StateKey => ProcedureState.Menu;

        public ProcedureMenuState(StateMachine<ProcedureState> stateMachine, ProcedureCore procedure)
            : base(stateMachine, procedure)
        {
        }

        #region 状态周期

        public override void Enter()
        {
            Cursor.lockState = CursorLockMode.None;

            Global.Get<LocalInputManager>().SwitchMap(LocalInputType.UI);
            Global.ShowUI("BeginPanel");

            var audio = Global.Get<Framework.SubSystems.AudioManager>();
            audio?.PlayBGM("BGM_Menu");
            // 回菜单时把场景里的环境音 / 循环音效收掉，否则会跟着带过来
            audio?.StopAmbience();
            audio?.StopLoop();
        }

        #endregion
    }
}
