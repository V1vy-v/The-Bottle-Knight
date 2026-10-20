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
            Global.ShowUI("DialogPanel", new DialogPanelProperties { dialogText = text });
        }

        public override void Exit()
        {
            base.Exit();
            DialogPanel.OnDialogueCompleted -= OnCompleted;
            Global.HideUI("DialogPanel");
        }

        private void OnCompleted()
        {
            MarkFinished();
        }
    }
}
