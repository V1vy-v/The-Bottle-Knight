﻿﻿using Framework;
using System;
using System.Collections.Generic;
using GamePlay;
using GamePlay.Player;
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

        /// <summary>商店界面关闭（GameplayShopState 据此回到漫游）</summary>
        public static event Action OnShopClosed;

        /// <summary>
        /// 本店卖什么。由触发商店的 ShopInteractable 写入；
        /// 为 null 或空表示这家店卖全部道具。
        /// </summary>
        public static System.Collections.Generic.List<PropConfig> PendingGoods;

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
            RefreshCaps();
        }

        /// <summary>右下角瓶盖数量</summary>
        private void RefreshCaps()
        {
            if (_txtNum == null) return;

            GameObject owner = Global.Get<GameplayRoot>()?.Player;
            PlayerController pc = owner != null ? owner.GetComponent<PlayerController>() : null;
            _txtNum.text = (pc != null && pc.PlayerStats != null ? pc.PlayerStats.Caps : 0).ToString();
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

            // 本店商品：ShopInteractable 指定了就用它，否则卖全部
            List<PropConfig> allProps = PendingGoods != null && PendingGoods.Count > 0
                ? PendingGoods
                : ShopProps.AllProps.allProps;
            int totalCount = allProps.Count;

            // 页数跟着本店商品走
            _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalCount / MaxDisplayCount));
            if (_currentPageIndex >= _totalPages) _currentPageIndex = _totalPages - 1;

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

            // 扣瓶盖：不够就买不了
            GameObject ownerGo = Global.Get<GameplayRoot>()?.Player;
            PlayerController owner = ownerGo != null ? ownerGo.GetComponent<PlayerController>() : null;
            PlayerStats ownerStats = owner != null ? owner.PlayerStats : null;

            if (ownerStats == null)
            {
                Debug.LogWarning("[ShopPanel] 找不到主角，购买失败");
                return;
            }

            if (ownerStats.Caps < _currentSelectedProp.price)
            {
                Debug.LogWarning($"[ShopPanel] 瓶盖不足：需要 {_currentSelectedProp.price}，现有 {ownerStats.Caps}");
                Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("UI_Error");
                return;
            }

            ownerStats.Caps -= _currentSelectedProp.price;
            RefreshCaps();

            // 标记为已拥有
            _currentSelectedProp.isHold = true;

            Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("UI_Hover");

            // 购买效果：吸管解锁远战模组，并弹一次切换提示
            if (_currentSelectedProp.unlockRangedModule)
            {
                if (ownerGo != null) ownerGo.GetComponent<PlayerCombat>()?.UnlockRangeModule();

                if (!string.IsNullOrEmpty(_currentSelectedProp.tipOnBuy))
                    TutorialTips.Show(_currentSelectedProp.tipOnBuy);
            }


            // 兑换成功后，可以选择刷新界面
            // RefreshShopItems(); 
        }

        private void OnCloseBtn()
        {
            // TODO: 关闭界面逻辑
            Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("UI_close");
            OnShopClosed?.Invoke();
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