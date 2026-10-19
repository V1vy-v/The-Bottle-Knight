using Framework;
using UI;

namespace GamePlay.Session
{
    /// <summary>
    /// 暂停（Esc 菜单）。打开时切到 UI 输入映射并显示 MenuPanel，
    /// 关闭后回到暂停前的模式（GameplayRoot._resumeMode），不会推进节点。
    /// </summary>
    public sealed class GameplayPauseState : GameplayModeStateBase
    {
        public override GameplayMode StateKey => GameplayMode.Pause;

        protected override LocalInputType InputMap => LocalInputType.UI;

        public GameplayPauseState(GameplayRoot root, StateMachine<GameplayMode> sm) : base(root, sm)
        {
        }

        public override void Enter()
        {
            base.Enter();

            MenuPanel.OnMenuClosed += HandleMenuClosed;
            Global.ShowUI("MenuPanel");
        }

        public override void Exit()
        {
            base.Exit();

            MenuPanel.OnMenuClosed -= HandleMenuClosed;
            Global.HideUI("MenuPanel");
        }

        private void HandleMenuClosed()
        {
            // 回到暂停前的模式（OnModeChanged 记录了 _resumeMode）
            Root.Resume();
        }
    }
}
