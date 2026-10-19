using GamePlay.Enemy;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHPUI : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Image bkImg;

    private EnemyController enemyController;

    private void Awake()
    {
        if (image == null) return;

        if (image.sprite == null)
        {
            image.sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f));
        }

        // 居中
        var rt = image.rectTransform;
        var pivot = rt.pivot;
        pivot.x = 0.5f;
        rt.pivot = pivot;

        // Filled 模式配置
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Right;   // 从右端开始填充
        image.fillAmount = 1f;
    }

    private void Start()
    {
        enemyController = GetComponentInParent<EnemyController>();
        if (enemyController != null)
        {
            print("获取到组件");
            enemyController.OnWound += OnHPChanged;
            OnHPChanged(enemyController.state.CurrentHP, enemyController.state.MaxHP);
        }
        else
        {
            print("未获取到组件");
        }

        //gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (enemyController != null)
            enemyController.OnWound -= OnHPChanged;
    }

    private void OnHPChanged(float current, float max)
    {
        if (image == null) return;

        float percent = max > 0 ? (float)current / max : 0f;
        image.fillAmount = Mathf.Clamp01(percent);

        if (bkImg != null)
        {
            bkImg.gameObject.SetActive(current > 0);
        }
    }
}