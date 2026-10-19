using System;
using Framework;
using GamePlay.Procedure;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// MenuPanel 的界面逻辑：Esc 打开，继续游戏 / 设置 / 退出。
    /// 关闭时通过 <see cref="OnMenuClosed"/> 通知 GameplayPauseState 恢复。
    /// </summary>
    public partial class MenuPanel
    {
        /// <summary>界面关闭（GameplayPauseState 据此 Resume）</summary>
        public static event Action OnMenuClosed;

        private Button _continue;
        private Button _setting;
        private Button _quit;
        private Button _saveAndExit;

        #region 生命周期

        protected override void Init()
        {
            // 什么都不做：UIController.Awake 是「先 AddListener 再 Init」，
            // 在这里缓存按钮的话，AddListener 拿到的全是 null。
        }

        protected override void AddListener()
        {
            // 绑定在 Awake 之前就写好了，这里自己取一次（自动绑定拿到的是 RectTransform）
            _continue = AsButton(_continueBtn);
            _setting = AsButton(_settingBtn);
            _quit = AsButton(_quitBtn);
            _saveAndExit = AsButton(_saveAndExitBtn);

            if (_continue != null) _continue.onClick.AddListener(CloseSelf);
            if (_setting != null) _setting.onClick.AddListener(OpenSetting);
            if (_quit != null) _quit.onClick.AddListener(QuitToMenu);
            if (_saveAndExit != null) _saveAndExit.onClick.AddListener(QuitToMenu);

            // 设置面板还没做，先置灰免得点了没反应
            if (_setting != null) _setting.interactable = false;
        }

        protected override void RemoveListener()
        {
            if (_continue != null) _continue.onClick.RemoveListener(CloseSelf);
            if (_setting != null) _setting.onClick.RemoveListener(OpenSetting);
            if (_quit != null) _quit.onClick.RemoveListener(QuitToMenu);
            if (_saveAndExit != null) _saveAndExit.onClick.RemoveListener(QuitToMenu);

            base.RemoveListener();
        }

        protected override void UpdateView()
        {
            // 菜单没有需要刷新的动态数据
        }

        #endregion

        #region 内部方法

        private static Button AsButton(RectTransform rect)
        {
            return rect != null ? rect.GetComponent<Button>() : null;
        }

        private void CloseSelf()
        {
            OnMenuClosed?.Invoke();
        }

        private void OpenSetting()
        {
            // 设置面板还没做（目前只有音量设置，音频也还没接入），按钮已置灰
            Debug.LogWarning("[MenuPanel] 设置面板还没做");
        }

        private void QuitToMenu()
        {
            // 存档系统还没做，保存并退出与直接退出暂时同一个行为
            ProcedureCore.Instance.RequestMenu();
        }

        #endregion
    }
}
