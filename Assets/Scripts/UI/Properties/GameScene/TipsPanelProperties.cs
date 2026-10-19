using System;
using Framework;

namespace UI
{
    /// <summary>
    /// TipsPanel 的界面数据，打开界面时传入
    /// </summary>
    [Serializable]
    public class TipsPanelProperties : PanelProperties
    {
        /// <summary>要显示的教学文案</summary>
        public string tipsText;

        // 供预制体上的 UIView 序列化使用
        public TipsPanelProperties() { }

        public TipsPanelProperties(PanelPriority priority) : base(priority) { }
    }
}
