using UnityEngine;
using GamePlay.Enemy;
using System.Collections.Generic;

public class Bullet : MonoBehaviour
{
    [Header("子弹属性")]
    public float speed = 15f;       // 飞行速度
    public float lifeTime = 3f;     // 存活时间（防止无限飞出屏幕导致内存泄漏）
    public bool isPierce = false;   // 是否穿透（穿透弹不会击中一个就销毁）

    private float _damage;          // 最终伤害（由玩家攻击力计算好传进来）
    private Vector2 _direction;     // 飞行方向
    private bool _isInitialized = false; // 是否已初始化

    // 防止一个怪物有多个碰撞体导致瞬间重复扣血
    private HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();

    /// <summary>
    /// 初始化子弹
    /// </summary>
    /// <param name="dir">飞行方向</param>
    /// <param name="finalDamage">最终伤害（由玩家攻击力+液体加成计算得出）</param>
    public void Init(Vector2 dir, float finalDamage)
    {
        _direction = dir.normalized;
        _damage = finalDamage;
        _isInitialized = true;

        // 让子弹贴图朝向飞行方向（如果你的子弹是箭头/长条形，这句很重要）
        transform.right = _direction;

        // 设定生命周期，时间到自动销毁
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (!_isInitialized) return;

        // 沿指定方向移动
        transform.Translate(_direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!_isInitialized) return;

        // 1. 尝试获取敌人
        var enemy = col.GetComponentInParent<EnemyController>();

        if (enemy != null)
        {
            // 如果这个怪物已经被打过，直接跳过（防止多碰撞体重复扣血）
            if (_hitEnemies.Contains(enemy)) return;

            // 记录并扣血
            _hitEnemies.Add(enemy);
            enemy.ApplyDamage(_damage);

            // 如果不是穿透弹，打中一个就销毁
            if (!isPierce)
            {
                Destroy(gameObject);
            }
            return;
        }

        // 2. 碰到墙壁/障碍物检测（假设墙壁层级叫 "Wall" 或 "Ground"）
        // 你可以根据自己的项目修改这里的层级判断
        if (col.gameObject.layer == LayerMask.NameToLayer("Ground") ||
            col.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            Destroy(gameObject); // 碰到墙直接销毁
        }
    }
}