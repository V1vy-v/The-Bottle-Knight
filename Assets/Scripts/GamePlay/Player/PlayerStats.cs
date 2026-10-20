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

            IsDead = false;
            HasMantos = false;
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

            Hp = Mathf.Clamp(Hp - dmg, 0, MaxHp);
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
