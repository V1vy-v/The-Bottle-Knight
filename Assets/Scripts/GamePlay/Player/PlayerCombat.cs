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

        // 【新增】记录当前朝向（1为右，-1为左）
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

            // 获取 SpriteRenderer 以防你是靠 flipX 翻转的
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            _liquids?.CurrentLiquid?.TickSkill(Time.deltaTime);
            _liquids?.StandbyLiquid?.TickSkill(Time.deltaTime);
            TickPrimaryAtk(Time.deltaTime);

            // 每帧实时检测角色的朝向
            UpdateFacingDir();
        }

        // 【新增】检测朝向，兼容 localScale 和 flipX
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
            return Vector2.zero;
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

                // 【修改点 1】实时计算带朝向的偏移量
                Vector2 actualOffset = new Vector2(_primaryMeleeAtkOffset.x * _facingDir, _primaryMeleeAtkOffset.y);
                Vector2 center = (Vector2)transform.position + actualOffset;

                // 传入真实的偏移量
                RangeAttack(actualOffset, finalRadius);

                // 用真实的中心点画圈
                StartCoroutine(DrawAttackRange(center, finalRadius));
            }
            else
            {
                _view.PlayAttack(AttackModule.Ranged);
                Vector2 dir = (SearchForEnemy() - (Vector2)transform.position).normalized;

                if (dir == Vector2.zero) dir = new Vector2(_facingDir, 0f);

                // 【修改点 2】远程攻击的生成点偏移也跟随翻转
                Vector2 actualRangedOffset = new Vector2(_primaryRangedAtkOffset.x * _facingDir, _primaryRangedAtkOffset.y);

                DirectionalAttack(dir, (Vector2)transform.position + actualRangedOffset, _liquids?.CurrentLiquid?.Config?.BulletPrefabRes, _stats.RangedDamage);
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

            if (_currentAtkModule == AttackModule.Melee)
                _currentAtkModule = AttackModule.Ranged;
            else
                _currentAtkModule = AttackModule.Melee;
        }

        public void UnlockRangeModule()
        {
            _isStrawGained |= true;
        }

        private void DirectionalAttack(Vector2 dir, Vector2 pos, string projectileRes, float dmg)
        {
            if (string.IsNullOrEmpty(projectileRes)) return;
            Global.Instantiate(projectileRes, new YooAsset.InstantiateOptions(true, _bulletRoot, false));
        }

        private void RangeAttack(Vector2 offset, float radius)
        {
            float dmg = _stats.MeleeDamage * (1f + _liquids.AtkBonus);

            // 这里的 center 会自动应用传入的翻转后的 offset
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

            if (hit)
            {
                _liquids?.CurrentLiquid?.OnHitEnemy();
            }
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