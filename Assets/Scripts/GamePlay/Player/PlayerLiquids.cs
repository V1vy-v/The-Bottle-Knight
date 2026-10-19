using GamePlay.Configs;
using GamePlay.Liquid;
using System;
using UnityEngine;

namespace GamePlay.Player
{
    public enum LiquidTankType
    {
        Current = 0,        //主槽
        Standby = 1,        //备用槽
    }
    /// <summary>
    /// 主角液体组件
    /// </summary>
    public class PlayerLiquids
    {
        private PlayerStats _stats;
        private float _cdRemain;
        private float _elapsed;
        // 液体槽
        public LiquidBase CurrentLiquid;
        public LiquidBase StandbyLiquid;

        #region 聚合属性
        /// <summary>备用槽里真正生效的被动来源（有主动形态的不提供备用槽被动）</summary>
        public LiquidBase StandbyPassive =>
            StandbyLiquid != null && StandbyLiquid.Config.Kind == LiquidKind.StandbyPassive ? StandbyLiquid : null;

        /// <summary>正在持续的技能形态</summary>
        private LiquidBase ActiveSkill =>
            CurrentLiquid != null && CurrentLiquid.IsSkillActive ? CurrentLiquid : null; 

        public float AtkBonus => ActiveSkill?.Config.AtkBonus ?? 0f;
        public float MoveSpeedBonus => ActiveSkill?.Config.MoveSpeedBonus ?? 0f;
        public float RangeBonus => ActiveSkill?.Config.RangeBonus ?? 0f;
        public float AttackSpeedBonus => (StandbyPassive?.Config.AttackSpeedBonus ?? 0f)  + (ActiveSkill?.Config.AttackSpeedBonus ?? 0f);
        public float SelfMissChance => StandbyPassive?.Config.PassiveMissChance ?? 0f;
        public float EnemyMissChance => StandbyPassive?.Config.EnemyMissChance ?? 0f;
        #endregion

        #region 事件
        public event Action OnChangeLiquid;
        public event Action OnGainLiquid;
        public event Action OnPourOutLiquid;
        #endregion

        public void Init(PlayerStats stats)
        {
            _stats = stats;
        }

        /// <summary>
        /// 切换液体，备用液体必须可作主形态
        /// </summary>
        public bool ChangeLiquid()
        {
            if (StandbyLiquid == null || !StandbyLiquid.Config.CanBeMain)
                return false;
            if (_elapsed > 0)
                return false;

            _elapsed = _stats.ChangeLiquidCd;
            LiquidBase tmp = CurrentLiquid;
            CurrentLiquid = StandbyLiquid;
            StandbyLiquid = tmp;

            tmp?.ForceEndSkill();
            OnChangeLiquid?.Invoke();
            return true;
        }
        /// <summary>
        /// 吸取液体：装进备用槽，槽里有液体就直接替换掉
        /// （策划案：吸取的新液体会默认置入备用槽液体并替换其中液体）。
        /// 要无视槽位塞主槽请用 ForceGetLiquid。
        /// </summary>
        public bool GetLiquid(LiquidConfig cfg)
        {
            StandbyLiquid = LiquidFactory.Create(cfg, _stats);
            if (StandbyLiquid == null) { Debug.LogError($"[PlayerLiquids] 没有对应液体类: {cfg?.Id}"); return false; }

            OnGainLiquid?.Invoke();
            return true;
        }
        /// <summary>
        /// 倾倒液体，备用槽必须为非空
        /// </summary>
        public bool PourOutLiquid()
        {
            if (StandbyLiquid == null)
                return false;

            StandbyLiquid = null;
            OnPourOutLiquid?.Invoke();
            return true;
        }
        /// <summary>
        /// 强制吸取液体
        /// </summary>
        public bool ForceGetLiquid(LiquidConfig cfg, LiquidTankType type)
        {
            var liquid = LiquidFactory.Create(cfg, _stats);
            if (liquid == null) 
            { 
                Debug.LogError($"[PlayerLiquids] 没有对应液体类: {cfg?.Id}"); 
                return false;
            }

            switch (type)
            {
                case LiquidTankType.Current:
                    CurrentLiquid = liquid;
                    break;
                case LiquidTankType.Standby:
                    StandbyLiquid = liquid;
                    break;
            }

            OnGainLiquid?.Invoke();
            return true;
        }

        /// <summary>
        /// 切换液体CD Tick
        /// </summary>
        /// <param name="delta"></param>
        public void TickChangeLiquid(float delta)
        {
            _elapsed = Mathf.Max(0f, _elapsed - delta);
        }

        #region 给 UI 的只读状态

        /// <summary>备用槽里是否有能换到主槽的液体（HUD 用它判断切换键解不解锁）</summary>
        public bool HasSwitchableLiquid =>
            StandbyLiquid != null
            && StandbyLiquid.Config != null
            && StandbyLiquid.Config.CanBeMain;

        /// <summary>切换液体剩余冷却秒数，供 UI 读取</summary>
        public float ChangeLiquidCdRemain => _elapsed;

        /// <summary>切换液体冷却总时长，供 UI 读取</summary>
        public float ChangeLiquidCdTotal => _stats != null ? _stats.ChangeLiquidCd : 0f;

        /// <summary>现在能不能切（备用槽可换 且 不在冷却中）</summary>
        public bool CanChangeLiquid => HasSwitchableLiquid && _elapsed <= 0f;

        #endregion
    }
}
