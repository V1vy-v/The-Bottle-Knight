using Framework;
using GamePlay.Procedure;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// BeginPanel 的界面逻辑
    /// </summary>
    public partial class BeginPanel
    {
        #region 生命周期

        protected override void Init()
        {
            // TODO: 初始化界面数据与控件状态
            // <ui-bind:add>
            _beginBtn.onClick.AddListener(OnBeginBtn);
            _quitBtn.onClick.AddListener(OnQuitBtn);
            _settingBtn.onClick.AddListener(OnSettingBtn);
            // </ui-bind:add>
        }

        protected override void AddListener()
        {
            // TODO: 订阅界面以外的事件
        }

        protected override void RemoveListener()
        {
            // TODO: 取消订阅，并交给基类清理控制器事件
            // <ui-bind:remove>
            _beginBtn.onClick.RemoveListener(OnBeginBtn);
            _quitBtn.onClick.RemoveListener(OnQuitBtn);
            _settingBtn.onClick.RemoveListener(OnSettingBtn);
            // </ui-bind:remove>
            base.RemoveListener();
        }

        protected override void UpdateView()
        {
            // TODO: 用界面属性刷新控件显示
        }

        #endregion

        #region UI回调

        private void OnBeginBtn()
        {
            // 走 RequestNewGame：通关回菜单后再点开始，才不会掉进 ContinueGame（还没有存档）
            ProcedureCore.Instance.RequestNewGame();
        }

        private void OnQuitBtn()
        {
            // TODO: _quitBtn 点击后的逻辑
            Application.Quit();
        }

        private void OnSettingBtn()
        {
            Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("UI_Open");
            Global.ShowUI("SettingPanel");
        }

        #endregion
    }
}
