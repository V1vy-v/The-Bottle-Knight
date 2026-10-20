using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Tips : MonoBehaviour
{
    [SerializeField] private TMP_Text txt;

    [Tooltip("详情页右上角的关闭按钮")]
    [SerializeField] private Button closeBtn;

    public void Init(string info)
    {
        txt.text = info;

        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(Close);
        }
        else
        {
            Debug.LogWarning("[Tips] 预制体上没有绑定关闭按钮");
        }
    }

    /// <summary>
    /// 关闭按钮：播一下 UI 关闭音后销毁自己。
    /// PropCell 持有的引用会变成 Unity 的「假 null」，下次点格子自然重新打开。
    /// </summary>
    public void Close()
    {
        Framework.Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("UI_close");
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (closeBtn != null) closeBtn.onClick.RemoveListener(Close);
    }
}
