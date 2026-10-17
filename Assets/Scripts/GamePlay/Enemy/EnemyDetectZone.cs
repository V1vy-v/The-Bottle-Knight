using GamePlay.Enemy;
using UnityEngine;
/// <summary>
/// 碰撞器功能枚举
/// </summary>
public enum ZoneType { Detect, Attack }

public class EnemyDetectZone : MonoBehaviour
{
    public ZoneType zoneType;

    private EnemyController _owner;

    private void Awake()
    {
        _owner = GetComponentInParent<EnemyController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        if (!other.CompareTag("Player")) return;
        Debug.Log($"[{zoneType}] 检测到玩家: {other.name}, 位置: {other.transform.position}");
        switch (zoneType)
        {
            case ZoneType.Detect:
                _owner.playerInDetectRange = true;
                _owner.SetPlayerTarget(other.transform);
                break;
            case ZoneType.Attack:
                _owner.playerInAttackRange = true;
                break;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        switch (zoneType)
        {
            case ZoneType.Detect:
                _owner.playerInDetectRange = false;
                _owner.MissPlayer();
                break;
            case ZoneType.Attack:
                _owner.playerInAttackRange = false;
                break;
        }
    }
}