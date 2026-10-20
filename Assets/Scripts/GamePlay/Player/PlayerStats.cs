using GamePlay.Configs;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>
    /// 主角运行时数值。
    /// 对局开始时从 <see cref="PlayerConfig"/> 拷一份基础值，之后由状态与道具在此之上增删
    /// —— 配置是静态基础值，这里是当前值。
    /// </summary>
    public class PlayerStats
    {
        // 生命
        public float Hp;
        public float MaxHp;
        public bool IsDead;

        /// <summary>瓶盖数量</summary>
        public int Caps;

        // 移动
        public float MoveSpeed;
        public float SprintSpeed;
        public float SprintCd;
        public float SprintDuration;

        // 喷射累积伤害
        public float SprintDamagePerTick;
        public float SprintTickInterval;
        public float SprintCartExtraDamage;

        //普通攻击
        public float PrimaryAtkInterval;

        // 近战 J
        public float MeleeDamage;
        public float MeleeRange;

        // 远程 Q
        public float RangedDamage;
        public float RangedRange;

        // 倍率基准
        public float AttackSpeed;

        // 液体切换 Tab
        public float ChangeLiquidCd;

        // 持有道具
        public bool HasMantos;

        // ===== 道具增益（购买时由 RecalcProps 汇总） =====
        /// <summary>伤害倍率，1 = 原始</summary>
        public float DamageMultiplier = 1f;
        /// <summary>攻速加成，0.15 = +15%</summary>
        public float AttackSpeedBonus;
        /// <summary>技能冷却倍率，1 = 原始</summary>
        public float SkillCdMultiplier = 1f;
        /// <summary>是否免疫持续伤害（手套）</summary>
        public bool ImmuneDot;
        /// <summary>每多少秒补一层护盾，0 = 无</summary>
        public float ShieldInterval;
        public int ShieldMax = 1;
        /// <summary>当前护盾层数（可挡对应次数的伤害）</summary>
        public int Shield;
        public float StunChanceMinion;
        public float StunChanceElite;
        public float StunDuration = 1.2f;

        private float _shieldTimer;

        /// <summary>
        /// 按「当前已拥有的道具」重算增益。买道具 / 领赠品之后调一次即可。
        /// </summary>
        public void RecalcProps(PropConfigs props)
        {
            DamageMultiplier = 1f;
            AttackSpeedBonus = 0f;
            SkillCdMultiplier = 1f;
            ImmuneDot = false;
            ShieldInterval = 0f;
            ShieldMax = 1;
            StunChanceMinion = 0f;
            StunChanceElite = 0f;

            if (props != null && props.allProps != null)
            {
                for (int i = 0; i < props.allProps.Count; i++)
                {
                    PropConfig p = props.allProps[i];
                    if (p == null || !p.isHold) continue;

                    DamageMultiplier += p.DamageBonus;
                    AttackSpeedBonus += p.AttackSpeedBonus;

                    if (p.SkillCdReduction > 0f)
                        SkillCdMultiplier *= 1f - Mathf.Clamp01(p.SkillCdReduction);

                    if (p.ImmuneDot) ImmuneDot = true;

                    if (p.ShieldInterval > 0f)
                    {
                        ShieldInterval = p.ShieldInterval;
                        ShieldMax = Mathf.Max(ShieldMax, Mathf.Max(1, p.ShieldMax));
                    }

                    StunChanceMinion = Mathf.Max(StunChanceMinion, p.StunChanceMinion);
                    StunChanceElite = Mathf.Max(StunChanceElite, p.StunChanceElite);
                    if (p.StunDuration > 0f) StunDuration = p.StunDuration;
                }
            }

            Shield = Mathf.Clamp(Shield, 0, ShieldMax);
        }

        /// <summary>护盾按间隔补层（由 PlayerController 每帧驱动）</summary>
        public void TickShield(float deltaTime)
        {
            if (ShieldInterval <= 0f || IsDead || Shield >= ShieldMax) return;

            _shieldTimer += deltaTime;
            if (_shieldTimer < ShieldInterval) return;

            _shieldTimer = 0f;
            Shield++;
            Debug.Log($"[护盾] 获得 1 层（{Shield}/{ShieldMax}）");
        }

        /// <summary>持续伤害（火路 / 亡语之类）：手套免疫</summary>
        public void ApplyDot(float damage)
        {
            if (ImmuneDot) return;
            ApplyDamage(Mathf.CeilToInt(damage));
        }

        // ===== 脱战回血 =====
        /// <summary>每秒回复量（0 = 不回血）</summary>
        public float RegenPerSecond;
        /// <summary>受伤后多久开始回血</summary>
        public float RegenDelay = 3f;

        private float _lastHurtTime = -99f;
        private bool _regenning;

        /// <summary>是否正在回血（HUD 想加特效可以用）</summary>
        public bool IsRegenning => _regenning;

        /// <summary>
        /// 脱战回血：只有「站着不动 + 不在战斗中 + 已经脱离受伤状态」才生效。
        /// 由 PlayerController 每帧驱动。
        /// </summary>
        public void TickRegen(float deltaTime, bool idleAndSafe)
        {
            if (IsDead || RegenPerSecond <= 0f) { _regenning = false; return; }

            if (!idleAndSafe || Hp >= MaxHp || Time.time - _lastHurtTime < RegenDelay)
            {
                _regenning = false;
                return;
            }

            bool wasFull = Hp >= MaxHp;
            Hp = Mathf.Clamp(Hp + RegenPerSecond * deltaTime, 0f, MaxHp);
            _regenning = true;

            if (!wasFull && Hp >= MaxHp) Debug.Log("[回血] 已回满");
        }

        public void Init(PlayerConfig config)
        {
            MaxHp = config.MaxHp;
            Hp = MaxHp;

            Caps = config.StartCaps;

            MoveSpeed = config.MoveSpeed;
            SprintSpeed = config.SprintSpeed;
            SprintCd = config.SprintCd;
            SprintDuration = config.SprintDuration;

            SprintDamagePerTick = config.SprintDamagePerTick;
            SprintTickInterval = config.SprintTickInterval;
            SprintCartExtraDamage = config.SprintCartExtraDamage;

            PrimaryAtkInterval = config.PrimaryAtkInterval;

            MeleeDamage = config.MeleeDamage;
            MeleeRange = config.MeleeRange;

            RangedDamage = config.RangedDamage;
            RangedRange = config.RangedRange;

            AttackSpeed = config.AttackSpeed;

            ChangeLiquidCd = config.ChangeLiquidCd;

            RegenPerSecond = config.RegenPerSecond;
            RegenDelay = config.RegenDelay;
            _lastHurtTime = -99f;
            _regenning = false;

            IsDead = false;
            HasMantos = false;

            // 新局重置道具增益（道具 isHold 在 StartNewGame 里已清）
            DamageMultiplier = 1f;
            AttackSpeedBonus = 0f;
            SkillCdMultiplier = 1f;
            ImmuneDot = false;
            ShieldInterval = 0f;
            ShieldMax = 1;
            Shield = 0;
            StunChanceMinion = 0f;
            StunChanceElite = 0f;
            _shieldTimer = 0f;
        }

        /// <summary>
        /// 玩家回复血量
        /// </summary>
        /// <param name="heal"></param>
        public void Healing(int heal)
        {
            if (IsDead)
                return;

            Hp = Mathf.Clamp(Hp + heal, 0, MaxHp);

            //动画与音效
        }
        /// <summary>受击音最小间隔（秒）：持续伤害时避免变成机关枪</summary>
        private const float HurtSfxInterval = 0.18f;
        private float _lastHurtSfxTime = -10f;

        /// <summary>
        /// 玩家受到伤害
        /// </summary>
        /// <param name="dmg"></param>
        public void ApplyDamage(int dmg)
        {
            if (IsDead)
                return;

            // 护盾优先挡：一层抵一次完整伤害
            if (Shield > 0)
            {
                Shield--;
                Debug.Log($"[护盾] 挡下 {dmg} 点伤害，剩余 {Shield} 层");
                Framework.Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("SFX_Player_Shield");
                return;
            }

            Hp = Mathf.Clamp(Hp - dmg, 0, MaxHp);
            _lastHurtTime = Time.time;      // 脱战回血要等 RegenDelay 之后才开始
            Debug.Log("玩家受伤");
            //动画与音效
            if (Hp <= 0)
            {
                IsDead = true;
                //死亡动画与音效
                Framework.Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("SFX_Player_Death");
            }
            else if (Time.time - _lastHurtSfxTime >= HurtSfxInterval)
            {
                _lastHurtSfxTime = Time.time;

                // 受击音：三个变体随机，避免每次同一个
                int pick = UnityEngine.Random.Range(0, 3);
                string hurtKey = pick == 0 ? "SFX_Player_Hurt" : (pick == 1 ? "SFX_Player_Hurt_1" : "SFX_Player_Hurt_2");
                Framework.Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx(hurtKey);
            }

        }
    }
}
