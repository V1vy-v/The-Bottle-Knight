using GamePlay.Configs;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>玩家表现：唯一持有 Animator / SpriteRenderer 的组件，纯 API，不含规则</summary>
    public class PlayerView : MonoBehaviour
    {
        private SpriteRenderer _sprite;
        private Animator _animator;

        [Header("形态动画")]
        [Tooltip("不装液体时的默认形态，留空则用预制体上原本的 Controller")]
        public AnimatorOverrideController AnimatorOverride;
        [Tooltip("快乐水形态")] public AnimatorOverrideController ColaForm;
        [Tooltip("国窖形态")] public AnimatorOverrideController GuoJiaoForm;
        [Tooltip("雷碧形态")] public AnimatorOverrideController LeiBiForm;
        [Tooltip("三拳形态")] public AnimatorOverrideController SanQuanForm;

        /// <summary>预制体上原本的 Controller，没有对应形态时回退到它</summary>
        private RuntimeAnimatorController _defaultController;

        private void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
            _defaultController = _animator != null ? _animator.runtimeAnimatorController : null;
        }

        private static readonly int AnimWalk = Animator.StringToHash("Walk");
        private static readonly int AnimAtk = Animator.StringToHash("PrimaryAtk");
        private static readonly int AnimRanged = Animator.StringToHash("RangedAtk");
        private static readonly int AnimDeath = Animator.StringToHash("Death");

        /// <summary>Animator 里有没有某个参数（没有就安静地退回近战动画，不报错）</summary>
        private bool HasParam(int hash)
        {
            if (_animator == null) return false;
            AnimatorControllerParameter[] ps = _animator.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].nameHash == hash) return true;
            }
            return false;
        }

        public void PlayAttack(AttackModule module) 
        {
            // 远近战分开：远程优先走 RangedAtk 触发器；Animator 还没配就退回 PrimaryAtk
            if (module == AttackModule.Ranged && HasParam(AnimRanged))
                _animator.SetTrigger(AnimRanged);
            else
                _animator.SetTrigger(AnimAtk);
        }
        public void PlayDeath()
        {
            if (_animator == null) return;
            // 动画机里没配 Death 参数就只靠音效，别让 SetTrigger 刷警告
            if (!HasParam(AnimDeath)) return;

            _animator.SetTrigger(AnimDeath);
        }

        /// <summary>复活时把动画机拉回默认状态：死亡是 Trigger，不重置会一直停在倒地帧</summary>
        public void ResetAnimator()
        {
            if (_animator == null) return;
            _animator.Rebind();
            _animator.Update(0f);
        }
        public void SetMoving(bool v) 
        { 
            _animator.SetBool(AnimWalk, v); 
        }
        public void SetFacing(float x) 
        { 
            if (Mathf.Abs(x) > 0.01f) 
                _sprite.flipX = x < 0f; 
        }

        /// <summary>
        /// 按主槽液体切换形态动画。
        /// 形态用的是 AnimatorOverrideController，基准状态机不变，
        /// 所以 Walk / PrimaryAtk / Death 这些参数与状态名照旧可用。
        /// 传 null 或没有对应形态的液体（料酒 / 冰美式）就回退默认。
        /// </summary>
        public void ApplyForm(LiquidType? type)
        {
            if (_animator == null) return;

            AnimatorOverrideController target = type switch
            {
                LiquidType.Cola => ColaForm,
                LiquidType.GuoJiao => GuoJiaoForm,
                LiquidType.LeiBi => LeiBiForm,
                LiquidType.SanQuan => SanQuanForm,
                _ => null,
            };

            if (target == null) target = AnimatorOverride;

            RuntimeAnimatorController next = target != null ? target : _defaultController;
            if (_animator.runtimeAnimatorController != next)
                _animator.runtimeAnimatorController = next;
        }
    }
}
