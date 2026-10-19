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
}
