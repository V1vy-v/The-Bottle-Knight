using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PropConfigs", menuName = "Configs/PropConfigs")]
public class PropConfigs : ScriptableObject
{
    public List<PropConfig> allProps = new List<PropConfig>();
}