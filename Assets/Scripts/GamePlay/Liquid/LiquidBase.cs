using GamePlay.Configs;
using GamePlay.Player;
using UnityEngine;

namespace GamePlay.Liquid
{
    public abstract class LiquidBase
    {
        public LiquidConfig Config;
        public PlayerStats Stats;

        #region 技能时序
        private float _cdRemain;
        private float _elapsed;

        public bool IsSkillActive { get; private set; }

        /// <summary>剩余冷却秒数，供 UI 读取</summary>
        public float CdRemain => _cdRemain;

        /// <summary>本次技能已持续秒数，供 UI 读取</summary>
        public float SkillElapsed => _elapsed;

        public bool TryUseSkill()
        {
            if (_cdRemain > 0f || IsSkillActive || !CanUseSkill()) return false;
            IsSkillActive = true;
            _elapsed = 0f;
            OnSkillBegin();
            return true;
        }
        public void TickSkill(float dt)
        {
            OnPassiveTick(dt);

            if (IsSkillActive)
            {
                _elapsed += dt;
                OnSkillTick(dt);
                if (OnSkillShouldEnd(_elapsed))
                {
                    IsSkillActive = false;
                    OnSkillEnd();
                    _cdRemain = Config.SkillCd * (Stats != null ? Stats.SkillCdMultiplier : 1f);
                }
            }
            else if (_cdRemain > 0f)
                _cdRemain = Mathf.Max(0f, _cdRemain - dt);
        }
        public void ForceEndSkill()
        {
            if (!IsSkillActive) return;
            IsSkillActive = false;
            OnSkillEnd();
            _cdRemain = Config.SkillCd * (Stats != null ? Stats.SkillCdMultiplier : 1f);
        }

        protected virtual bool CanUseSkill() => true;
        protected virtual void OnPassiveTick(float dt) { } 
        protected abstract void OnSkillBegin();
        protected virtual void OnSkillTick(float dt) { }
        protected virtual void OnSkillEnd() { }
        protected virtual bool OnSkillShouldEnd(float elapsed) => elapsed >= Config.SkillDuration;
        #endregion

        public virtual void Init(LiquidConfig cfg, PlayerStats stats) { Config = cfg; Stats = stats; }

        public virtual void OnHitEnemy() { }
        public virtual bool Attack() => false;
    }
}