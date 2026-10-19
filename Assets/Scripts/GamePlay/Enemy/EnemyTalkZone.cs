using UnityEngine;

namespace GamePlay.Enemy
{
    /// <summary>
    /// 对话触发范围的标记，挂在敌人的大号交互碰撞体所在的物体上。
    /// 两个碰撞体都在 Interactable 层：
    ///   InteractPoint（小）→ 吸取，由 AbsorbSource 提供 IInteractable
    ///   TalkZone（大）    → 对话，靠这个标记区分
    /// PlayerInteractor 扫到带标记的碰撞体才算对话目标，不带标记的才参与吸取判定。
    /// </summary>
    public class EnemyTalkZone : MonoBehaviour { }
}

