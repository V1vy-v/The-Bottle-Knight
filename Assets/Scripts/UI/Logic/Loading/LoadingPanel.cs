namespace UI
{
    /// <summary>
    /// LoadingPanel 的界面逻辑
    /// </summary>
    public partial class LoadingPanel
    {
        #region 生命周期

        protected override void Init()
        {
            // TODO: 初始化界面数据与控件状态
            // <ui-bind:add>
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
            // </ui-bind:remove>
            base.RemoveListener();
        }

        protected override void UpdateView()
        {
            // TODO: 用界面属性刷新控件显示
        }

        #endregion
    }
}
