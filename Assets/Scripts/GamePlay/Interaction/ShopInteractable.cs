using Framework;
using GamePlay.Session;
using GamePlay.Configs;
using GamePlay.Player;
using GamePlay.Story;
using UnityEngine;

namespace GamePlay.Interaction
{
    /// <summary>
    /// 无盖者商店的交互物：按 F 打开商店界面。
    /// 厨房抽屉、超市收银台、超市货架夹层三处共用这一个组件（界面是同一套 ShopPanel）。
    /// </summary>
    public class ShopInteractable : MonoBehaviour, IInteractable
    {
        [Header("提示")]
        [SerializeField] private string _prompt = "按 F 打开商店";

        [Header("前置条件")]
        [Tooltip("为空表示随时可交互；填了的话要求当前节点 Id 等于它才能开")]
        [SerializeField] private string _requireNodeId = "";

        /// <summary>无盖者商店的优先级：剧情 > 商店 > 吸取（AbsorbSource 是 10）</summary>
        public int Priority => 50;

        public Transform PromptAnchor => transform;

        public bool CanInteract
        {
            get
            {
                GameplayRoot root = Global.Get<GameplayRoot>();

                // 只在自由行动时能开商店：切到 Shop 状态会把 Battle 状态顶掉，
                // 战斗中开界面会直接让这场战斗被跳过。
                if (root == null || root.CurrentMode != GameplayMode.FreeRoam) return false;

                if (string.IsNullOrEmpty(_requireNodeId)) return true;

                NodeConfig node = root.Story?.CurrentNode;
                return node != null && node.Id == _requireNodeId;
            }
        }

        public string GetPromptText(PlayerLiquids liquids)
        {
            return _prompt;
        }

        public void Interact(GameObject player, PlayerLiquids liquids)
        {
            if (!CanInteract) return;

            // 交给状态机处理：切 InputMap、显示界面、关闭后回漫游
            Global.Get<GameplayRoot>()?.ChangeMode(GameplayMode.Shop);
        }
    }
}
