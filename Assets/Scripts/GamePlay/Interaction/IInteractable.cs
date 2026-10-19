using GamePlay.Player;
using UnityEngine;

namespace GamePlay.Interaction
{
    /// <summary>可被主角用 F 交互的对象</summary>
    public interface IInteractable
    {
        /// <summary>当前是否可交互（剧情阶段、是否已用过等前置条件）</summary>
        bool CanInteract { get; }

        /// <summary>候选重叠时的取舍权重，大者优先。剧情 > 商店 > 吸取</summary>
        int Priority { get; }

        /// <summary>可交互的最远距离（按 PromptAnchor 到主角的距离算）。决定提示显示与 F 键是否生效</summary>
        float InteractRadius { get; }

        /// <summary>提示文字的显示距离，可以比 InteractRadius 大得多（用于常驻名字）</summary>
        float PromptRadius { get; }

        /// <summary>提示文案，如「按 F 打开抽屉」；返回 null 表示不显示提示</summary>
        string GetPromptText(PlayerLiquids liquids);

        /// <summary>提示挂点。通常直接 return transform</summary>
        Transform PromptAnchor { get; }

        /// <summary>执行交互。player 传主角根物体，交互物自己去 GetComponent 需要的东西</summary>
        void Interact(GameObject player, PlayerLiquids liquids);

    }
}