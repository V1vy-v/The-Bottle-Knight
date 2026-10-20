using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections;

public class DiaLogController : MonoBehaviour
{
    public event Action OnDialogueEnd;

    /// <summary>单例</summary>
    public static DiaLogController Instance;

    [Header("角色数据库")]
    /// <summary>角色 SO 资产，Inspector 里拖</summary>
    public CharacterDatabase characterDatabase;

    [Header("打字机设置")]
    [Tooltip("每个字之间的间隔秒数，越小越快")]
    [SerializeField] private float typeInterval = 0.15f;

    private bool _isTyping;              // 当前是否正在打字
    private string _fullLine;            // 当前这一句的完整内容
    private Coroutine _typingCoroutine;  // 打字协程的引用，方便打断

    private Image spriteLeft;    // 角色A立绘
    private Image spriteRight;   // 角色B立绘
    private TMP_Text nameText;   // 角色名字 text
    private TMP_Text dialogText; // 文本内容 text
    private Button next;         // 下一步按钮
    private GameObject optionButton; // 选项按钮
    private Transform buttonGroup;   // 选项按钮父容器

    /// <summary>当前对话索引</summary>
    private int dialogIndex;

    /// <summary>对话文本按行分割</summary>
    private string[] dialogRows;

    /// <summary>所有对话行的最大下标 + 1。越过它就当对话结束 —— end 行的下标写错时不至于卡死</summary>
    private int _maxRowIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (characterDatabase == null)
            Debug.LogError("[DiaLogController] 没有配置 CharacterDatabase！");
    }

    private void OnDisable()
    {
        // 停止协程
        FinishTyping();
    }

    public void SetupUI(
        Image left, Image right,
        TMP_Text name, TMP_Text dialog,
        Button nextBtn,
        GameObject optionBtn = null, Transform btnGroup = null)
    {
        spriteLeft = left;
        spriteRight = right;
        nameText = name;
        dialogText = dialog;
        next = nextBtn;
        optionButton = optionBtn;
        buttonGroup = btnGroup;
    }

    #region 外部入口：传入文本

    /// <summary>用 TextAsset 设置对话内容</summary>
    public void SetDialogContent(TextAsset textAsset)
    {
        if (textAsset == null)
        {
            Debug.LogWarning("[DiaLogController] 传入的 TextAsset 为空");
            return;
        }
        SetDialogContent(textAsset.text);
    }

    /// <summary>用字符串设置对话内容</summary>
    public void SetDialogContent(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            Debug.LogWarning("[DiaLogController] 传入的对话文本为空");
            return;
        }

        FinishTyping();
        dialogIndex = 0;
        ClearOptionButtons();

        dialogRows = content.Replace("\r", "").Split('\n');
        _maxRowIndex = 0;
        for (int i = 0; i < dialogRows.Length; i++)
        {
            string[] cells = dialogRows[i].Split(',');
            if (cells.Length < 2) continue;
            if (cells[0] != "#" && cells[0] != "&") continue;
            if (int.TryParse(cells[1], out int idx) && idx + 1 > _maxRowIndex) _maxRowIndex = idx + 1;
        }

        ShowDiaLogRow();
    }

    #endregion

    #region 显示逻辑

    public void UpdateText(string _name, string _text)
    {
        if (nameText != null) nameText.text = _name;

        TypeText(_text);
    }

    public void UpdateImage(string _name, string _position)
    {
        if (spriteLeft == null || spriteRight == null) return;

        spriteLeft.gameObject.SetActive(false);
        spriteRight.gameObject.SetActive(false);

        if (characterDatabase == null) return;
        var icon = characterDatabase.GetIcon(_name);
        if (icon == null) return;

        if (_position == "左")
        {
            spriteLeft.sprite = icon;
            spriteLeft.gameObject.SetActive(true);
        }
        else if (_position == "右")
        {
            spriteRight.sprite = icon;
            spriteRight.gameObject.SetActive(true);
        }
    }

    public void ShowDiaLogRow()
    {
        if (dialogRows == null || dialogRows.Length == 0) return;

        // 兜底：已经走过所有对话行 → 直接结束。
        // 原来只认 end 行的下标，end 行写错（比如 3 行对话写成 end,2）就会永远停在最后一句。
        if (dialogIndex >= _maxRowIndex)
        {
            OnDialogueEnd?.Invoke();
            return;
        }

        for (int i = 0; i < dialogRows.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(dialogRows[i])) continue;

            string[] cells = dialogRows[i].Split(',');
            if (cells.Length < 2) continue;

            if (cells[0] == "#" && int.Parse(cells[1]) == dialogIndex)
            {
                UpdateText(cells[2], cells[4]);
                UpdateImage(cells[2], cells[3]);
                dialogIndex = int.Parse(cells[5]);
                if (next != null) next.gameObject.SetActive(true);
                break;
            }
            else if (cells[0] == "&" && int.Parse(cells[1]) == dialogIndex)
            {
                if (next != null) next.gameObject.SetActive(false);
                GenerateOption(i);
            }
            else if (cells[0] == "end" && int.Parse(cells[1]) == dialogIndex)
            {
                OnDialogueEnd?.Invoke();
                print("对话结束");
                break;
            }
        }
    }

    public void OnClickNext()
    {
        // 直接显示全句
        if (_isTyping)
        {
            FinishTyping();
            return;
        }

        ShowDiaLogRow();
    }

    #endregion

    #region 选项

    public void GenerateOption(int _index)
    {
        if (optionButton == null || buttonGroup == null)
        {
            Debug.LogError("[DiaLogController] optionButton / buttonGroup 未注入！");
            return;
        }

        string[] cells = dialogRows[_index].Split(',');

        if (cells[0] == "&")
        {
            GameObject button = Instantiate(optionButton, buttonGroup);
            button.GetComponentInChildren<TMP_Text>().text = cells[4];
            button.GetComponent<Button>().onClick.AddListener(delegate
            {
                OnOptionClick(int.Parse(cells[5]));
            });

            GenerateOption(_index + 1);
        }
    }

    public void OnOptionClick(int _id)
    {
        FinishTyping();
        dialogIndex = _id;
        ShowDiaLogRow();
        ClearOptionButtons();
    }

    private void ClearOptionButtons()
    {
        if (buttonGroup == null) return;
        for (int i = buttonGroup.childCount - 1; i >= 0; i--)
        {
            Destroy(buttonGroup.GetChild(i).gameObject);
        }
    }

    #endregion

    #region 打字机

    /// <summary>用打字机效果显示一段文本</summary>
    private void TypeText(string content)
    {
        if (dialogText == null) return;

        // 停掉上一次打字
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        _fullLine = content;
        _isTyping = true;
        _typingCoroutine = StartCoroutine(TypeRoutine(content));
    }

    /// <summary>打字机协程：逐字追加</summary>
    private IEnumerator TypeRoutine(string content)
    {
        if (dialogText == null) yield break;

        dialogText.text = "";

        for (int i = 0; i < content.Length; i++)
        {
            dialogText.text += content[i];
            yield return new WaitForSeconds(typeInterval);
        }

        _isTyping = false;
        _typingCoroutine = null;
    }

    /// <summary>立刻结束打字，显示全句</summary>
    private void FinishTyping()
    {
        if (_typingCoroutine == null && !_isTyping) return;

        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        if (dialogText != null && !string.IsNullOrEmpty(_fullLine))
            dialogText.text = _fullLine;

        _isTyping = false;
    }

    #endregion
}