using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 怪物移动组件，封装 NavMeshAgent
/// </summary>
public class EnemyMoveController : MonoBehaviour
{
    private NavMeshAgent _agent;
    private SpriteRenderer _sprite;

    /// <summary>没有烘焙导航网格时的直线目标（超市区域常常没烘，退化成直线追击）</summary>
    private Vector2? _directTarget;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _sprite = GetComponentInChildren<SpriteRenderer>();

        if (_agent != null)
        {
            _agent.updateRotation = false;
            _agent.updateUpAxis = false;
        }
    }

    private void Update()
    {
        // 根据速度方向翻转
        if (_agent != null && _sprite != null)
        {
            float vx = _agent.velocity.x;
            if (Mathf.Abs(vx) > 0.01f)   
            {
                _sprite.flipX = vx < 0;   
            }
        }

        // 导航网格没覆盖到这里：直线走过去，否则敌人会杵在原地永远够不到玩家
        if (_directTarget.HasValue)
        {
            Vector2 pos = transform.position;
            Vector2 delta = _directTarget.Value - pos;
            float speed = _agent != null ? _agent.speed : 3f;

            if (speed <= 0.01f || delta.sqrMagnitude < 0.04f)
            {
                _directTarget = null;
                return;
            }

            Vector2 step = delta.normalized * speed * Time.deltaTime;

            // 直线兜底也必须撞墙就停：没有导航网格时光改 transform 是不受物理约束的，会直接穿墙
            if (Physics2D.CircleCast(pos, _bodyRadius, step.normalized, step.magnitude, WallMask).collider != null)
            {
                _directTarget = null;
                return;
            }

            transform.position = pos + (step.sqrMagnitude > delta.sqrMagnitude ? delta : step);

            if (_sprite != null && Mathf.Abs(delta.x) > 0.01f)
                _sprite.flipX = delta.x < 0f;
        }
    }

    [Header("撞墙检测（没有 NavMesh 时的直线追击用）")]
    [SerializeField] private LayerMask WallMask = 1 << 10;   // Obstacle

    private float _bodyRadius = 0.8f;

    /// <summary>本体半径，由 EnemyController 在 Awake 时按碰撞体尺寸写进来</summary>
    public float BodyRadius { set => _bodyRadius = Mathf.Max(0.2f, value); }
    public void MoveTo(Vector2 targetPos)
    {
        if (_agent == null || !_agent.isActiveAndEnabled) return;

        // 在导航网格上就正常寻路；不在（没烘 / 烘漏了）就走直线
        if (_agent.isOnNavMesh)
        {
            _directTarget = null;
            _agent.SetDestination(targetPos);
            return;
        }

        _directTarget = targetPos;
    }

    public void SetSpeed(float speed = 0)
    {
        if (_agent != null)
        {
            _agent.speed = speed;
            _agent.isStopped = (speed <= 0.01f);
        }

        if (speed <= 0.01f) _directTarget = null;
    }

    /// <summary>停住：清掉速度与路径，否则死亡后还会因为惯性滑一段</summary>
    public void Stop()
    {
        _directTarget = null;
        if (_agent == null) return;

        // 【关键修复】必须判断它是否在 NavMesh 上
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            _agent.ResetPath();
        }
        else
        {
            // 如果没在网格上，说明它本来就没动，直接清空速度即可（或者直接 return）
            _agent.velocity = Vector3.zero;
        }
    }

    public bool HasReachedDestination()
    {
        if (_agent == null) return false;
        if (_agent.pathPending) return false;
        return _agent.remainingDistance <= _agent.stoppingDistance;
    }
    /// <summary>
    /// 随机选一个方向移动
    /// </summary>
    /// <param name="distance">随机移动的距离</param>
    public void MoveToRandomDirection(float distance = 5f)
    {
        if (_agent == null || !_agent.isActiveAndEnabled) return;

        Vector2 randomDir = Random.insideUnitCircle.normalized;//方向
        Vector2 targetPos = (Vector2)transform.position + randomDir * distance;

        //防止点落在墙里或地图外
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, distance, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
    }
}