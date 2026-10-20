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

            if (_tipsTxt != null)
            {
                // 自动绑定的字段是 RectTransform（绑到了文字所在节点），文字组件在这里自己取一次
                var label = _tipsTxt.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    // 信件这类长文本：开字号自适应，文字多了自动缩小，保证整段塞得下
                    if (props != null && props.autoFit)
                    {
                        label.enableAutoSizing = true;
                        label.fontSizeMin = 12f;
                        if (label.fontSizeMax < 24f) label.fontSizeMax = 46f;
                    }
                    else
                    {
                        label.enableAutoSizing = false;
                    }

                    label.text = text ?? string.Empty;
                }
            }

            _closed = false;

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

        /// <summary>本次显示是否「不自动关闭」（凭按键 / 点击关闭）</summary>
        private float _holdOverride = -1f;

        private void Update()
        {
            // 只有「不自动关闭」的弹窗（信件）才靠按键关闭，普通教学提示到点自己消失
            if (_closed || _holdOverride > 0f) return;
            if (_hideRoutine != null) return;

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
