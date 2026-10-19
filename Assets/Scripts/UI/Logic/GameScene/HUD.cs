using Framework;
using GamePlay;
using GamePlay.Liquid;
using GamePlay.Player;
using GamePlay.Story;
using TMPro;
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

        /// <summary>任务栏前缀。预制体里原本写的是「当前任务 ******」，前缀固定、目标由节点给</summary>
        private const string QuestPrefix = "当前任务 ";

        private TextMeshProUGUI _questLabel;
        private string _questTextCache;
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

            // 任务栏绑定给的是 RectTransform，文字在 TextMeshProUGUI 上
            _questLabel = _questTxt != null ? _questTxt.GetComponent<TextMeshProUGUI>() : null;

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
            // 任务栏不依赖主角，先刷
            RefreshQuest();

            LiquidBase main = _liquids?.CurrentLiquid;     // 主槽
            LiquidBase standby = _liquids?.StandbyLiquid;  // 备用槽

            // 还没拿到主角（HUD 先于主角出现）：四个位置都按未解锁显示，只留锁图标
            if (_stats == null)
            {
                ApplySlot(_skill1Icon, _skill1Mask, _skill1LockGO, false, 0f);
                ApplySlot(_skill2Icon, _skill2Mask, _skill2LockGO, false, 0f);
                ApplySlot(_toggleIcon, _toggleMask, _toggleLockGO, false, 0f);
                ApplySlot(_sprintIcon, _sprintMask, _sprintLockGO, false, 0f);
                return;
            }

            // ---- 血条 ----
            if (_healthFill != null)
                _healthFill.fillAmount = _stats.MaxHp > 0f ? Mathf.Clamp01(_stats.Hp / _stats.MaxHp) : 0f;

            // ---- 图标：按当前装配的液体换图（LiquidConfig.Icon）----
            // 切换小球不跟液体图标走：它表示「现在能不能切换液体槽」，用的是固定图标
            SetIcon(_skill1Icon, main);
            SetIcon(_skill2Icon, standby);

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
        /// 任务栏：直接读当前节点。节点没填 questText 就整行隐藏。
        /// 轮询而不是订阅事件 —— HUD 是 ShowUI 之后才出现的，事件会漏掉第一个节点。
        /// </summary>
        private void RefreshQuest()
        {
            if (_questLabel == null) return;

            NodeConfig node = Global.Get<GameplayRoot>()?.Story?.CurrentNode;
            string text = node != null ? node.questText : null;

            if (text == _questTextCache) return;   // 只在变化时写，避免每帧重建文本网格

            _questTextCache = text;
            bool has = !string.IsNullOrEmpty(text);

            _questLabel.gameObject.SetActive(has);
            if (has) _questLabel.text = QuestPrefix + text;
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

        /// <summary>按液体配置换技能图标。配置里没填 Icon 就保留预制体上的占位图</summary>
        private static void SetIcon(Image icon, LiquidBase liquid)
        {
            if (icon == null) return;

            Sprite sprite = liquid?.Config?.Icon;
            if (sprite != null && icon.sprite != sprite) icon.sprite = sprite;
        }

        /// <summary>
        /// 刷新一个图标位。
        /// 未解锁：只显示锁，图标与冷却扇形都隐藏；
        /// 已解锁：锁消失，图标与扇形（有冷却时）才出现。
        /// </summary>
        private static void ApplySlot(Image icon, Image cdMask, GameObject lockGO, bool unlocked, float unavailableRatio)
        {
            if (icon != null)
            {
                icon.gameObject.SetActive(unlocked);
                icon.color = Color.white;
            }

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
