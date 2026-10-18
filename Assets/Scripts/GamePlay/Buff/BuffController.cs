using GamePlay.Enemy;
using System.Collections.Generic;
using UnityEngine;

public class BuffController : MonoBehaviour
{
    private EnemyController controller;
    private EnemyRealState state;

    private Dictionary<string, BuffInstance> activeBuffs = new Dictionary<string, BuffInstance>();
    private List<string> removeBuffs = new List<string>();

    private void Awake()
    {
        controller = GetComponent<EnemyController>();
        if (controller != null)
            state = controller.state;
    }

    private void Update()
    {
        if (activeBuffs.Count == 0) return;

        removeBuffs.Clear();

        foreach (var kvp in activeBuffs)
        {
            if (kvp.Value.Update(Time.deltaTime))
                removeBuffs.Add(kvp.Key);
        }

        foreach (var id in removeBuffs)
            RemoveBuff(id);
    }

    public void AddBuff(BuffConfig config)
    {
        if (config == null || state == null) return;

        if (activeBuffs.TryGetValue(config.Id, out var existing))
        {
            existing.currentDuration = config.duration;
            return;
        }

        var instance = new BuffInstance(config);
        activeBuffs.Add(config.Id, instance);

        // 逐个效果应用
        foreach (var effect in config.effects)
            effect.OnApply(state);
    }

    public void RemoveBuff(string buffID)
    {
        if (!activeBuffs.TryGetValue(buffID, out var buff)) return;

        // 逐个效果移除
        foreach (var effect in buff.config.effects)
            effect.OnRemove(state);

        activeBuffs.Remove(buffID);
    }
}