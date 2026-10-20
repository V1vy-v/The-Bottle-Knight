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

        /// <summary>字号自适应：文字多就把字号压小，保证整段塞得下（「致后来者」信件用）</summary>
        public bool autoFit;

        /// <summary>
        /// 停留多少秒后自动关闭。&lt;= 0 表示不自动关闭，等玩家按键 / 点击。
        /// 默认 -1 → 用面板上配的 _holdSeconds。
        /// </summary>
        public float holdSeconds = -1f;

        // 供预制体上的 UIView 序列化使用
        public TipsPanelProperties() { }

        public TipsPanelProperties(PanelPriority priority) : base(priority) { }
    }
}
