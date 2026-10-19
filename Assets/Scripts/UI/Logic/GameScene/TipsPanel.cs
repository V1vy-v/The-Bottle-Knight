using System.Collections;
using Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// TipsPanel 的界面逻辑：显示一句教学提示，停留若干秒后自动关闭，也可以点按钮提前关。
    /// 文案由 TipsPanelProperties.tipsText 传入，来源是节点配置里的 tipText。
    /// </summary>
    public partial class TipsPanel
    {
        /// <summary>停留多久后自动关闭</summary>
        [SerializeField] private float _holdSeconds = 6f;

        private Coroutine _hideRoutine;
        private bool _closed;

        #region 生命周期

        protected override void Init()
        {
            // <ui-bind:add>
            // </ui-bind:add>
        }

        protected override void AddListener()
        {
            // 关闭按钮还没进绑定表，先按层级找；等 UIViewBindingEditor 生成后也能一起用
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveListener(CloseSelf);
                button.onClick.AddListener(CloseSelf);
            }
        }

        protected override void RemoveListener()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
                button.onClick.RemoveListener(CloseSelf);

            // <ui-bind:remove>
            // </ui-bind:remove>
            base.RemoveListener();
        }

        protected override void UpdateView()
        {
            string text = (Properties as TipsPanelProperties)?.tipsText;

            if (_tipsTxt != null)
            {
                // 自动绑定的字段是 RectTransform（绑到了文字所在节点），文字组件在这里自己取一次
                var label = _tipsTxt.GetComponent<TextMeshProUGUI>();
                if (label != null) label.text = text ?? string.Empty;
            }

            _closed = false;

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        #endregion

        #region 内部方法

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(_holdSeconds);

            CloseSelf();
        }

        /// <summary>关闭（自动超时与点按钮共用，只会生效一次）</summary>
        private void CloseSelf()
        {
            if (_closed) return;
            _closed = true;

            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            Global.HideUI("TipsPanel");

            // 队列里还有下一条就接着播，没有就把输入映射还回去
            GamePlay.TutorialTips.OnClosed();
        }

        #endregion
    }
}
