using UnityEngine;
using GamePlay.Player;
using GamePlay.Enemy;

/// <summary>
/// Buff 效果基类，不同效果 buff 继承实现。
/// 注意：
/// - 属性型（加攻/加速）：在 OnApply 加、OnRemove 减，不需要 tick
/// - 持续型（DoT/HoT）：在 OnTick 里扣血/回血，tickDt 是一个 tick 代表的时间
/// - 瞬时型（加一次血）：duration <= 0，只走 OnApply
/// </summary>
public abstract class BuffEffect : ScriptableObject
{
    //加 Buff 时调用
    public virtual void OnApply(EnemyRealState state) { }
    public virtual void OnApply(PlayerStats state) { }

    //移除 Buff 时调用
    public virtual void OnRemove(EnemyRealState state) { }
    public virtual void OnRemove(PlayerStats state) { }

    //每次 tick 调用
    public virtual void OnTick(EnemyRealState state, float tickDt) { }
    public virtual void OnTick(PlayerStats state, float tickDt) { }
}