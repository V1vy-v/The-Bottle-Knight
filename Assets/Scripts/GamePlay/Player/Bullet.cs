using Framework;
using UnityEngine;
using GamePlay.Enemy;
using System.Collections.Generic;

/// <summary>
/// 玩家远程攻击的子弹。走对象门面实例化，所以必须实现 <see cref="IPoolable"/>
/// （否则 InstantiateManager 会抛「未实现 IPoolable，不能进入池」）。
/// 穿透 / 命中 / 超时都不再 Destroy，而是还给对象池。
/// </summary>
public class Bullet : MonoBehaviour, IPoolable
{
    [Header("子弹属性")]
    public float speed = 15f;       // 飞行速度
    public float lifeTime = 3f;     // 存活时间（防止无限飞出屏幕）
    public bool isPierce = false;   // 是否穿透（穿透弹不会击中一个就回收）

    [Header("动画与销毁")]
    public string flyAnimName = "飞行";
    public string hitAnimName = "击中";
    public float hitDestroyDelay = 0.5f; // 击中动画播放多久后回收

    private float _damage;          // 最终伤害
    private Vector2 _direction;     // 飞行方向
    private bool _isInitialized = false;
    private bool _isHit = false;

    /// <summary>剩余存活时间</summary>
    private float _lifeRemain;
    /// <summary>击中后剩余动画时间（<0 表示没在播）</summary>
    private float _hitRemain = -1f;

    private Animator _animator;
    private Collider2D _collider;
    private Rigidbody2D _body;

    // 防止一个怪物有多个碰撞体导致瞬间重复扣血
    private HashSet<EnemyController> _hitEnemies = new HashSet<EnemyController>();

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _collider = GetComponent<Collider2D>();

        // 触发事件要求碰撞双方至少有一个 Rigidbody2D。
        // 敌人是 NavMesh 驱动的、身上没有刚体，子弹自己也必须有，否则永远打不到人。
        _body = GetComponent<Rigidbody2D>();
        if (_body == null) _body = gameObject.AddComponent<Rigidbody2D>();

        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.freezeRotation = true;
        _body.simulated = true;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    /// <summary>初始化子弹</summary>
    public void Init(Vector2 dir, float finalDamage)
    {
        _direction = dir.normalized;
        _damage = finalDamage;
        _isInitialized = true;
        _isHit = false;
        _lifeRemain = lifeTime;
        _hitRemain = -1f;
        _hitEnemies.Clear();

        if (_collider != null) _collider.enabled = true;

        // 让子弹贴图朝向飞行方向
        transform.right = _direction;

        // 用刚体速度飞：transform 位移对 Kinematic 刚体不会及时同步碰撞，容易漏判
        if (_body != null)
        {
            _body.velocity = _direction * speed;
            _body.position = transform.position;
        }

        if (_animator != null) _animator.Play(flyAnimName);
    }

    /// <summary>还池 / 再次借出前由对象池调用</summary>
    public void Reset()
    {
        _isInitialized = false;
        _isHit = false;
        _damage = 0f;
        _direction = Vector2.zero;
        _lifeRemain = 0f;
        _hitRemain = -1f;
        _hitEnemies.Clear();

        if (_body != null) _body.velocity = Vector2.zero;
        if (_collider != null) _collider.enabled = true;
    }

    private void Update()
    {
        // 击中后：等动画播完再还池
        if (_isHit)
        {
            if (_hitRemain > 0f)
            {
                _hitRemain -= Time.deltaTime;
                if (_hitRemain <= 0f) Recycle();
            }
            return;
        }

        if (!_isInitialized) return;

        _lifeRemain -= Time.deltaTime;
        if (_lifeRemain <= 0f)
        {
            Recycle();
            return;
        }

        // 有刚体就靠速度飞（物理会同步碰撞）；万一刚体被去掉，退回 transform 位移
        if (_body != null)
        {
            if (_body.velocity.sqrMagnitude < 0.01f) _body.velocity = _direction * speed;
        }
        else
        {
            transform.Translate(_direction * speed * Time.deltaTime, Space.World);
        }
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!_isInitialized || _isHit) return;

        // 1. 敌人
        var enemy = col.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            if (_hitEnemies.Contains(enemy)) return;

            _hitEnemies.Add(enemy);
            enemy.ApplyDamage(_damage);

            if (!isPierce) TriggerHit();
            return;
        }

        // 2. 墙壁 / 场景阻挡
        if (col.gameObject.layer == LayerMask.NameToLayer("Obstacle") ||
            col.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            TriggerHit();
        }
    }

    /// <summary>刹车、播击中动画、关碰撞、延时还池</summary>
    private void TriggerHit()
    {
        if (_isHit) return;
        _isHit = true;

        if (_animator != null) _animator.Play(hitAnimName);
        if (_body != null) _body.velocity = Vector2.zero;
        if (_collider != null) _collider.enabled = false;

        _hitRemain = hitDestroyDelay;
    }

    /// <summary>还给对象池（不是 Destroy —— 池化对象销毁后就回不到池里了）</summary>
    private void Recycle()
    {
        _isInitialized = false;
        _isHit = false;
        _hitRemain = -1f;

        if (_body != null) _body.velocity = Vector2.zero;

        var manager = Global.Get<InstantiateManager>();
        if (manager != null)
        {
            manager.Release(gameObject);
            return;
        }

        Destroy(gameObject);
    }
}
