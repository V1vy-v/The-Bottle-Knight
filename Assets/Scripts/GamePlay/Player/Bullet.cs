using UnityEngine;
using GamePlay.Enemy;
using System.Collections.Generic;

public class Bullet : MonoBehaviour
{
    [Header("子弹属性")]
    public float speed = 15f;       // 飞行速度
    public float lifeTime = 3f;     // 存活时间（防止无限飞出屏幕导致内存泄漏）
    public bool isPierce = false;   // 是否穿透（穿透弹不会击中一个就销毁）

    [Header("动画与销毁")]
    public string flyAnimName = "飞行";
    public string hitAnimName = "击中";
    public float hitDestroyDelay = 0.5f; // 击中动画播放多久后销毁（根据你的动画时长调整）

    private float _damage;          // 最终伤害
    private Vector2 _direction;     // 飞行方向
    private bool _isInitialized = false; // 是否已初始化
    private bool _isHit = false;    // 是否已经击中（防止重复触发）

    private Animator _animator;
    private Collider2D _collider;

    // 防止一个怪物有多个碰撞体导致瞬间重复扣血
    private HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();

    private void Awake()
    {
        // 获取组件
        _animator = GetComponentInChildren<Animator>();
        _collider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// 初始化子弹
    /// </summary>
    /// <param name="dir">飞行方向</param>
    /// <param name="finalDamage">最终伤害</param>
    public void Init(Vector2 dir, float finalDamage)
    {
        _direction = dir.normalized;
        _damage = finalDamage;
        _isInitialized = true;

        // 让子弹贴图朝向飞行方向
        transform.right = _direction;

        // 播放飞行动画（如果状态机默认就是飞行，这句可以省略，但加上更保险）
        if (_animator != null)
        {
            _animator.Play(flyAnimName);
        }

        // 设定生命周期，时间到自动销毁
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        // 如果没初始化，或者已经击中了，就不移动了（刹车）
        if (!_isInitialized || _isHit) return;

        // 沿指定方向移动
        transform.Translate(_direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!_isInitialized || _isHit) return;

        // 1. 尝试获取敌人
        var enemy = col.GetComponentInParent<EnemyController>();

        if (enemy != null)
        {
            // 如果这个怪物已经被打过，直接跳过（防止多碰撞体重复扣血）
            if (_hitEnemies.Contains(enemy)) return;

            // 记录并扣血
            _hitEnemies.Add(enemy);
            enemy.ApplyDamage(_damage);

            // 如果不是穿透弹，打中一个就触发击中逻辑
            if (!isPierce)
            {
                TriggerHit();
            }
            return;
        }

        // 2. 碰到墙壁/障碍物检测
        if (col.gameObject.layer == LayerMask.NameToLayer("Ground") ||
            col.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            // 碰到墙触发击中逻辑
            TriggerHit();
        }
    }

    /// <summary>
    /// 触发击中逻辑：刹车、播动画、禁碰撞、延迟销毁
    /// </summary>
    private void TriggerHit()
    {
        if (_isHit) return;
        _isHit = true;

        // 1. 播放击中动画
        if (_animator != null)
        {
            _animator.Play(hitAnimName);
        }

        // 2. 关闭碰撞体，防止击中后还卡在别人身上或者挡住后续子弹
        if (_collider != null)
        {
            _collider.enabled = false;
        }

        // 3. 延迟销毁（等待动画播完）
        Destroy(gameObject, hitDestroyDelay);
    }
}