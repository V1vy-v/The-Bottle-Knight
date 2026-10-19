using System.Collections.Generic;
using Framework;
using GamePlay.Session;
using UI;

namespace GamePlay
{
    /// <summary>
    /// 教学提示的统一入口（TipsPanel）。
    /// <para>
    /// 用队列而不是「后一条直接顶掉前一条」：节点推进可能只隔一次按键，
    /// 直接 ShowUI 会把刚弹出来的提示瞬间换掉，玩家根本来不及看。
    /// 一条播完（TipsPanel 关闭）再播下一条。
    /// </para>
    /// <para>
    /// 显示期间切到 UI 输入映射，主角不再响应 WASD / J；队列播完再把操作权还回去。
    /// </para>
    /// </summary>
    public static class TutorialTips
    {
        private static readonly Queue<string> Pending = new Queue<string>();

        private static bool _showing;

        /// <summary>把一条提示排进队列；没有正在显示的就立刻弹</summary>
        public static void Show(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_showing)
            {
                Pending.Enqueue(text);
                return;
            }

            ShowNow(text);
        }

        /// <summary>由 TipsPanel 在关闭时回调，接着播下一条</summary>
        public static void OnClosed()
        {
            _showing = false;

            if (Pending.Count > 0)
            {
                ShowNow(Pending.Dequeue());
                return;
            }

            RestoreInput();
        }

        /// <summary>清空队列（重开一局时用）</summary>
        public static void Clear()
        {
            Pending.Clear();
            _showing = false;
        }

        private static void ShowNow(string text)
        {
            _showing = true;

            // 显示界面就切 UI 映射，别让主角在提示期间还能动
            SwitchInput(LocalInputType.UI);

            Global.ShowUI("TipsPanel", new TipsPanelProperties { tipsText = text });
        }

        /// <summary>队列播完才把操作权还回去</summary>
        private static void RestoreInput()
        {
            // 只在自由行动时还；对话 / 商店 / 战斗的状态自己会管输入映射
            GameplayRoot root = Global.Get<GameplayRoot>();
            if (root == null || root.CurrentMode == GameplayMode.FreeRoam)
                SwitchInput(LocalInputType.Player);
        }

        private static void SwitchInput(LocalInputType type)
        {
            Global.Get<LocalInputManager>()?.SwitchMap(type);
        }
    }
}
