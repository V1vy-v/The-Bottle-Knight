﻿using System.Collections.Generic;
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
}
