using System.Collections.Generic;
using GamePlay.Configs;
using UnityEngine;

[CreateAssetMenu(fileName = "PropConfig", menuName = "Configs/PropConfig")]
public class PropConfig : ConfigBase
{
    public Sprite sprite;//图标
    public string tips;//武器详细解释
    public int price;//价格
    public bool isHold;//是否拥有

    [Header("购买效果")]
    [Tooltip("买下后解锁远战模组（吸管），Q 才能在近战/远战之间切换")]
    public bool unlockRangedModule;
    [Tooltip("买下后弹一次教学提示，留空不弹")]
    [TextArea(2, 4)] public string tipOnBuy;

    [Header("数值增益（买下后由 PlayerStats.RecalcProps 汇总）")]
    [Tooltip("伤害加成，0.2 = +20%（普攻与技能都吃）")]
    public float DamageBonus;

    [Tooltip("攻速加成，0.15 = +15%（等比例缩短普攻间隔）")]
    public float AttackSpeedBonus;

    [Tooltip("技能冷却缩减，0.15 = -15%")]
    public float SkillCdReduction;

    [Tooltip("免疫火路 / 亡语这类持续伤害（手套）")]
    public bool ImmuneDot;

    [Tooltip("每 N 秒补 1 层护盾，0 = 不提供护盾（保鲜膜 = 10）")]
    public float ShieldInterval;

    [Tooltip("护盾最多叠几层")]
    public int ShieldMax = 1;

    [Tooltip("普攻打小怪时的眩晕概率（0.5 = 50%）")]
    public float StunChanceMinion;

    [Tooltip("普打精英时的眩晕概率（0.25 = 25%）")]
    public float StunChanceElite;

    [Tooltip("眩晕时长（秒）")]
    public float StunDuration = 1.2f;
}
