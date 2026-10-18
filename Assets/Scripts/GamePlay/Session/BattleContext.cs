using GamePlay.Configs;
using GamePlay.Story;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.Session
{
    /// <summary>
    /// 战斗上下文：根据节点加载对应战斗，并管理战斗运行时状态
    /// </summary>
    public class BattleContext
    {
        /// <summary>当前战斗配置</summary>
        public BattleConfig CurrentBattle { get; private set; }

        /// <summary>还活着的敌人数</summary>
        public int AliveEnemyCount { get; private set; }

        /// <summary>本场战斗是否已胜利</summary>
        public bool IsVictory { get; private set; }

        /// <summary>当前是否处于战斗中</summary>
        public bool IsInBattle => AliveEnemyCount > 0 && !IsVictory;

        /// <summary>根据节点刷新战斗配置</summary>
        public void RefreshByNode(NodeConfig node)
        {
            if (node == null)
            {
                Debug.LogWarning("[BattleContext] NodeConfig 为空");
                CurrentBattle = null;
                return;
            }

            CurrentBattle = node.battleConfig;
            if (CurrentBattle != null)
                Debug.Log($"[BattleContext] 切换战斗: {node.Id} -> {CurrentBattle.Id}");
        }

        /// <summary>开始战斗（进入战斗模式时调用）</summary>
        public void StartBattle(int enemyCount)
        {
            if (enemyCount <= 0)
            {
                return;
            }

            AliveEnemyCount = enemyCount;
            IsVictory = false;
        }

        /// <summary>一只敌人死亡</summary>
        public void OnEnemyDead()
        {
            if (AliveEnemyCount <= 0) return;

            AliveEnemyCount = Mathf.Max(0, AliveEnemyCount - 1);

            if (AliveEnemyCount <= 0)
                MarkVictory();
        }

        /// <summary>战斗胜利</summary>
        public void MarkVictory()
        {
            if (IsVictory) return;

            IsVictory = true;
        }

        /// <summary>清理战斗状态（离开战斗模式时调用）</summary>
        public void Clear()
        {
            AliveEnemyCount = 0;
            IsVictory = false;
        }
    }
}