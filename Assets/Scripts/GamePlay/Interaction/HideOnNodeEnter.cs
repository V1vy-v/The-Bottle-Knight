using Framework;
using GamePlay.Configs;
using GamePlay.Story;
using UnityEngine;

namespace GamePlay.Interaction
{
    /// <summary>
    /// 剧情 NPC 的退场：走到指定节点时把自己隐藏（比如门口说完话就走的雷碧）。
    /// <para>
    /// 挂在那个 NPC 的物体上即可，只用 alpha/SetActive 之外的方式表现都行 —— 这里直接 SetActive(false)，
    /// 因为它只是个摆着的立绘，不承担任何交互。
    /// </para>
    /// </summary>
    public class HideOnNodeEnter : MonoBehaviour
    {
        [Tooltip("进入哪个节点时消失（填节点 Id，比如 Node_24）。留空则永不消失")]
        [SerializeField] private string _nodeId = "Node_24";

        private bool _done;

        private void Update()
        {
            if (_done) return;

            NodeConfig node = Global.Get<GameplayRoot>()?.Story?.CurrentNode;
            if (node == null || node.Id != _nodeId) return;

            _done = true;
            gameObject.SetActive(false);
            Debug.Log($"[剧情] {name} 在节点 {_nodeId} 退场");
        }
    }
}
