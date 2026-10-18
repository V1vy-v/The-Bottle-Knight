using Framework;
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

            _fired = true;

            var root = Global.Get<GameplayRoot>();
            root?.ReportGoalReached(_goalId);
            if (_asEncounterAnchor) RegisterEncounter(root);

            Debug.Log($"[流程] 触发 {name}");
        }
    }
}