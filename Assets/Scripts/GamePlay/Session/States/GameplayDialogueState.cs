using Framework;
using GamePlay;
using GamePlay.Session;
using System;
using System.Diagnostics;
using UI;
using UnityEngine;

namespace GamePlay.Session
{
    public sealed class GameplayDialogueState : GameplayModeStateBase
    {
        public override GameplayMode StateKey => GameplayMode.Dialogue;
        protected override LocalInputType InputMap => LocalInputType.UI;

        //完成后自动推进
        protected override bool AutoAdvanceOnFinish => true;

        public GameplayDialogueState(GameplayRoot root, StateMachine<GameplayMode> sm)
            : base(root, sm) { }

        public override void Enter()
        {
            base.Enter();

            DialogPanel.OnDialogueCompleted += OnCompleted;

            // 对话弹出音（NPC 交谈音）
            Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("UI_NPC");

            TextAsset text = Root.Dialog.CurrentDialogue;
            if (text == null)
            {
                UnityEngine.Debug.LogWarning($"[对话] 节点 {Root.Story?.CurrentNode?.name} 没有对话文本，直接跳过");
                MarkFinished();
                return;
            }

            Global.ShowUI("DialogPanel", new DialogPanelProperties { dialogText = text });
        }

        public override void Exit()
        {
            base.Exit();
            DialogPanel.OnDialogueCompleted -= OnCompleted;
            Global.HideUI("DialogPanel");
        }

        /// <summary>空格一键跳过整段对话</summary>
        protected override void Tick(float deltaTime)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                UnityEngine.Debug.Log("[对话] 空格跳过");
                DialogPanel.SkipCurrent();
            }
        }

        private void OnCompleted()
        {
            // 终章：对话结束后把「致后来者」当弹窗显示，玩家关掉它才收尾
            var node = Root.Story != null ? Root.Story.CurrentNode : null;
            if (node != null && !string.IsNullOrEmpty(node.letterText) && !_letterShown)
            {
                _letterShown = true;
                Root.ShowEndingLetter(node.letterText);
                return;
            }

            MarkFinished();
        }

        private bool _letterShown;
    }
}
