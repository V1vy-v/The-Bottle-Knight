using Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// ShopPanel 的界面数据，打开界面时传入
    /// </summary>
    [Serializable]
    public class ShopPanelProperties : PanelProperties
    {
        // TODO: 补上该界面需要的数据字段，加上 SerializeField 即可在 UIView 上配置
        [SerializeField] private GameObject propCell;
        [SerializeField] private Transform content;
        [SerializeField] private PropConfigs allProps;
        public GameObject PropCell => propCell;
        public Transform Content => content;
        public PropConfigs AllProps => allProps;
        // 供预制体上的 UIView 序列化使用
        public ShopPanelProperties() { }

        public ShopPanelProperties(PanelPriority priority) : base(priority) { }
    }
}
