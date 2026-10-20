using System.Collections.Generic;
using GamePlay.Enemy;
using GamePlay.Player;
using UnityEngine;

namespace GamePlay.Interaction
{
    /// <summary>
    /// 潜入用的「发现区」：玩家走进来，把范围内原本被动的巡逻怪唤醒成战斗状态。
    /// <para>
    /// 摆法：一个带 BoxCollider2D（勾 IsTrigger）的物体，<b>Layer 必须是 Obstacle</b>；
    /// 区域内的敌人勾上 EnemyController 的 _passiveAtStart（平时站着/巡逻，不追人），
    /// 玩家踩进来才一拥而上。脱战仍然是原生的「走出侦测区 → 回巡逻」。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class WakeOnEnterZone : MonoBehaviour
    {
        [Tooltip("唤醒半径。填 0 表示只唤醒本物体子物体里的敌人")]
        [SerializeField] private float _radius = 18f;

        [Tooltip("敌人所在层。默认 Enemy(8)")]
        [SerializeField] private LayerMask _enemyMask = 256;

        [Tooltip("只唤醒一次；关掉则每次进入都检查一遍")]
        [SerializeField] private bool _once = true;

        [Tooltip("唤醒时弹一次教学提示，留空不弹")]
        [TextArea(1, 3)] [SerializeField] private string _wakeTip;

        private bool _fired;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_once && _fired) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;

            _fired = true;

            int count = Wake();
            Debug.Log($"[潜入] {name} 唤醒 {count} 只敌人");

            if (count > 0 && !string.IsNullOrEmpty(_wakeTip))
                TutorialTips.Show(_wakeTip);
        }

        /// <summary>唤醒范围内所有还没进入战斗的敌人，返回唤醒数量</summary>
        private int Wake()
        {
            var woken = new HashSet<EnemyController>();
            int count = 0;

            if (_radius <= 0f)
            {
                var children = GetComponentsInChildren<EnemyController>(true);
                for (int i = 0; i < children.Length; i++)
                    if (TryWake(children[i])) { woken.Add(children[i]); count++; }

                return count;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _radius, _enemyMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var enemy = hits[i].GetComponentInParent<EnemyController>();
                if (enemy == null || woken.Contains(enemy)) continue;
                if (TryWake(enemy)) { woken.Add(enemy); count++; }
            }

            return count;
        }

        /// <summary>只有「活着 + 还是被动」的敌人才需要唤醒</summary>
        private static bool TryWake(EnemyController enemy)
        {
            if (enemy == null || enemy.IsDefeated || enemy.CombatEnabled) return false;

            enemy.EnterCombat();
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (_radius <= 0f) return;

            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
