using UnityEngine;
using GamePlay.Player;
using GamePlay.Enemy;

/// <summary>
/// 万能属性修饰 Buff：填正数增加，填负数减少，为0不处理。
/// </summary>
[CreateAssetMenu(fileName = "AttributeBuffEffect", menuName = "Configs/BuffEffect/AttributeBuffEffect")]
public class AttributeBuffEffect : BuffEffect
{
    [Header("=== 全局说明 ===")]
    [Tooltip("填正数增加，填负数减少，为0则不改变该属性")]
    public string tips = "正数增加，负数减少，为0不处理";

    [Header("=== 基础属性 ===")]
    [Header("移动速度")]
    public float moveSpeed = 0f;

    [Header("最大生命值")]
    public float maxHp = 0f;

    [Header("攻击力 / 伤害")]
    public float attack = 0f;

    [Header("=== 战斗属性 ===")]
    [Header("攻击范围")]
    public float attackRange = 0f;

    [Header("攻击冷却/间隔 (填负数就是加攻速)")]
    public float attackCooldown = 0f;


    //敌人部分
    public override void OnApply(EnemyRealState state) => ApplyToEnemy(state, 1f);
    public override void OnRemove(EnemyRealState state) => ApplyToEnemy(state, -1f);

    private void ApplyToEnemy(EnemyRealState state, float sign)
    {
        state.bonusMoveSpeed += moveSpeed * sign;
        state.bonusAtk += attack * sign;
        state.bonusHP += maxHp * sign;
        state.bonusAttackRange += attackRange * sign;
        state.bonusAttackCooldown += attackCooldown * sign;

        // 如果修改了最大生命值，同步修改当前血量，并限制在合法范围内
        if (maxHp != 0)
        {
            state.CurrentHP += maxHp * sign;
            state.ClampHP();
        }
    }

    //玩家部分
    public override void OnApply(PlayerStats state) => ApplyToPlayer(state, 1f);
    public override void OnRemove(PlayerStats state) => ApplyToPlayer(state, -1f);

    private void ApplyToPlayer(PlayerStats state, float sign)
    {
        state.MoveSpeed += moveSpeed * sign;

        // 玩家的攻击力分为近战和远程，一加都加
        state.MeleeDamage += attack * sign;
        state.RangedDamage += attack * sign;

        // 玩家的攻击范围也分近战和远程
        state.MeleeRange += attackRange * sign;
        state.RangedRange += attackRange * sign;

        // 玩家的攻击间隔
        state.PrimaryAtkInterval += attackCooldown * sign;

        // 玩家的最大生命值及当前血量
        state.MaxHp += maxHp * sign;
        if (maxHp != 0)
        {
            state.Hp += maxHp * sign;
            state.Hp = Mathf.Clamp(state.Hp, 0, state.MaxHp);
        }
    }
}