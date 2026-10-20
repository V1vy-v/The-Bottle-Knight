using Framework;
using UnityEngine;

namespace UI
{
    public partial class DialogPanel
    {
        public static System.Action OnDialogueCompleted;

        /// <summary>当前打开的对话面板（一键跳过用；同时只开一个）</summary>
        public static DialogPanel Current { get; private set; }

        private DiaLogController _dialogController;

        protected override void Init()
        {
            _dialogController = GetComponent<DiaLogController>();

            // 传入所有 UI 引用
            _dialogController.SetupUI(
                _roleA, _roleB, _nameText, _dialogText,
                _bK,           // next 按钮
                null, null);  

            _dialogController.OnDialogueEnd += HandleDialogueEnd;
            _bK.onClick.AddListener(OnBK);
        }

        /// <summary>一键跳过整段对话（空格触发）</summary>
        public static void SkipCurrent()
        {
            Current?.SkipDialogue();
        }

        public void SkipDialogue()
        {
            _dialogController?.SkipAll();
        }

        protected override void AddListener() { }

        protected override void RemoveListener()
        {
            if (Current == this) Current = null;
            _bK.onClick.RemoveListener(OnBK);
            _dialogController.OnDialogueEnd -= HandleDialogueEnd;
            base.RemoveListener();
        }

        protected override void UpdateView() { }

        protected override void OnShow()
        {
            Current = this;

            if (Properties is DialogPanelProperties props && props.dialogText != null)
            {
                _dialogController?.SetDialogContent(props.dialogText);
            }
            else
            {
                Debug.LogWarning("[DialogPanel] 没有传入 dialogText");
            }
        }

        private void HandleDialogueEnd()
        {
            OnDialogueCompleted?.Invoke();
        }

        private void OnBK() => _dialogController?.OnClickNext();
    }
}