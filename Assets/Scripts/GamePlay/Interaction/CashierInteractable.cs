using Framework;
using GamePlay.Configs;
using GamePlay.Player;
using GamePlay.Session;
using UnityEngine;

namespace GamePlay.Interaction
{
    /// <summary>
    /// 超市收银台：按 F 领一次东西（策划案里是送曼妥思），<b>不是商店界面</b>。
    /// 想让它同时卖东西就再挂一个 <see cref="ShopInteractable"/>（两个 IInteractable 同物体时按 Priority 取）。
    /// </summary>
    public class CashierInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("领取的道具（曼妥思就是 PropConfig 8）")]
        [SerializeField] private PropConfig _gift;

        [SerializeField] private string _prompt = "收银台";

        [TextArea(1, 3)]
        [Tooltip("领取后弹一次的提示，留空不弹")]
        [SerializeField] private string _tip = "收银台送你一颗曼妥思：快乐水涌动期间命中可以续时长。";

        [SerializeField] private float _interactRadius = 8f;
        [SerializeField] private float _promptRadius = 99999f;

        /// <summary>本次对局是否已经领过（不对局间保存）</summary>
        private bool _given;

        public int Priority => 50;
        public float InteractRadius => _interactRadius;
        public float PromptRadius => _promptRadius;
        public Transform PromptAnchor => transform;

        public bool CanInteract
        {
            get
            {
                if (_given) return false;

                GameplayRoot root = Global.Get<GameplayRoot>();
                return root != null && root.CurrentMode == GameplayMode.FreeRoam;
            }
        }

        public string GetPromptText(PlayerLiquids liquids) => _given ? "已经领过了" : _prompt;

        public void Interact(GameObject player, PlayerLiquids liquids)
        {
            if (!CanInteract) return;

            _given = true;

            if (_gift != null) _gift.isHold = true;

            // 赠品也要立刻生效
            var pc = player != null ? player.GetComponent<PlayerController>() : null;
            pc?.PlayerStats?.RecalcProps(GamePlay.Configs.ConfigManager.Instance != null
                ? GamePlay.Configs.ConfigManager.Instance.Props
                : null);

            Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("UI_Hover");

            if (!string.IsNullOrEmpty(_tip))
                TutorialTips.Show(_tip);

            Debug.Log($"[收银台] 领取 {(_gift != null ? _gift.name : "（没配道具）")}");
        }
    }
}
