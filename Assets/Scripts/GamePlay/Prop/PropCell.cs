using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;

public class PropCell : MonoBehaviour
{
    [Header("UI 引用")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Toggle toggle;
    [SerializeField] private TextMeshProUGUI priceText;

    [Header("Tooltip 详情页")]
    [SerializeField] private GameObject tooltipPrefab;

    /// <summary>距鼠标的偏移</summary>
    [SerializeField] private Vector2 _tooltipOffset = new Vector2(15f, -15f);

    private PropConfig _config;
    private Action<PropConfig> _onSelected;

    private EventTrigger _eventTrigger;
    private GameObject _currentTooltipInstance;

    /// <summary>当前正在显示详情的那一格（同时只留一个）</summary>
    private static PropCell _openCell;

    public void Init(PropConfig config, Action<PropConfig> onSelected)
    {
        _config = config;
        _onSelected = onSelected;

        if (iconImage != null && config.sprite != null)
            iconImage.sprite = config.sprite;

        if (priceText != null)
            priceText.text = config.price.ToString();

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        SetupEventTrigger();
    }

    private void SetupEventTrigger()
    {
        _eventTrigger = GetComponent<EventTrigger>();
        if (_eventTrigger == null)
            _eventTrigger = gameObject.AddComponent<EventTrigger>();

        _eventTrigger.triggers.Clear();

        // 悬浮只负责音效
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnPointerEnterDelegate(); });
        _eventTrigger.triggers.Add(enterEntry);

        // 详情页改成点击显示 / 再点一次收起
        EventTrigger.Entry clickEntry = new EventTrigger.Entry();
        clickEntry.eventID = EventTriggerType.PointerClick;
        clickEntry.callback.AddListener((data) => { ToggleTooltip(); });
        _eventTrigger.triggers.Add(clickEntry);
    }

    private void OnToggleValueChanged(bool isOn)
    {
        if (isOn && _onSelected != null)
        {
            _onSelected.Invoke(_config);
        }
    }

    private void OnPointerEnterDelegate()
    {
        // 悬停音：三个变体随机播放，避免重复听腻
        int pick = UnityEngine.Random.Range(0, 3);
        string hoverKey = pick == 0 ? "UI_Hover" : (pick == 1 ? "UI_Hover_1" : "UI_Hover_2");
        Framework.Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx(hoverKey);
    }

    #region 详情页（点击开关）

    private void ToggleTooltip()
    {
        if (_currentTooltipInstance != null)
        {
            DestroyTooltip();
            return;
        }

        ShowTooltip();
    }

    private void ShowTooltip()
    {
        if (tooltipPrefab == null || _config == null) return;

        // 同一时间只留一个详情页
        if (_openCell != null && _openCell != this)
            _openCell.DestroyTooltip();

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        Transform parent = rootCanvas != null ? rootCanvas.transform : transform.parent;

        _currentTooltipInstance = Instantiate(tooltipPrefab, parent);
        _currentTooltipInstance.transform.SetAsLastSibling();   // 置于最顶层

        Tips tipScript = _currentTooltipInstance.GetComponent<Tips>();
        if (tipScript != null)
            tipScript.Init(_config.tips);
        else
            Debug.LogWarning("[PropCell] Tooltip 预制体上没有挂载 Tips 脚本！");

        PlaceTooltip(_currentTooltipInstance, rootCanvas);
        _openCell = this;
    }

    /// <summary>放在鼠标旁边，并夹到屏幕内（详情页比鼠标大得多，不夹会跑出屏幕）</summary>
    private void PlaceTooltip(GameObject tooltip, Canvas canvas)
    {
        var rt = tooltip.transform as RectTransform;

        // CanvasScaler 会缩放，sizeDelta 是 canvas 单位，换算成像素再夹
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 halfPx = rt != null ? rt.sizeDelta * 0.5f * scale : Vector2.zero;

        Vector2 pos = (Vector2)Input.mousePosition + _tooltipOffset;

        pos.x = Mathf.Clamp(pos.x, halfPx.x + 8f, Mathf.Max(halfPx.x + 8f, Screen.width - halfPx.x - 8f));
        pos.y = Mathf.Clamp(pos.y, halfPx.y + 8f, Mathf.Max(halfPx.y + 8f, Screen.height - halfPx.y - 8f));

        tooltip.transform.position = pos;
    }

    private void DestroyTooltip()
    {
        if (_currentTooltipInstance != null)
        {
            Destroy(_currentTooltipInstance);
            _currentTooltipInstance = null;
        }

        if (_openCell == this) _openCell = null;
    }

    #endregion

    private void OnDestroy()
    {
        DestroyTooltip();
    }
}
