using UnityEngine;
using UnityEngine.UI;
using System; // 需要引入 Action

public class PropCell : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Toggle toggle;
    // [SerializeField] private Text nameText; // 如果有名字文本的话

    private PropConfig _config;
    private Action<PropConfig> _onSelected; // 用来通知 ShopPanel

    /// <summary>
    /// 初始化格子
    /// </summary>
    /// <param name="config">道具配置</param>
    /// <param name="onSelected">被选中时的回调</param>
    public void Init(PropConfig config, Action<PropConfig> onSelected)
    {
        _config = config;
        _onSelected = onSelected;

        if (iconImage != null && config.sprite != null)
        {
            iconImage.sprite = config.sprite;
        }

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners(); // 防止重复绑定
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }
    }

    private void OnToggleValueChanged(bool isOn)
    {
        if (isOn && _onSelected != null)
        {
            _onSelected.Invoke(_config);
        }
    }
}