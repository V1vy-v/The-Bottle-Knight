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

        [Header("刷怪点")]
        [Tooltip("场景里的刷怪锚点物体名（如 [IceCafeArena]）。填了就用它 + 所有子物体当刷怪位置，" +
                 "并隐藏它们的贴图；留空则用剧情触发器登记的遭遇点，两者都没有时围绕主角散开")]
        public string SpawnAnchorName;

        [Header("音乐")]
        [Tooltip("进战斗时播的 BGM 键。留空用 Combat_Light；BOSS 战填 Combat_Boss")]
        public string BgmKey = "";

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
