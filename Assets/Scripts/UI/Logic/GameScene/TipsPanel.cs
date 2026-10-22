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
            var props = Properties as TipsPanelProperties;
            string text = props?.tipsText;

            // 尺寸 / 锚点 / 字号 / 自适应全部交给预制体（LayoutGroup + ContentSizeFitter + TMP 自适应），
            // 代码这里只负责把文字塞进去。
            if (_tipsTxt != null)
            {
                // 自动绑定的字段是 RectTransform（绑到了文字所在节点），文字组件在这里自己取一次
                var label = _tipsTxt.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = text ?? string.Empty;
                    LogLayoutDiagnostics(label);
                }
            }

            _closed = false;
            _shownAt = Time.unscaledTime;

            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            // <= 0 表示不自动关，等玩家按键 / 点击（信件）
            float hold = props != null && props.holdSeconds >= 0f ? props.holdSeconds : _holdSeconds;
            _holdOverride = props != null ? props.holdSeconds : -1f;

            if (hold > 0f)
                _hideRoutine = StartCoroutine(HideAfterDelay(hold));
        }

        /// <summary>
        /// 临时诊断：把面板 / 文字框的实际尺寸、锚点、父节点链、画布缩放、字体首选尺寸打出来，
        /// 用来定位「提示框超出屏幕」。确认没问题后可以删掉这段。
        /// </summary>
        private void LogLayoutDiagnostics(TextMeshProUGUI label)
        {
            var self = transform as RectTransform;
            var canvas = GetComponentInParent<Canvas>();

            Vector2 preferred = label.GetPreferredValues(label.text);
            Vector2 panelSize = self != null ? self.rect.size : Vector2.zero;
            Vector2 textSize = _tipsTxt != null ? _tipsTxt.rect.size : Vector2.zero;

            // 父链：一层层打印名字 / 尺寸 / 锚点，看是「预制体数值被写坏了」还是「父节点是 0 尺寸」
            var chain = new System.Text.StringBuilder();
            Transform p = self != null ? self.parent : null;
            int depth = 0;
            while (p != null && depth < 5)
            {
                var prt = p as RectTransform;
                if (prt != null)
                    chain.Append($"[{prt.name} size={prt.rect.width:F0}x{prt.rect.height:F0} " +
                                 $"a=({prt.anchorMin.x:F2},{prt.anchorMin.y:F2})~({prt.anchorMax.x:F2},{prt.anchorMax.y:F2}) " +
                                 $"sd={prt.sizeDelta.x:F0},{prt.sizeDelta.y:F0}] ");
                else
                    chain.Append($"[{p.name} (非RectTransform)] ");

                p = p.parent;
                depth++;
            }

            Debug.Log($"[TipsPanel] 屏幕={Screen.width}x{Screen.height} 画布缩放={(canvas != null ? canvas.scaleFactor : -1f):F2} " +
                      $"面板={panelSize.x:F0}x{panelSize.y:F0} " +
                      $"sd={(self != null ? self.sizeDelta.x : 0):F0},{(self != null ? self.sizeDelta.y : 0):F0} " +
                      $"txtAnchor={(self != null ? self.anchorMin.x : 0):F2}~{(self != null ? self.anchorMax.x : 0):F2} " +
                      $"文字框={textSize.x:F0}x{textSize.y:F0} " +
                      $"字号={label.fontSize:F0}({label.fontSizeMin:F0}~{label.fontSizeMax:F0},自适应={label.enableAutoSizing}) " +
                      $"文字首选={preferred.x:F0}x{preferred.y:F0} 长度={(label.text != null ? label.text.Length : 0)}\n" +
                      $"父链: {chain}");
        }

        /// <summary>本次显示是否「不自动关闭」（凭按键 / 点击关闭）</summary>
        private float _holdOverride = -1f;

        /// <summary>弹出时刻：跳过刚弹出来那一帧的按键，避免「按空格结束对话」顺手把信件关了</summary>
        private float _shownAt;

        private void Update()
        {
            // 只有「不自动关闭」的弹窗（信件）才靠按键关闭，普通教学提示到点自己消失
            if (_closed || _holdOverride > 0f) return;
            if (_hideRoutine != null) return;
            if (Time.unscaledTime - _shownAt < 0.35f) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            bool pressed = (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                           || (mouse != null && mouse.leftButton.wasPressedThisFrame);

            if (pressed) CloseSelf();
        }

        #endregion

        #region 内部方法

        private IEnumerator HideAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);

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
