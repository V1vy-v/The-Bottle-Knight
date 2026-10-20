using Framework;
using GamePlay.Configs;
using GamePlay.Enemy;
using GamePlay.Story;
using UnityEngine;

namespace GamePlay.Interaction
{
    /// <summary>
    /// 剧情单位：走到指定节点时原地倒地（被雷碧打晕的中三拳），倒地后就能用 F 吸取。
    /// <para>
    /// 比「开局就在地上躺着」更贴合演出 —— 门口那段对话里才被打晕。
    /// 挂在场景里那只敌人身上（和 EnemyController 同物体），并把 EnemyController 勾成
    /// 「被动」（_passiveAtStart 或留空 talkGoalId 不用填），否则它会在对话期间追着主角打。
    /// </para>
    /// <para>
    /// 同物体上还需要 AbsorbSource，且 EnemyConfig 的 CanAbsorb = 1、AbsorbLiquid 填三拳。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class KnockOutOnNode : MonoBehaviour
    {
        [Tooltip("进入哪个节点时倒地（填节点 Id，比如 Node_24）。留空则永远不倒")]
        [SerializeField] private string _nodeId = "Node_24";

        private EnemyController _enemy;
        private bool _done;

        private void Awake() => _enemy = GetComponent<EnemyController>();

        private void Update()
        {
            if (_done || _enemy == null) return;

            NodeConfig node = Global.Get<GameplayRoot>()?.Story?.CurrentNode;
            if (node == null || node.Id != _nodeId) return;

            _done = true;
            _enemy.ForceDefeated();
            Debug.Log($"[剧情] {name} 在节点 {_nodeId} 被打晕，可以吸取了");
        }
    }
}
