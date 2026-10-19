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

    [Header("Tooltip 悬浮窗")]
    [SerializeField] private GameObject tooltipPrefab;

    private PropConfig _config;
    private Action<PropConfig> _onSelected;

    private EventTrigger _eventTrigger;
    private GameObject _currentTooltipInstance;

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

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnPointerEnterDelegate(); });
        _eventTrigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { OnPointerExitDelegate(); });
        _eventTrigger.triggers.Add(exitEntry);
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
        if (tooltipPrefab != null && _currentTooltipInstance == null)
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            Transform parent = rootCanvas != null ? rootCanvas.transform : transform.parent;

            _currentTooltipInstance = Instantiate(tooltipPrefab, parent);
            _currentTooltipInstance.transform.SetAsLastSibling(); // 置于最顶层

            Tips tipScript = _currentTooltipInstance.GetComponent<Tips>();
            if (tipScript != null && _config != null)
            {
                tipScript.Init(_config.tips);
            }
            else
            {
                Debug.LogWarning("[PropCell] Tooltip 预制体上没有挂载 Tips 脚本！");
            }

            _currentTooltipInstance.transform.position = (Vector2)Input.mousePosition + new Vector2(15f, -15f);
        }
    }

    private void OnPointerExitDelegate()
    {
        DestroyTooltip();
    }

    private void DestroyTooltip()
    {
        if (_currentTooltipInstance != null)
        {
            Destroy(_currentTooltipInstance);
            _currentTooltipInstance = null;
        }
    }

    private void OnDestroy()
    {
        DestroyTooltip();
    }
}