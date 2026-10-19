using Framework;
using GamePlay.Enemy;
using GamePlay.Liquid;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.Player
{
    public enum AttackModule
    {
        Melee = 0,
        Ranged = 1,
    }
    public class PlayerCombat : MonoBehaviour
    {
        private PlayerStats _stats;
        private PlayerLiquids _liquids;
        private PlayerView _view;
        [SerializeField] private Vector2 _primaryMeleeAtkOffset;
        [SerializeField] private Vector2 _primaryRangedAtkOffset;
        [SerializeField] private LayerMask _enemyLayerMask;
        [SerializeField] private Transform _bulletRoot;

        private bool _isStrawGained = false;
        private AttackModule _currentAtkModule = AttackModule.Melee;
        private float _elapsed;

        private LineRenderer _rangeRenderer;

        private float _facingDir = 1f;
        private SpriteRenderer _spriteRenderer;

        public void Init(PlayerStats stats, PlayerView view, PlayerLiquids liquids)
        {
            _stats = stats;
            _view = view;
            _liquids = liquids;
        }

        private void Awake()
        {
            _rangeRenderer = gameObject.AddComponent<LineRenderer>();
            _rangeRenderer.startWidth = 0.1f;
            _rangeRenderer.endWidth = 0.1f;
            _rangeRenderer.useWorldSpace = true;
            _rangeRenderer.enabled = false;
            _rangeRenderer.sortingOrder = 999;

            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            _liquids?.CurrentLiquid?.TickSkill(Time.deltaTime);
            _liquids?.StandbyLiquid?.TickSkill(Time.deltaTime);
            TickPrimaryAtk(Time.deltaTime);

            UpdateFacingDir();
        }

        private void UpdateFacingDir()
        {
            if (transform.lossyScale.x < 0)
                _facingDir = -1f;
            else if (_spriteRenderer != null && _spriteRenderer.flipX)
                _facingDir = -1f;
            else
                _facingDir = 1f;
        }

        private Vector2 SearchForEnemy()
        {
            return Vector2.zero; // 目前没用上，远程已经改成纯水平射击了
        }

        public void PrimaryAttack()
        {
            if (_elapsed > 0) return;
            _elapsed = _stats.PrimaryAtkInterval / (1f + _liquids.AttackSpeedBonus);

            if (_liquids?.CurrentLiquid?.Attack() == true) return;

            if (_currentAtkModule == AttackModule.Melee)
            {
                _view.PlayAttack(AttackModule.Melee);
                float finalRadius = _stats.MeleeRange * (1f + _liquids.RangeBonus);

                Vector2 actualOffset = new Vector2(_primaryMeleeAtkOffset.x * _facingDir, _primaryMeleeAtkOffset.y);
                Vector2 center = (Vector2)transform.position + actualOffset;

                RangeAttack(actualOffset, finalRadius);
                StartCoroutine(DrawAttackRange(center, finalRadius));
            }
            else
            {
                _view.PlayAttack(AttackModule.Ranged);

                // 【核心修改】强制纯左右方向，彻底没有 Y 轴！
                Vector2 dir = new Vector2(_facingDir, 0f);

                // 计算生成点偏移（同样根据朝向翻转）
                Vector2 actualRangedOffset = new Vector2(_primaryRangedAtkOffset.x * _facingDir, _primaryRangedAtkOffset.y);
                Vector2 spawnPos = (Vector2)transform.position + actualRangedOffset;

                // 补全伤害计算（把你的基础伤害乘上液体加成）
                float finalDmg = _stats.RangedDamage * (1f + _liquids.AtkBonus);

                // 呼叫发射
                DirectionalAttack(dir, spawnPos, _liquids?.CurrentLiquid?.Config?.BulletPrefabRes, finalDmg);
            }
        }

        // 【完善】让子弹真正动起来
        private void DirectionalAttack(Vector2 dir, Vector2 pos, string projectileRes, float dmg)
        {
            if (string.IsNullOrEmpty(projectileRes)) return;

            // 实例化子弹
            GameObject bulletGO = Global.Instantiate(projectileRes, new YooAsset.InstantiateOptions(true, _bulletRoot, false));
            if (bulletGO == null) return;

            // 设置世界坐标位置
            bulletGO.transform.position = pos;

            // 获取子弹身上的脚本并初始化
            var bullet = bulletGO.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.Init(dir, dmg);
            }
        }

        public void UseSkill()
        {
            var cur = _liquids?.CurrentLiquid;
            if (cur == null) return;
            if (cur.TryUseSkill()) return;

            string reason = cur.IsSkillActive ? "持续中"
                          : cur.CdRemain > 0f ? $"冷却 {cur.CdRemain:F1}s"
                          : cur is GuoJiao gj ? $"红温 {gj.RageRatio:P0}"
                          : "不可用";
            Debug.Log($"[技能] 释放失败：{cur.Config?.Name} —— {reason}");
        }

        public void SwitchModule()
        {
            if (!_isStrawGained) return;
            _currentAtkModule = _currentAtkModule == AttackModule.Melee ? AttackModule.Ranged : AttackModule.Melee;
        }

        public void UnlockRangeModule()
        {
            _isStrawGained |= true;
        }

        private void RangeAttack(Vector2 offset, float radius)
        {
            float dmg = _stats.MeleeDamage * (1f + _liquids.AtkBonus);
            Vector2 center = (Vector2)transform.position + offset;

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, _enemyLayerMask);

            bool hit = false;
            HashSet<EnemyController> damagedEnemies = new HashSet<EnemyController>();

            foreach (var col in hits)
            {
                var enemy = col.GetComponentInParent<EnemyController>();

                if (enemy == null) continue;
                if (damagedEnemies.Contains(enemy)) continue;

                enemy.ApplyDamage(dmg);
                damagedEnemies.Add(enemy);
                hit = true;
            }

            if (hit) _liquids?.CurrentLiquid?.OnHitEnemy();
        }

        private void TickPrimaryAtk(float delta)
        {
            _elapsed = Mathf.Max(0f, _elapsed - delta);
        }

        private IEnumerator DrawAttackRange(Vector2 center, float radius)
        {
            int segments = 40;
            _rangeRenderer.positionCount = segments;
            _rangeRenderer.enabled = true;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                _rangeRenderer.SetPosition(i, new Vector3(center.x + x, center.y + y, 0f));
            }

            yield return new WaitForSeconds(0.2f);
            _rangeRenderer.enabled = false;
        }
    }
}