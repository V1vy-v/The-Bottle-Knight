using System;
using UnityEngine;

namespace Framework
{
    /// <summary>
    /// UI界面控制器基类，用于实现单个UI界面的所有逻辑
    /// </summary>
    public class UIController<T> : MonoBehaviour, IUIController where T : IUIProperties
    {
        #region 控制器属性

        [Header("UI过渡动画")] 
        [SerializeField] [Tooltip("显示动画")] private AnimComponent animIn;
        [SerializeField] [Tooltip("隐藏动画")] private AnimComponent animOut;
        
        [Header("UI Attributes")]
        [SerializeField] private string uiControllerID;
        [SerializeField] private bool isVisible;
        [SerializeField] [Tooltip("UI界面额外属性")] private T properties;

        // 供子类使用，以完成特殊操作
        public event Action<IUIController> UIDestroyed;
        public event Action<IUIController> InTransitionFinished;
        public event Action<IUIController> OutTransitionFinished;
        public event Action<IUIController> CloseRequested;
        
        #endregion

        #region 暴露属性
        
        public AnimComponent AnimIn { get => animIn; set => animIn = value; }
        public AnimComponent AnimOut { get => animOut; set => animOut = value; }
        public string UIControllerID { get => uiControllerID; set => uiControllerID = value; }
        public bool IsVisible { get => isVisible; set => isVisible = value; }
        public T Properties { get => properties; set => properties = value; }
        
        #endregion

        #region 控制器方法
        
        private void Awake()
        {
            AddListener();
            Init();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        protected virtual void Init()
        {
            
        }

        private void OnDestroy()
        {
            UIDestroyed?.Invoke(this);
            RemoveListener();
        }
        
        /// <summary>
        /// 显示界面
        /// </summary>
        public void Show(IUIProperties props = null)
        {
            if (props != null)
            {
                if (props is T uiProperties)
                {
                    SetProperties(uiProperties);
                }
                else
                {
                    Debug.LogError($"{GetType()} wrong property type {props.GetType().Name}");
                    return;
                }
            }
            
            HierarchyFixOnShow();

            if (!gameObject.activeSelf)
            {
                DoAnimation(animIn, OnTransitionInFinished, true);
            }
            else
            {
                InTransitionFinished?.Invoke(this);
            }

            OnShow();
            UpdateView();

            // <临时诊断> 界面比画布还大就是配错了，把尺寸 / sizeDelta / 父链打出来定位
            LogOversizeDiagnostics();
        }

        /// <summary>
        /// 临时诊断：UI 实际尺寸超过画布时打印尺寸、sizeDelta 和父节点链。
        /// 用来定位「面板比屏幕还大 / 超出屏幕」。确认问题后可以删掉。
        /// </summary>
        private void LogOversizeDiagnostics()
        {
            var self = transform as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (self == null || canvas == null) return;

            float canvasWidth = canvas.pixelRect.width;
            float canvasHeight = canvas.pixelRect.height;
            if (self.rect.width <= canvasWidth * 1.05f && self.rect.height <= canvasHeight * 1.05f) return;

            var chain = new System.Text.StringBuilder();
            Transform p = self.parent;
            int depth = 0;
            while (p != null && depth < 6)
            {
                var prt = p as RectTransform;
                if (prt != null)
                    chain.Append($"[{prt.name} {prt.rect.width:F0}x{prt.rect.height:F0} sd={prt.sizeDelta.x:F0},{prt.sizeDelta.y:F0} " +
                                 $"a=({prt.anchorMin.x:F2},{prt.anchorMin.y:F2})~({prt.anchorMax.x:F2},{prt.anchorMax.y:F2})] ");
                else
                    chain.Append($"[{p.name} 非RectTransform] ");

                p = p.parent;
                depth++;
            }

            Debug.LogWarning($"[UI尺寸异常] {name}({GetType().Name}) 画布={canvasWidth:F0}x{canvasHeight:F0} " +
                             $"面板={self.rect.width:F0}x{self.rect.height:F0} sd={self.sizeDelta.x:F0},{self.sizeDelta.y:F0} " +
                             $"a=({self.anchorMin.x:F2},{self.anchorMin.y:F2})~({self.anchorMax.x:F2},{self.anchorMax.y:F2})\n" +
                             $"父链: {chain}");
        }

        /// <summary>
        /// UI显示时逻辑
        /// </summary>
        protected virtual void OnShow()
        {
            
        }

        /// <summary>
        /// 更新UI显示数据，可以是获取新的数据来源，或是使用变更的UIProperties
        /// </summary>
        protected virtual void UpdateView()
        {
            
        }

        /// <summary>
        /// 隐藏界面
        /// </summary>
        /// <param name="animate">是否播放动画</param>
        public void Hide(bool animate = true)
        {
            DoAnimation(animate ? animOut : null, OnTransitionOutFinished, false);
            OnHide();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// 播放UI界面过渡动画，当动画播放完毕后调用回调函数
        /// </summary>
        /// <param name="anim">过渡动画组件</param>
        /// <param name="callback">回调函数</param>
        /// <param name="visible">设置UI界面可见性</param>
        private void DoAnimation(AnimComponent anim, Action callback, bool visible)
        {
            if (!anim)
            {
                // 没有过渡动画处理
                gameObject.SetActive(visible);
                callback?.Invoke();
            }
            else
            {
                // 当前UI界面是否显示
                if (visible && !gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }
                
                anim.Animate(transform, callback);
            }
        }

        private void OnTransitionInFinished()
        {
            isVisible = true;
            InTransitionFinished?.Invoke(this);
        }

        private void OnTransitionOutFinished()
        {
            isVisible = false;
            gameObject.SetActive(false);    // 关闭动画结束，禁用对象
            OutTransitionFinished?.Invoke(this);
        }

        /// <summary>
        /// 添加监听事件，由子类添加自定义行为
        /// </summary>
        protected virtual void AddListener()
        {
            
        }

        /// <summary>
        /// 注销监听事件，由子类添加自定义行为
        /// </summary>
        protected virtual void RemoveListener()
        {
            UIDestroyed = null;
            InTransitionFinished = null;
            OutTransitionFinished = null;
            CloseRequested = null;
        }
        
        /// <summary>
        /// 界面隐藏时触发处理逻辑，由子类添加自定义行为
        /// </summary>
        protected virtual void OnHide()
        {
            
        }

        protected virtual void SetProperties(T props)
        {
            properties = props;
        }

        /// <summary>
        /// 在显示的时候处理一些层级，由子类添加自定义行为
        /// </summary>
        protected virtual void HierarchyFixOnShow()
        {
            
        }

        /// <summary>
        /// 关闭UI界面请求
        /// </summary>
        /// <param name="controller">UI界面控制器</param>
        protected void CloseUIRequested(IUIController controller)
        {
            CloseRequested?.Invoke(controller);
        }
        
        #endregion
    }
}