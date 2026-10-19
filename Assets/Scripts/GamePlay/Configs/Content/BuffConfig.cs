using System.Collections.Generic;
using GamePlay.Configs;
using UnityEngine;

[CreateAssetMenu(fileName = "BuffConfig", menuName = "Configs/BuffConfig")]
public class BuffConfig : ConfigBase
{
    public float duration;            

    [Tooltip("0 表示不 tick（纯属性型）；>0 表示每隔这么多秒 tick 一次")]
    public float tickInterval = 0f;

    public BuffEffect[] effects;
}
