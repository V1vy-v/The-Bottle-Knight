using Framework;
using UI;

namespace GamePlay.Session
{
    /// <summary>
    /// 商店界面。打开时切到 UI 输入映射（主角不再响应 WASD / J），关闭后回到漫游。
    /// <para>
    /// 不自动推进节点 —— 商店是流程里的可选环节，不是节点目标，
    /// 所以这里不用 AutoAdvanceOnFinish，关闭时直接 ChangeMode 回 FreeRoam。
    /// </para>
    /// </summary>
    public sealed class GameplayShopState : GameplayModeStateBase
    {
        public override GameplayMode StateKey => GameplayMode.Shop;

        protected override LocalInputType InputMap => LocalInputType.UI;

        public GameplayShopState(GameplayRoot root, StateMachine<GameplayMode> sm)
            : base(root, sm) { }

        public override void Enter()
        {
            base.Enter();

            ShopPanel.OnShopClosed += HandleShopClosed;
            Global.ShowUI("ShopPanel");
        }

        public override void Exit()
        {
            base.Exit();

            ShopPanel.OnShopClosed -= HandleShopClosed;
            Global.HideUI("ShopPanel");
        }

        private void HandleShopClosed()
        {
            Root.ChangeMode(GameplayMode.FreeRoam);
        }
    }
}
