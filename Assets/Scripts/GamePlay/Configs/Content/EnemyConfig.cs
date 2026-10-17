using UnityEngine;

namespace GamePlay.Configs
{
    /// <summary>
    /// 敌人分级。决定掉落、抗性与出场方式。
    /// </summary>
    public enum EnemyTier
    {
        Minion = 0,    // 小怪
        Elite = 1,     // 精英，掉 1 瓶盖
        MiniBoss = 2,  // 小 BOSS（如料酒），可吸，掉 2 瓶盖
        Boss = 3,      // BOSS，可吸，掉 2 瓶盖
        Scripted = 4,  // 剧情单位（中三拳），无战斗
    }

    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Configs/EnemyConfig")]
    public class EnemyConfig : ConfigBase
    {
        [Header("基础数值")]
        public EnemyTier Tier = EnemyTier.Minion;
        public float MaxHp = 50f;
        public float Damage = 8f;
        public float MoveSpeed = 2f;            // 巡逻速度
        //public float AttackRange = 1f;
        public float AttackCooldown = 1.5f;
        public float HurtStunTime = 0.3f;       // 受击僵直

        [Header("AI 参数")]
        public float IdleDuration = 2f;
        public float PatrolDuration = 5f;
        public float FadeDuration = 1f;         // 死亡渐隐

        [Header("掉落与吸取")]
        public int CapDrop;                     // 瓶盖：精英 1 / BOSS 2 / 其余 0
        public bool CanAbsorb;                  // 是否可吸取
        public string AbsorbLiquidId;           // 吸取产出的液体，指向 LiquidConfig 的 Id

        [Header("抗性")]
        public bool ImmuneKnockback;            // BOSS 免疫击退
        public bool ImmuneStun;                 // BOSS 免疫眩晕
        public float DebuffDurationScale = 1f;  // BOSS 0.5：冰冻 / 酒雾持续减半
        public float DebuffChanceScale = 1f;    // BOSS 0.5：概率减半

        [Header("装配")]
        public string PrefabLocation;           // YooAsset 地址，弱引用；不在此处直接引用 prefab
    }
}
