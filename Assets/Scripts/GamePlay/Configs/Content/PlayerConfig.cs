using UnityEngine;

namespace GamePlay.Configs
{
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Configs/PlayerConfig")]
    public class PlayerConfig : ConfigBase
    {
        // Id / Name 继承自 ConfigBase

        [Header("生命")]
        public float MaxHp = 100f;

        [Header("移动")]
        public float MoveSpeed = 15f;
        public float SprintSpeed = 40f;         // 点按喷射时的移动速度
        public float SprintDuration = 0.2f;     // 一次喷射持续多久（点按型，没有蓄力）
        public float SprintCd = 1f;             // 喷射冷却，从按下那一刻开始算，建议 >= SprintDuration

        [Header("喷射伤害（点按喷射期间每 tick 结算，尚未实现）")]
        public float SprintDamagePerTick = 4f;
        public float SprintTickInterval = 0.5f;
        public float SprintCartExtraDamage = 2f;    // 推着购物车时的额外伤害

        [Header("普通攻击 J")]
        public float PrimaryAtkInterval = 0.8f;     // 普攻间隔

        [Header("近战")]
        public float MeleeDamage = 10f;         // 近战伤害
        public float MeleeRange = 1.5f;         // 判定半径

        [Header("远程")]
        public float RangedDamage = 14f;
        public float RangedRange = 4.5f;        // 约近战 3 倍

        [Header("倍率基准")]
        public float AttackSpeed = 1f;          // 攻速倍率，道具与状态在此之上乘算

        [Header("液体切换 Tab")]
        public float ChangeLiquidCd = 10f;      // 冷却
    }
}
