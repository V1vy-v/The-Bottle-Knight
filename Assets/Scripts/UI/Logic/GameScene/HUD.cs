using GamePlay.Liquid;
using GamePlay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 局内 HUD 的界面逻辑。
    /// 血量与冷却都是连续量，所以每帧轮询主角，不订阅事件。
    /// </summary>
    public partial class HUD
    {
        /// <summary>主角预制体上的 tag，用来在对局中找到主角实例</summary>
        private const string PlayerTag = "Player";

        /// <summary>锁住时图标压暗的程度</summary>
        private static readonly Color LockedTint = new Color(0.45f, 0.45f, 0.45f, 1f);

        private GameObject _playerGO;
        private PlayerController _player;
        private PlayerStats _stats;
        private PlayerLiquids _liquids;

        /// <summary>下次尝试重新抓主角的时间（主角还没生成时不要每帧 Find）</summary>
        private float _nextAcquire;

        // Image 引用（从 Binding 给的 RectTransform 上取）
        private Image _healthFill;
        private Image _sprintIcon, _sprintMask;
        private Image _skill1Icon, _skill1Mask;
        private Image _skill2Icon, _skill2Mask;
        private Image _toggleIcon, _toggleMask;

        // 锁的物体。注意名字不能叫 _xxxLock —— Binding 里已经有一组同名的 RectTransform
        private GameObject _sprintLockGO, _skill1LockGO, _skill2LockGO, _toggleLockGO;

        #region 生命周期

        protected override void Init()
        {
            // 血条已经由美术在预制体里配好 Filled（纵向），这里只取引用，不改它的设置
            _healthFill = ImageOf(_healthBar);

            _sprintMask = ImageOf(_sprintCdMask);
            _skill1Mask = ImageOf(_skill1CdMask);
            _skill2Mask = ImageOf(_skillCdMask);
            _toggleMask = ImageOf(_toggleCdMask);

            _sprintIcon = ImageOf(_sprintImg);
            _skill1Icon = ImageOf(_skill1Img);
            _skill2Icon = ImageOf(_skill2Img);
            _toggleIcon = ImageOf(_toggleImg);

            _sprintLockGO = GameObjectOf(_sprintLock);
            _skill1LockGO = GameObjectOf(_skill1Lock);
            _skill2LockGO = GameObjectOf(_skill2Lock);
            _toggleLockGO = GameObjectOf(_toggleLock);

            WarnIfMissing(_healthFill, "healthBar");
            WarnIfMissing(_skill1Mask, "skill1CdMask");
            WarnIfMissing(_skill2Mask, "skillCdMask");

            // <ui-bind:add>
            // </ui-bind:add>
        }

        protected override void AddListener()
        {
        }

        protected override void RemoveListener()
        {
            // <ui-bind:remove>
            // </ui-bind:remove>
            base.RemoveListener();
        }

        protected override void UpdateView()
        {
            Refresh();
        }

        /// <summary>
        /// 每帧刷新。不用 UpdateView —— 基类的 UpdateView 只在 Show 时调一次，
        /// 而血量和冷却都是连续变化的。
        /// </summary>
        private void LateUpdate()
        {
            // 主角在对局开始后才生成，结束对局会销毁，所以要能重新抓
            if (_playerGO == null && Time.unscaledTime >= _nextAcquire)
            {
                _nextAcquire = Time.unscaledTime + 0.25f;
                AcquirePlayer();
            }

            Refresh();
        }

        #endregion

        #region 刷新

        private void Refresh()
        {
            if (_stats == null) return;

            // ---- 血条 ----
            if (_healthFill != null)
                _healthFill.fillAmount = _stats.MaxHp > 0f ? Mathf.Clamp01(_stats.Hp / _stats.MaxHp) : 0f;

            LiquidBase main = _liquids?.CurrentLiquid;     // 主槽
            LiquidBase standby = _liquids?.StandbyLiquid;  // 备用槽

            // ---- 主槽技能 ----
            ApplySlot(_skill1Icon, _skill1Mask, _skill1LockGO, main != null, UnavailableRatio(main));

            // ---- 备用槽技能 ----
            // 备用槽的液体也会被 PlayerCombat 每帧 TickSkill，所以它的冷却进度是真实的；
            // 只是要换到主槽才能按键释放。
            ApplySlot(_skill2Icon, _skill2Mask, _skill2LockGO, standby != null, UnavailableRatio(standby));

            // ---- Tab 切换液体 ----
            // 解锁 = 备用槽里有可作主形态的液体；扇形 = PlayerLiquids 的切换冷却
            bool toggleUnlocked = _liquids != null && _liquids.HasSwitchableLiquid;
            float toggleRatio = Ratio(_liquids?.ChangeLiquidCdRemain ?? 0f,
                                      _liquids?.ChangeLiquidCdTotal ?? 0f);
            ApplySlot(_toggleIcon, _toggleMask, _toggleLockGO, toggleUnlocked, toggleRatio);

            // ---- 喷射（点按型，没有解锁条件）----
            float sprintRatio = Ratio(_player?.SprintCdRemain ?? 0f, _player?.SprintCdTotal ?? 0f);
            ApplySlot(_sprintIcon, _sprintMask, _sprintLockGO, true, sprintRatio);
        }

        /// <summary>
        /// 液体「不可用进度」0~1，直接喂 fillAmount。
        /// LiquidBase 是技能结束时才起冷却，所以分两段：持续中按持续时间，冷却中按冷却时间。
        /// 两段衔接处都是 1，扫圈不会跳。
        /// </summary>
        private static float UnavailableRatio(LiquidBase liquid)
        {
            if (liquid == null) return 0f;

            if (liquid.IsSkillActive)
            {
                if (liquid.Config != null && liquid.Config.SkillDuration > 0f)
                    return Mathf.Clamp01(liquid.SkillElapsed / liquid.Config.SkillDuration);
                return 1f;
            }

            if (liquid.CdRemain > 0f)
            {
                if (liquid.Config != null && liquid.Config.SkillCd > 0f)
                    return Mathf.Clamp01(liquid.CdRemain / liquid.Config.SkillCd);
                return 1f;
            }

            return 0f;
        }

        /// <summary>剩余 / 总时长 -> 0~1。总时长为 0 时按「只要还在冷却就是满格」处理</summary>
        private static float Ratio(float remain, float total)
        {
            if (remain <= 0f) return 0f;
            return total > 0f ? Mathf.Clamp01(remain / total) : 1f;
        }

        /// <summary>刷新一个图标位：图标压暗 + 冷却扇形 + 锁定显隐</summary>
        private static void ApplySlot(Image icon, Image cdMask, GameObject lockGO, bool unlocked, float unavailableRatio)
        {
            if (icon != null)
                icon.color = unlocked ? Color.white : LockedTint;

            if (cdMask != null)
            {
                float ratio = Mathf.Clamp01(unavailableRatio);
                cdMask.fillAmount = ratio;
                // 没冷却时整块关掉，省一次半透明叠加
                cdMask.gameObject.SetActive(unlocked && ratio > 0.001f);
            }

            if (lockGO != null)
                lockGO.SetActive(!unlocked);
        }

        #endregion

        #region 取主角

        private void AcquirePlayer()
        {
            // 主角身上有 "Player" tag（EnemyDetectZone 也是靠它认人的）。
            // 用 GetComponentInParent 是因为 tag 可能挂在带碰撞体的子物体上。
            GameObject tagged = GameObject.FindGameObjectWithTag(PlayerTag);
            _player = tagged != null ? tagged.GetComponentInParent<PlayerController>() : null;

            _playerGO = _player != null ? _player.gameObject : null;
            _stats = _player?.PlayerStats;
            _liquids = _player?.PlayerLiquids;
        }

        #endregion

        #region 小工具

        private static Image ImageOf(RectTransform rt) => rt != null ? rt.GetComponent<Image>() : null;

        private static GameObject GameObjectOf(RectTransform rt) => rt != null ? rt.gameObject : null;

        private static void WarnIfMissing(Object bindTarget, string bindingName)
        {
            if (bindTarget == null)
                Debug.LogWarning($"[HUD] 绑定 {bindingName} 上找不到 Image，对应图标不会被刷新");
        }

        #endregion
    }
}
