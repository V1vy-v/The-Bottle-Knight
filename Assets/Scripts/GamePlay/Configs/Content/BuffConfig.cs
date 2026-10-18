using System.Collections.Generic;
using GamePlay.Configs;
using UnityEngine;

[CreateAssetMenu(fileName = "BuffConfig", menuName = "Configs/BuffConfig")]
public class BuffConfig : ConfigBase
{
    public float duration = 5f;

    // 一个 Buff 可以有多个效果
    public List<BuffEffect> effects = new List<BuffEffect>();
}
