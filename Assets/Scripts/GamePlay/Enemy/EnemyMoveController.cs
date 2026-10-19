using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 怪物移动组件，封装 NavMeshAgent
/// </summary>
public class EnemyMoveController : MonoBehaviour
{
    private NavMeshAgent _agent;
    private SpriteRenderer _sprite;

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
    }
    public void MoveTo(Vector2 targetPos)
    {
        if (_agent == null || !_agent.isActiveAndEnabled) return;
        _agent.SetDestination(targetPos);
    }

    public void SetSpeed(float speed = 0)
    {
        if (_agent != null)
        {
            _agent.speed = speed;
            _agent.isStopped = (speed <= 0.01f);
        }
    }

    /// <summary>停住：清掉速度与路径，否则死亡后还会因为惯性滑一段</summary>
    public void Stop()
    {
        if (_agent == null) return;

        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();
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