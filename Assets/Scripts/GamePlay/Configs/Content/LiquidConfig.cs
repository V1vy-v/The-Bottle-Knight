using UnityEngine;

namespace GamePlay.Configs
{
    /// <summary>
    /// 液体类型。区分三类字段集不同的液体，规则与被动生效位置由它决定。
    /// </summary>
    public enum LiquidKind
    {
        Active = 0,          // 主动形态：国窖 / 快乐水 / 雷碧，靠 K 释放
        MainPassive = 1,     // 主形态被动：三拳，装备在主槽即生效，无冷却
        StandbyPassive = 2,  // 备用槽被动：料酒 / 冰美式，只在备用槽生效
    }

    public enum LiquidType
    { 
        Cola,
        GuoJiao, 
        LeiBi, 
        LiaoJiu, 
        BingMeiShi, 
        SanQuan 
    }

    [CreateAssetMenu(fileName = "LiquidConfig", menuName = "Configs/LiquidConfig")]
    public class LiquidConfig : ConfigBase
    {
        [Header("类型")]
        public LiquidType Type;
        public LiquidKind Kind = LiquidKind.Active;
        public bool CanBeMain = true;           // 能否作主形态（冰美式 / 料酒 false）

        [Header("表现")]
        [Tooltip("技能栏上显示的图标。国窖 / 快乐水 / 三拳 / 雷碧 / 冰美式 / 料酒 各一张")]
        public Sprite Icon;


        [Header("普攻 / 弹幕")]
        public int Damage;                      // 普攻或弹幕伤害
        public float AttackInterval;            // 攻击间隔
        public string BulletPrefabRes;          // 弹幕预制体路径

        [Header("K 技能（主动形态）")]
        public float SkillDamage;               // 国窖 50（篮球质变 100）
        public float SkillCd;                   // 雷碧 8 / 快乐水 12 —— 持续结束后才开始计时
        public float SkillDuration;             // 国窖 5 / 快乐水 10 / 雷碧 8

        [Header("持续期增益")]
        public float AtkBonus;                  // 快乐水 0.20
        public float MoveSpeedBonus;            // 快乐水 0.10 / 雷碧 0.60
        public float RangeBonus;                // 快乐水 0.20
        public float AttackSpeedBonus;          // 冰美式 0.15 / 料酒 0.20 / 国窖冷静 0.20
        public bool HasKnockback;               // 快乐水涌动附带击退

        [Header("国窖 · 红温")]
        public float RagePerHit = 10f;
        public float RageDecayPerSec = 10f;
        public float RageMax = 100f;
        public float RageDecayDelay = 5f;       // 5 秒内无命中才开始衰减
        public float ChillSlow;                 // 冷静期间命中减速 0.20

        [Header("快乐水 · 曼妥思")]
        public float ExtendPerHit = 0.5f;       // 每次命中延长涌动
        public float ExtendMax = 5f;            // 延长上限

        [Header("雷碧 · 穿过")]
        public float PassThroughDamage = 15f;
        public float PassThroughRadiusScale = 1f; // 闪电五连鞭 1.5

        [Header("备用槽被动")]
        public float PassiveMissChance;         // 料酒：自身命中失败 0.10
        public float EnemyMissChance;           // 料酒：周围敌人命中失败 0.30
    }
}
