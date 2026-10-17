using Framework;
using UnityEngine;

namespace GamePlay.Interaction
{
    /// <summary>
    /// 走进即宣告「本节点目标完成」的区域触发器（出口、传送点、抵达点）。
    /// 挂载要求：Collider2D 勾 IsTrigger，**Layer 必须是 Obstacle**。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FlowTrigger : MonoBehaviour
    {
        [SerializeField] private bool _once = true;

        [Tooltip("与节点 goalId 匹配；留空接受任何节点")]
        [SerializeField] private string _goalId = "flow";
        [Tooltip("勾上则把自己注册为本场战斗的刷怪锚点，并在敌人生成后被隐藏")]
        [SerializeField] private bool _asEncounterAnchor;
        private bool _fired;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_once && _fired) return;
            if (other.GetComponentInParent<Player.PlayerController>() == null) return;

            _fired = true;

            var root = Global.Get<GameplayRoot>();
            root?.ReportGoalReached(_goalId);
            if (_asEncounterAnchor) root?.SetEncounterAnchor(transform);

            Debug.Log($"[流程] 触发 {name}");
        }
    }
}