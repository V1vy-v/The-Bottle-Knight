using Framework;
using GamePlay.Story;
using System.Collections.Generic;
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
        [Tooltip("勾上则把自己登记为本场战斗的遭遇点：战斗开始时隐藏整个占位物，并在占位贴图的位置生成敌人")]
        [SerializeField] private bool _asEncounterAnchor;

        [Tooltip("刷怪位置（一张占位贴图一个）。留空则用「自己 + 所有子物体」——把多余的占位贴图拖成自己的子物体即可")]
        [SerializeField] private Transform[] _spawnPoints;
        /// <summary>触发后把自己隐藏（用于一次性剧情 NPC，如快乐水）</summary>
        [SerializeField] private bool _hideAfterFire;
        private bool _fired;

        /// <summary>
        /// 登记遭遇点。刷怪位置优先用 _spawnPoints；留空则用「自己 + 所有子物体」，
        /// 也就是每张占位贴图一个位置（把多余的占位贴图拖成自己的子物体即可）。
        /// 战斗开始时隐藏的是自己这个物体，所以子物体里的贴图会一起消失。
        /// </summary>
        private void RegisterEncounter(GameplayRoot root)
        {
            if (root == null) return;

            var points = new List<Transform>();

            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                points.AddRange(_spawnPoints);
            }
            else
            {
                points.Add(transform);                                  // 自己也是一张占位贴图
                for (int i = 0; i < transform.childCount; i++)
                    points.Add(transform.GetChild(i));
            }

            root.SetEncounter(gameObject, points);
        }

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_once && _fired) return;
            if (other.GetComponentInParent<Player.PlayerController>() == null) return;

            var root = Global.Get<GameplayRoot>();
            if (root == null) return;

            // 当前节点不是在等这个目标（比如还在「吸取雷碧」的漫游节点）就什么都别做，
            // 更不要把自己标记成已触发 —— 否则玩家顺手先踩到下一个触发点，
            // 等真正走到这一步时触发器已经哑了，流程再也推不动（开放地图里很容易发生）。
            NodeConfig node = root.Story?.CurrentNode;
            if (node != null && !string.IsNullOrEmpty(node.goalId) && node.goalId != _goalId)
            {
                Debug.Log($"[流程] {name} 暂不触发：当前节点 {node.Id} 等的是 {node.goalId}，本触发器报 {_goalId}");
                return;
            }

            _fired = true;

            root.ReportGoalReached(_goalId);
            if (_asEncounterAnchor) RegisterEncounter(root);
            if (_hideAfterFire) gameObject.SetActive(false);

            Debug.Log($"[流程] 触发 {name}");
        }
    }
}