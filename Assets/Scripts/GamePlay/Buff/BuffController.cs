using GamePlay.Enemy;
using GamePlay.Player;
using System.Collections.Generic;
using UnityEngine;

public class BuffController : MonoBehaviour
{
    private EnemyController enemyController;
    private PlayerController playerController;

    // 缓存（懒加载后填充，不要直接用这两个字段判断）
    private EnemyRealState enemyState;
    private PlayerStats playerState;

    private Dictionary<string, BuffInstance> activeBuffs = new Dictionary<string, BuffInstance>();
    private List<string> removeBuffs = new List<string>();

    // 懒加载属性：解决 Awake 时序问题
    private EnemyRealState EnemyState
    {
        get
        {
            if (enemyState == null && enemyController != null)
                enemyState = enemyController.state;
            return enemyState;
        }
    }

    private PlayerStats PlayerState
    {
        get
        {
            if (playerState == null && playerController != null)
                playerState = playerController.PlayerStats;   // ⚠️ 按实际字段名确认
            return playerState;
        }
    }

    private bool HasTarget => EnemyState != null || PlayerState != null;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        playerController = GetComponent<PlayerController>();

        if (enemyController == null && playerController == null)
        {
            Debug.LogError($"[BuffController] {name} 上既没有 EnemyController 也没有 PlayerController，Buff 不会生效");
        }
    }

    private void Update()
    {
        if (activeBuffs.Count == 0) return;

        removeBuffs.Clear();

        foreach (var kvp in activeBuffs)
        {
            var buff = kvp.Value;

            // ① 先处理 tick：用 tickInterval 作为一次 tick 代表的时间，而不是 Time.deltaTime
            if (buff.config.tickInterval > 0f)
            {
                buff.tickTimer -= Time.deltaTime;
                if (buff.tickTimer <= 0f)
                {
                    buff.tickTimer += buff.config.tickInterval;   // += 避免累积误差
                    if (buff.config.effects != null)
                    {
                        foreach (var effect in buff.config.effects)
                            TickEffect(effect, buff.config.tickInterval);
                    }
                }
            }

            // ② 再判到期：这样到期那一帧仍有机会 tick，不会吞掉最后一次
            if (buff.Update(Time.deltaTime))
                removeBuffs.Add(kvp.Key);
        }

        foreach (var id in removeBuffs)
            RemoveBuff(id);
    }

    private void TickEffect(BuffEffect effect, float tickDt)
    {
        if (effect == null) return;

        var e = EnemyState;
        if (e != null) { effect.OnTick(e, tickDt); return; }

        var p = PlayerState;
        if (p != null) effect.OnTick(p, tickDt);
    }

    /// <summary>
    /// 添加 Buff。
    /// - duration > 0：进入 activeBuffs，按时间 tick / 到期移除
    /// - duration <= 0：瞬时 buff，只调 OnApply，不进字典、不调 OnRemove
    /// 已存在同名 Buff 时：刷新时长 + 重置 tick 计时（不重复 Apply）
    /// </summary>
    public void AddBuff(BuffConfig config)
    {
        if (config == null) return;

        if (!HasTarget)
        {
            Debug.LogError($"[BuffController] {name} 没有可用的 Buff 目标（EnemyState / PlayerStats 均为 null），" +
                           $"请检查 EnemyController.state 或 PlayerController.PlayerStats 是否已初始化");
            return;
        }

        // 瞬时 buff：只 Apply 一次，不进字典
        if (config.duration <= 0f)
        {
            if (config.effects != null)
            {
                foreach (var effect in config.effects)
                    ApplyEffect(effect, true);
            }
            return;
        }

        // 已存在：只刷新时长 + 重置 tick 计时
        if (activeBuffs.TryGetValue(config.Id, out var existing))
        {
            existing.currentDuration = config.duration;
            existing.tickTimer = 0f;   // 与构造函数保持一致
            return;
        }

        var instance = new BuffInstance(config);
        activeBuffs.Add(config.Id, instance);

        if (config.effects != null)
        {
            foreach (var effect in config.effects)
                ApplyEffect(effect, true);
        }
    }

    /// <summary>
    /// 移除 Buff，并触发对应的 OnRemove
    /// </summary>
    public void RemoveBuff(string buffID)
    {
        if (!activeBuffs.TryGetValue(buffID, out var buff)) return;

        if (buff.config.effects != null)
        {
            foreach (var effect in buff.config.effects)
                ApplyEffect(effect, false);
        }

        activeBuffs.Remove(buffID);
    }

    /// <summary>
    /// 统一分发到 BuffEffect 的两个重载。
    /// 优先级：Enemy > Player（同一 GameObject 上一般只会挂其中一个）。
    /// </summary>
    private void ApplyEffect(BuffEffect effect, bool apply)
    {
        if (effect == null) return;

        var e = EnemyState;
        if (e != null)
        {
            if (apply) effect.OnApply(e);
            else effect.OnRemove(e);
            return;
        }

        var p = PlayerState;
        if (p != null)
        {
            if (apply) effect.OnApply(p);
            else effect.OnRemove(p);
        }
    }
}