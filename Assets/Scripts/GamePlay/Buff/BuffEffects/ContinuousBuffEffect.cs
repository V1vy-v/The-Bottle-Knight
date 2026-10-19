using UnityEngine;
using GamePlay.Player;
using GamePlay.Enemy;

/// <summary>
/// 通用型 Buff：支持持续扣血/回血，同时支持持续期间的属性加成（如减速、加攻）。
/// </summary>
[CreateAssetMenu(fileName = "ContinuousBuffEffect", menuName = "Configs/BuffEffect/ContinuousBuffEffect")]
public class ContinuousBuffEffect : BuffEffect
{
    [Header("=== 周期触发 (每次 Tick 生效) ===")]
    [Header("每次 Tick 造成的伤害 (填正数，例如 5)")]
    public float damagePerTick = 0f;

    [Header("每次 Tick 恢复的血量 (填正数，例如 10)")]
    public float healPerTick = 0f;


    [Header("=== 常驻属性加成 (加 Buff 时生效，结束时恢复) ===")]
    [Header("移动速度 (填正数加速，填负数减速)")]
    public float moveSpeed = 0f;

    [Header("最大生命值 (填正数增加上限，填负数减少上限)")]
    public float maxHp = 0f;

    [Header("攻击力 / 伤害")]
    public float attack = 0f;

    [Header("攻击范围")]
    public float attackRange = 0f;

    [Header("攻击冷却/间隔 (填负数就是加攻速)")]
    public float attackCooldown = 0f;


    //应用/移除 属性 (只触发一次)
    public override void OnApply(EnemyRealState state) => ApplyStat(state, 1f);
    public override void OnRemove(EnemyRealState state) => ApplyStat(state, -1f);
    public override void OnApply(PlayerStats state) => ApplyStat(state, 1f);
    public override void OnRemove(PlayerStats state) => ApplyStat(state, -1f);


    //周期 扣血/回血
    public override void OnTick(EnemyRealState state, float tickDt)
    {
        if (damagePerTick > 0)
        {
            state.CurrentHP -= damagePerTick;
            state.ClampHP();
        }
        if (healPerTick > 0)
        {
            state.CurrentHP += healPerTick;
            state.ClampHP();
        }
    }

    public override void OnTick(PlayerStats state, float tickDt)
    {
        if (state.IsDead) return;

        if (damagePerTick > 0)
            state.ApplyDamage(Mathf.RoundToInt(damagePerTick));

        if (healPerTick > 0)
            state.Healing(Mathf.RoundToInt(healPerTick));
    }


    //统一处理属性加减逻辑
    private void ApplyStat(EnemyRealState state, float sign)
    {
        state.bonusMoveSpeed += moveSpeed * sign;
        state.bonusAtk += attack * sign;
        state.bonusHP += maxHp * sign;
        state.bonusAttackRange += attackRange * sign;
        state.bonusAttackCooldown += attackCooldown * sign;

        if (maxHp != 0)
        {
            state.CurrentHP += maxHp * sign;
            state.ClampHP();
        }
    }

    private void ApplyStat(PlayerStats state, float sign)
    {
        state.MoveSpeed += moveSpeed * sign;

        state.MeleeDamage += attack * sign;
        state.RangedDamage += attack * sign;

        state.MeleeRange += attackRange * sign;
        state.RangedRange += attackRange * sign;

        state.PrimaryAtkInterval += attackCooldown * sign;

        state.MaxHp += maxHp * sign;
        if (maxHp != 0)
        {
            state.Hp += maxHp * sign;
            state.Hp = Mathf.Clamp(state.Hp, 0, state.MaxHp);
        }
    }
}