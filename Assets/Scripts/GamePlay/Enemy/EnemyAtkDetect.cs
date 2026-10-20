using GamePlay.Enemy;
using GamePlay.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAtkDetect : MonoBehaviour
{
    private float damage;

    private EnemyController _owner;

    private void Awake() => _owner = GetComponentInParent<EnemyController>();

    void Start()
    {
        
    }
    public void SetAtk(float atk)
    {
        damage = atk;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        // 对话 / 商店期间 AI 被冻结，这时不该再结算伤害
        if (_owner != null && _owner.IsAiPaused) return;

        PlayerController controller = collision.GetComponentInParent<PlayerController>();
        if (controller == null)
        {
            Debug.LogWarning($"[EnemyAtkDetect] 碰到的物体 {collision.name} 有 Player 标签，但身上没挂 PlayerController 脚本！", collision);
            return;
        }

        if (controller.PlayerStats == null)
        {
            Debug.LogWarning($"[EnemyAtkDetect] {collision.name} 的 PlayerController 里的 PlayerStats 是空的！", controller);
            return;
        }

        controller.PlayerStats.ApplyDamage((int)damage);
    }
}
