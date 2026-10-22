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
        private Transform _strawTransform;      // 吸管子物体，拿到吸管后显示
        /// <summary>吸管子物体的名字（Player.prefab 里那根叫「吸管_0」）</summary>
        private const string Straw = "吸管_0";
        private AttackModule _currentAtkModule = AttackModule.Melee;
        private float _elapsed;
        /// <summary>近战连击段数（1~3），用来轮流播三段不同的击打音</summary>
        private int _meleeComboIndex;
        /// <summary>上一拳的时间，停手超过 ComboResetTime 就重走第一段</summary>
        private float _lastMeleeTime = -10f;
        private const float ComboResetTime = 1.2f;

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

            // 吸管默认藏起来：买到吸管（UnlockRangeModule）才显示。
            // 放在 Awake 而不是依赖预制体状态，免得有人把预制体上的吸管重新点开
            Transform straw = ResolveStraw();
            if (straw != null) straw.gameObject.SetActive(false);
        }

        /// <summary>找吸管子物体并缓存；找不到只报一次警告</summary>
        private Transform ResolveStraw()
        {
            if (_strawTransform != null) return _strawTransform;

            Transform found = transform.Find(Straw);
            if (found == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == Straw) { found = child; break; }
                }
            }

            _strawTransform = found;
            if (found == null)
                Debug.LogWarning($"[PlayerCombat] 主角身上找不到吸管子物体「{Straw}」，吸管不会显示");

            return found;
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
            _elapsed = _stats.PrimaryAtkInterval / (1f + _liquids.AttackSpeedBonus + _stats.AttackSpeedBonus);

            if (_liquids?.CurrentLiquid?.Attack() == true) return;

            if (_currentAtkModule == AttackModule.Melee)
            {
                _view.PlayAttack(AttackModule.Melee);

                // 近战三段连击音：左拳 → 右拳 → 瓶顶，按顺序循环
                // 停手超过 ComboResetTime 就从第一段重新开始
                if (Time.time - _lastMeleeTime > ComboResetTime) _meleeComboIndex = 0;
                _lastMeleeTime = Time.time;

                _meleeComboIndex = _meleeComboIndex % 3 + 1;
                Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("SFX_Player_Melee_" + _meleeComboIndex);

                float finalRadius = _stats.MeleeRange * (1f + _liquids.RangeBonus);

                Vector2 actualOffset = new Vector2(_primaryMeleeAtkOffset.x * _facingDir, _primaryMeleeAtkOffset.y);
                Vector2 center = (Vector2)transform.position + actualOffset;

                RangeAttack(actualOffset, finalRadius);
                StartCoroutine(DrawAttackRange(center, finalRadius));
            }
            else
            {
                // 远程模式不放普攻动作：开枪只走子弹表现，播放「攻击」动画会显得像在近战挥管子
                if (!_isStrawGained) return;

                // 吸管的左右朝向由 PlayerView.SetFacing 每帧跟主角贴图同步，这里不再翻


                // 【核心修改】强制纯左右方向，彻底没有 Y 轴！
                Vector2 dir = new Vector2(_facingDir, 0f);

                // 计算生成点偏移（同样根据朝向翻转）
                Vector2 actualRangedOffset = new Vector2(_primaryRangedAtkOffset.x * _facingDir, _primaryRangedAtkOffset.y);
                // 子弹从吸管口出生；没吸管时用原来的偏移
                Vector2 spawnPos = _strawTransform != null
                    ? (Vector2)_strawTransform.position
                    : (Vector2)transform.position + actualRangedOffset;

                // 补全伤害计算（把你的基础伤害乘上液体加成）
                float finalDmg = _stats.RangedDamage * (1f + _liquids.AtkBonus) * _stats.DamageMultiplier;

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

            // 技能放不出来给个拒绝音，别让玩家以为按键没响应
            Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("UI_Error");
        }

        /// <summary>远战解锁了没（UI 判断能不能切）</summary>
        public bool CanSwitchModule => _isStrawGained;

        /// <summary>当前攻击模组（近战 / 远战），给 HUD 显示用</summary>
        public AttackModule CurrentModule => _currentAtkModule;

        public void SwitchModule()
        {
            if (!_isStrawGained) return;
            _currentAtkModule = _currentAtkModule == AttackModule.Melee ? AttackModule.Ranged : AttackModule.Melee;
        }

        public void UnlockRangeModule()
        {
            _isStrawGained = true;

            Transform straw = ResolveStraw();
            if (straw != null)
            {
                straw.gameObject.SetActive(true);

                // 吸管刚显示时先对齐当前朝向（PlayerView 每帧同步，这里补一次立即生效）
                _view?.ApplyStrawFacing();
            }
        }

        private void RangeAttack(Vector2 offset, float radius)
        {
            float dmg = _stats.MeleeDamage * (1f + _liquids.AtkBonus) * _stats.DamageMultiplier;
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
                TryStun(enemy);
                damagedEnemies.Add(enemy);
                hit = true;
            }

            if (hit) _liquids?.CurrentLiquid?.OnHitEnemy();
        }

        /// <summary>平底锅：普攻附带眩晕。BOSS 由 EnemyController 自己按 ImmuneStun 拒绝。</summary>
        private void TryStun(EnemyController enemy)
        {
            if (enemy == null || _stats == null) return;

            float chance = (enemy.config != null && enemy.config.Tier >= GamePlay.Configs.EnemyTier.MiniBoss)
                ? _stats.StunChanceElite
                : _stats.StunChanceMinion;

            if (chance <= 0f) return;
            if (UnityEngine.Random.value > chance) return;

            enemy.ApplyStun(_stats.StunDuration);
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