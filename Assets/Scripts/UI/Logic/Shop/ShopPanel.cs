using Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// ShopPanel 的界面逻辑
    /// </summary>
    public partial class ShopPanel
    {
        private const int MaxDisplayCount = 8; // 一页最多显示 8 个
        private int _currentPageIndex = 0;     // 当前页码（从 0 开始）
        private int _totalPages = 0;           // 总页数

        private ShopPanelProperties ShopProps => Properties as ShopPanelProperties;

        // 记录当前选中的道具配置
        private PropConfig _currentSelectedProp;

        #region 生命周期

        protected override void Init()
        {
            // 初始化按钮事件
            _buyBtn.onClick.AddListener(OnBuyBtn);
            _closeBtn.onClick.AddListener(OnCloseBtn);
            _lastBtn.onClick.AddListener(OnLastBtn);
            _nextBtn.onClick.AddListener(OnNextBtn);

            // 初始化分页数据
            _currentPageIndex = 0;
            if (ShopProps != null && ShopProps.AllProps != null)
            {
                int totalCount = ShopProps.AllProps.allProps.Count;
                _totalPages = Mathf.CeilToInt((float)totalCount / MaxDisplayCount);
            }
        }

        protected override void AddListener()
        {
            // TODO: 订阅界面以外的事件
        }

        protected override void RemoveListener()
        {
            _buyBtn.onClick.RemoveListener(OnBuyBtn);
            _closeBtn.onClick.RemoveListener(OnCloseBtn);
            _lastBtn.onClick.RemoveListener(OnLastBtn);
            _nextBtn.onClick.RemoveListener(OnNextBtn);
            base.RemoveListener();
        }

        protected override void UpdateView()
        {
            // 每次打开界面刷新显示
            RefreshShopItems();
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 刷新当前页的道具列表
        /// </summary>
        private void RefreshShopItems()
        {
            if (ShopProps == null || ShopProps.AllProps == null || ShopProps.Content == null || ShopProps.PropCell == null)
            {
                Debug.LogWarning("[ShopPanel] 配置缺失或 Properties 转换失败，无法生成商品列表");
                return;
            }

            // 刷新列表时，清空选中的记录
            _currentSelectedProp = null;

            Transform contentRoot = ShopProps.Content;

            // 清理旧的 Cell
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var child = contentRoot.GetChild(i).gameObject;
                child.transform.SetParent(null);
                Destroy(child);
            }

            // 获取 ToggleGroup
            ToggleGroup toggleGroup = contentRoot.GetComponent<ToggleGroup>();
            if (toggleGroup == null)
            {
                Debug.LogWarning("[ShopPanel] 找不到 ToggleGroup，无法实现单选效果！请检查 Content 节点。");
            }

            List<PropConfig> allProps = ShopProps.AllProps.allProps;
            int totalCount = allProps.Count;

            int startIndex = _currentPageIndex * MaxDisplayCount;
            int endIndex = Mathf.Min(startIndex + MaxDisplayCount, totalCount);

            // 生成新的 Cell
            for (int i = startIndex; i < endIndex; i++)
            {
                PropConfig config = allProps[i];
                GameObject cellGo = Instantiate(ShopProps.PropCell, contentRoot);

                Toggle cellToggle = cellGo.GetComponent<Toggle>() ?? cellGo.GetComponentInChildren<Toggle>();
                if (cellToggle != null && toggleGroup != null)
                {
                    cellToggle.group = toggleGroup;
                }

                PropCell cellScript = cellGo.GetComponent<PropCell>();
                if (cellScript != null)
                {
                    // 传入 config，并传入一个回调，当被选中时通知 ShopPanel
                    cellScript.Init(config, OnPropSelected);
                }
                else
                {
                    Debug.LogError("[ShopPanel] 生成的 PropCell 预制体上没有挂载 PropCell 脚本！");
                }
            }
        }

        /// <summary>
        /// 当格子被选中时的回调
        /// </summary>
        private void OnPropSelected(PropConfig config)
        {
            _currentSelectedProp = config;
        }

        private bool HasLastPage() => _currentPageIndex > 0;
        private bool HasNextPage() => _currentPageIndex < _totalPages - 1;

        #endregion

        #region UI回调

        private void OnBuyBtn()
        {
            // 购买逻辑
            if (_currentSelectedProp == null)
            {
                Debug.LogWarning("[ShopPanel] 请先选中一个道具！");
                return;
            }

            if (_currentSelectedProp.isHold)
            {
                Debug.LogWarning($"[ShopPanel] 道具 {_currentSelectedProp.name} 已经拥有了！");
                return;
            }

            // TODO: 对接玩家金币系统 (示例代码)
            // if (PlayerData.Instance.Gold < _currentSelectedProp.price) {
            //     Debug.Log("金币不足！");
            //     return;
            // }
            // PlayerData.Instance.Gold -= _currentSelectedProp.price;

            // 标记为已拥有
            _currentSelectedProp.isHold = true;


            // 兑换成功后，可以选择刷新界面
            // RefreshShopItems(); 
        }

        private void OnCloseBtn()
        {
            // TODO: 关闭界面逻辑
            Global.HideUI("ShopPanel");
        }

        private void OnLastBtn()
        {
            if (HasLastPage())
            {
                _currentPageIndex--;
                RefreshShopItems();
            }
        }

        private void OnNextBtn()
        {
            if (HasNextPage())
            {
                _currentPageIndex++;
                RefreshShopItems();
            }
        }

        #endregion
    }
}