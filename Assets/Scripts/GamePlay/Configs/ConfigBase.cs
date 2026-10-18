using UnityEngine;
using UnityEngine.Serialization;

namespace GamePlay.Configs
{
    public class ConfigBase : ScriptableObject
    {
        [Header("基础信息")]
        // NodeConfig 以前用 nodeId，字段并进来之后靠这行把旧资产里的数据迁到 Id。
        // 其它配置本来就叫 Id，不受影响。
        [FormerlySerializedAs("nodeId")]
        public string Id;

        public string Name;
    }
}
