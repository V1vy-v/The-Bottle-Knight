using Framework;
using GamePlay.Session;
using GamePlay.Story;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.Configs
{
    /// <summary>
    /// 所有配置资产的统一入口。按类型分组存放，运行时按 Id 查找。
    /// 约定：
    ///   · 需要「按 Id 查」的配置登记在这里；
    ///   · 配置之间互相引用一律用 Id 字符串，不放资源引用。
    /// </summary>
    [CreateAssetMenu(fileName = "ConfigManager", menuName = "Configs/ConfigManager")]
    public class ConfigManager : ScriptableObjectSingleton<ConfigManager>
    {
        [Header("主角")]
        [SerializeField] private List<PlayerConfig> _playerConfigs = new();

        [Header("敌人")]
        [SerializeField] private List<EnemyConfig> _enemyConfigs = new();

        [Header("战斗")]
        [SerializeField] private List<BattleConfig> _battleConfigs = new();

        [Header("液体")]
        [SerializeField] private List<LiquidConfig> _liquidConfigs = new();

        [Header("Buff")]
        [SerializeField] private List<BuffConfig> _buffConfigs = new();

        [Header("剧情")]
        [SerializeField] private List<StoryConfig> _storyConfigs = new();

        [Header("流程节点")]
        [SerializeField] private List<NodeConfig> _nodeConfigs = new();

        [Header("起始节点")]
        [Tooltip("新游戏从哪个节点开始，填 NodeConfig 的 Id")]
        [SerializeField] private string _startNodeId = "Node_0";

        [Header("道具")]
        [Tooltip("道具总表，HUD 道具栏与商店界面共用。留空则道具栏不显示")]
        [SerializeField] private PropConfigs _propConfigs;

        /// <summary>道具总表（可能为空）</summary>
        public PropConfigs Props => _propConfigs;

        /// <summary>类型 -> (Id -> 配置)。同类型内 Id 必须唯一</summary>
        private readonly Dictionary<Type, Dictionary<string, ConfigBase>> _byType = new();

        /// <summary>节点 Id -> 节点</summary>
        private readonly Dictionary<string, NodeConfig> _nodes = new();

        public string StartNodeId => _startNodeId;

        private void OnEnable() => Rebuild();

        /// <summary>重建索引。Inspector 里改完列表后如果没生效，手动调一次</summary>
        public void Rebuild()
        {
            _byType.Clear();
            _nodes.Clear();

            AddGroup(_playerConfigs);
            AddGroup(_enemyConfigs);
            AddGroup(_battleConfigs);
            AddGroup(_liquidConfigs);
            AddGroup(_buffConfigs);
            AddGroup(_storyConfigs);
            AddGroup(_nodeConfigs);

            foreach (var node in _nodeConfigs)
            {
                if (node == null) continue;

                if (string.IsNullOrEmpty(node.Id))
                {
                    Debug.LogError($"[ConfigManager] 节点 Id 为空：{node.name}");
                    continue;
                }

                if (!_nodes.TryAdd(node.Id, node))
                    Debug.LogError($"[ConfigManager] 节点 Id 重复：{node.Id}（{node.name}）");
            }
        }

        private void AddGroup<T>(List<T> list) where T : ConfigBase
        {
            var dict = new Dictionary<string, ConfigBase>();
            _byType[typeof(T)] = dict;

            if (list == null) return;

            foreach (var cfg in list)
            {
                if (cfg == null) continue;

                if (string.IsNullOrEmpty(cfg.Id))
                {
                    Debug.LogError($"[ConfigManager] {typeof(T).Name} 的 Id 为空：{cfg.name}");
                    continue;
                }

                if (!dict.TryAdd(cfg.Id, cfg))
                    Debug.LogError($"[ConfigManager] {typeof(T).Name} 重复 Id：{cfg.Id}");
            }
        }

        /// <summary>按 Id 取配置（同类型内唯一）</summary>
        public bool TryGetConfig<T>(string id, out T config) where T : ConfigBase
        {
            config = null;

            if (string.IsNullOrEmpty(id)) return false;

            if (_byType.TryGetValue(typeof(T), out var dict) &&
                dict.TryGetValue(id, out var value) &&
                value is T target)
            {
                config = target;
                return true;
            }

            Debug.LogWarning($"[ConfigManager] 找不到 {typeof(T).Name}：{id}");
            return false;
        }

        /// <summary>按 Id 取流程节点</summary>
        public bool TryGetNode(string nodeId, out NodeConfig node)
        {
            if (!string.IsNullOrEmpty(nodeId) && _nodes.TryGetValue(nodeId, out node)) return true;

            node = null;
            Debug.LogWarning($"[ConfigManager] 找不到流程节点：{nodeId}");
            return false;
        }

        /// <summary>取起始节点</summary>
        public bool TryGetStartNode(out NodeConfig node) => TryGetNode(_startNodeId, out node);
    }
}
