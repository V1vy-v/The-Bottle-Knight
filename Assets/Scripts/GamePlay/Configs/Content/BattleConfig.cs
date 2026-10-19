using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.Configs
{
    /// <summary>敌人配置 + 数量</summary>
    [System.Serializable]
    public class EnemySpawnEntry
    {
        public EnemyConfig Config;
        public int Count = 1;
    }

    [CreateAssetMenu(fileName = "BattleConfig", menuName = "Configs/BattleConfig")]
    public class BattleConfig : ConfigBase
    {
        [Header("敌人列表")]
        public List<EnemySpawnEntry> Enemies = new();

        [Header("奖励")]
        [Tooltip("胜利后额外奖励的瓶盖（擂台每轮 1 个；BOSS 战自身有掉落，一般填 0）")]
        public int VictoryCaps;

        /// <summary>总敌人数</summary>
        public int TotalCount
        {
            get
            {
                int sum = 0;
                foreach (var e in Enemies)
                    if (e.Config != null) sum += e.Count;
                return sum;
            }
        }
    }
}
