/// <summary>
/// Buff 实例，各自管理其运行状态（剩余时长、tick 计时）
/// </summary>
public class BuffInstance
{
    public BuffConfig config;
    public float currentDuration;
    public float tickTimer;

    public BuffInstance(BuffConfig config)
    {
        this.config = config;
        this.currentDuration = config.duration;

        // 立刻 tick 一次（DoT 常见：中毒当场跳一下）
        // 如果设计是"等一个间隔再 tick"，改成 config.tickInterval
        this.tickTimer = 0f;
    }

    /// <summary>
    /// 剩余时间倒计时，返回 true 表示到期
    /// </summary>
    public bool Update(float deltaTime)
    {
        currentDuration -= deltaTime;
        return currentDuration <= 0f;
    }
}