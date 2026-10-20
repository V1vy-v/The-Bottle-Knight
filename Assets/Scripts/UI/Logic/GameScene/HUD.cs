using Framework;
using GamePlay;
using GamePlay.Configs;
using GamePlay.Enemy;
using GamePlay.Interaction;
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

        /// <summary>
        /// 界面参数。HUD 是运行时挂出来的组件，预制体上只有 UIView，
        /// 所以可调参数都在 HUDProperties 里（预制体 → UIView → 界面属性）
        /// </summary>
        private HUDProperties _cfg;

        /// <summary>已拿到的道具图标，按需创建后复用</summary>
        private readonly System.Collections.Generic.List<Image> _itemIcons = new();

        /// <summary>MapRoot（遮罩），按名字从 HUD 下取；不依赖 binding 的 map 字段</summary>
        private RectTransform _mapRoot;
        /// <summary>MapRoot 下三张地图，按阶段切换显示</summary>
        private readonly System.Collections.Generic.List<RectTransform> _maps = new();
        private int _mapStage = -1;

        /// <summary>本阶段开始时的主角位置 = 世界里对应「出生点在图上位置」的那个点</summary>
        private Vector2 _mapAnchorWorld;
        private bool _hasMapAnchor;
        private float _mapAnchorReadyTime;

        /// <summary>图上出生点相对图片中心的像素偏移（左下为原点换算成中心为原点）</summary>
        private Vector2 _mapAnchorOffsetPx;
        /// <summary>每世界单位占多少像素（X）</summary>
        private float _mapPxPerWorld = 1f;
        /// <summary>每世界单位占多少像素（Y）。没配覆盖高度时等于 X 的缩放</summary>
        private float _mapPxPerWorldY = 1f;

        /// <summary>运行期可调的覆盖宽度 / 覆盖高度 / 出生点（调试时用按键微调，不用改资产重编译）</summary>
        private float[] _coveredOverride;
        private float[] _coveredHeightOverride;
        private Vector2[] _uvOverride;

        /// <summary>主角点（固定在遮罩中心）</summary>
        private Image _playerMark;
        /// <summary>敌人红点 / 交互感叹号，复用池</summary>
        private readonly System.Collections.Generic.List<Image> _enemyDots = new();
        private readonly System.Collections.Generic.List<Image> _interactMarks = new();

        /// <summary>下次重扫敌人的时间（FindObjectsOfType 不能每帧）</summary>
        private float _nextScan;

        /// <summary>下次打印小地图校准信息的时间</summary>
        private float _nextDebug;

        /// <summary>已经按贴图校正过尺寸的那张地图</summary>
        private RectTransform _fittedMap;

        /// <summary>本阶段是否已经提示过长宽比不一致</summary>

        /// <summary>瓶盖数量显示</summary>
        private TextMeshProUGUI _capsLabel;
        private int _capsCache = int.MinValue;
        private readonly System.Collections.Generic.List<Vector2> _enemyPositions = new();
        private readonly System.Collections.Generic.List<Vector2> _interactPositions = new();

        // ---- 参数取值：优先 HUDProperties，没配就用内置默认 ----
        private Vector2 CfgItemIconSize => _cfg != null ? _cfg.itemIconSize : new Vector2(80f, 80f);
        private Vector2 CfgPlayerMarkSize => _cfg != null ? _cfg.playerMarkSize : new Vector2(16f, 16f);
        private Vector2 CfgEnemyDotSize => _cfg != null ? _cfg.enemyDotSize : new Vector2(14f, 14f);
        private Vector2 CfgInteractSize => _cfg != null ? _cfg.interactMarkSize : new Vector2(22f, 22f);

        #region 生命周期

        protected override void Init()
        {
            // 界面参数（可空，取不到就用内置默认）
            _cfg = Properties as HUDProperties;

            // 血条已经由美术在预制体里配好 Filled（纵向），这里只取引用，不改它的设置
            _healthFill = ImageOf(_healthBar);

            // 瓶盖数字：MoneyRoot/NumTxt 上的 TMP
            _capsLabel = _numTxt != null ? _numTxt.GetComponent<TextMeshProUGUI>() : null;

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

            // ---- 瓶盖 ----
            if (_capsLabel != null && _stats.Caps != _capsCache)
            {
                _capsCache = _stats.Caps;
                _capsLabel.text = _stats.Caps.ToString();
            }

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

            // ---- 道具图标 / 小地图 ----
            RefreshItems();
            RefreshAtkMode();
            RefreshMap();
        }

        #region 近战 / 远战 状态

        private RectTransform _atkSlot;
        private Image _atkSlotIcon;
        private GameObject _atkSlotLock;
        private TextMeshProUGUI _atkModeLabel;
        private string _atkModeCache;

        /// <summary>
        /// 攻击模组槽位：预制体里没有这一格（只有切液体的 Toggle），
        /// 所以运行时克隆一份 Toggle 槽，保证描边 / 锁 / 尺寸和其它槽完全一致，
        /// 摆到 Toggle 正上方。图标可由 HUDProperties 换（近战 / 远战各一张）。
        /// </summary>
        private void EnsureAtkModuleSlot()
        {
            if (_atkSlot != null) return;
            if (_toggleImg == null) return;

            RectTransform source = _toggleImg.parent as RectTransform;
            if (source == null) return;

            GameObject clone = Instantiate(source.gameObject, source.parent);
            clone.name = "AtkModuleSlot";

            _atkSlot = (RectTransform)clone.transform;

            // 摆在「切换液体」上面一格（间距按槽位自身高度算，换分辨率也不会叠）
            float gap = source.rect.height + 16f;
            _atkSlot.anchoredPosition = source.anchoredPosition + new Vector2(0f, gap);

            _atkSlotIcon = clone.GetComponentInChildren<Image>(true);

            Transform lockT = clone.transform.Find("Lock");
            if (lockT == null)
            {
                for (int i = 0; i < clone.transform.childCount; i++)
                {
                    Transform c = clone.transform.GetChild(i);
                    if (c.name.StartsWith("Lock")) { lockT = c; break; }
                }
            }
            _atkSlotLock = lockT != null ? lockT.gameObject : null;

            // 近战/远战没有冷却，冷却遮罩藏掉
            Transform cd = clone.transform.Find("CdMask");
            if (cd != null) cd.gameObject.SetActive(false);
        }

        private void EnsureAtkModeLabel()
        {
            if (_atkModeLabel != null) return;

            Transform parent = _atkSlot != null ? _atkSlot : _toggleImg;
            if (parent == null) return;

            var go = new GameObject("AtkModeLabel", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -(_atkSlot != null ? _atkSlot.rect.height * 0.5f + 22f : 72f));
            rt.sizeDelta = new Vector2(160f, 40f);

            _atkModeLabel = go.AddComponent<TextMeshProUGUI>();
            if (_capsLabel != null) _atkModeLabel.font = _capsLabel.font;
            _atkModeLabel.fontSize = 26f;
            _atkModeLabel.alignment = TextAlignmentOptions.Center;
            _atkModeLabel.raycastTarget = false;
            _atkModeLabel.text = string.Empty;
        }

        private void RefreshAtkMode()
        {
            EnsureAtkModuleSlot();
            EnsureAtkModeLabel();

            PlayerCombat combat = _playerGO != null ? _playerGO.GetComponent<PlayerCombat>() : null;

            bool unlocked = combat != null && combat.CanSwitchModule;
            bool ranged = combat != null && combat.CurrentModule == AttackModule.Ranged;

            // ---- 槽位图标 / 锁 ----
            if (_atkSlotLock != null) _atkSlotLock.SetActive(combat != null && !unlocked);

            if (_atkSlotIcon != null)
            {
                Sprite want = null;
                if (_cfg != null) want = ranged ? _cfg.rangedModuleSprite : _cfg.meleeModuleSprite;
                if (want != null) _atkSlotIcon.sprite = want;

                // 没配美术图时用颜色区分：近战白 / 远战青蓝 / 未解锁压暗
                if (combat == null) _atkSlotIcon.color = new Color(1f, 1f, 1f, 0f);
                else if (!unlocked) _atkSlotIcon.color = LockedTint;
                else if (ranged) _atkSlotIcon.color = new Color(0.55f, 0.9f, 1f, 1f);
                else _atkSlotIcon.color = Color.white;
            }

            // ---- 文字 ----
            if (_atkModeLabel == null) return;

            string text;
            Color color;

            if (combat == null)
            {
                text = string.Empty;
                color = Color.white;
            }
            else if (!unlocked)
            {
                text = "近战";                                  // 还没拿吸管
                color = new Color(1f, 1f, 1f, 0.6f);
            }
            else if (ranged)
            {
                text = "远战";
                color = new Color(0.4f, 0.95f, 1f, 1f);         // 青蓝：远战
            }
            else
            {
                text = "近战";
                color = Color.white;
            }

            if (text != _atkModeCache)
            {
                _atkModeCache = text;
                _atkModeLabel.text = text;
            }

            _atkModeLabel.color = color;
        }

        #endregion

        /// <summary>
        /// 道具栏：把已拥有（PropConfig.isHold）的道具图标铺到 Content 里。
        /// Content 上挂了 GridLayoutGroup，只管建/复用 Image，位置交给它排。
        /// </summary>
        private void RefreshItems()
        {
            if (_content == null) return;

            ConfigManager cfg = ConfigManager.Instance;
            PropConfigs all = cfg != null ? cfg.Props : null;
            if (all == null) return;

            int shown = 0;
            for (int i = 0; i < all.allProps.Count; i++)
            {
                PropConfig prop = all.allProps[i];
                if (prop == null || !prop.isHold || prop.sprite == null) continue;

                Image icon = shown < _itemIcons.Count ? _itemIcons[shown] : CreateItemIcon();
                if (icon == null) return;

                icon.sprite = prop.sprite;
                icon.gameObject.SetActive(true);
                shown++;
            }

            // 多出来的先藏起来，不销毁（道具只会增加）
            for (int i = shown; i < _itemIcons.Count; i++)
            {
                if (_itemIcons[i] != null) _itemIcons[i].gameObject.SetActive(false);
            }
        }

        private Image CreateItemIcon()
        {
            var go = new GameObject("PropIcon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_content, false);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = CfgItemIconSize;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;

            _itemIcons.Add(image);
            return image;
        }

        /// <summary>
        /// 小地图（雷达式）：
        /// 遮罩 MapRoot 固定，按剧情阶段显示 MapRoot 下对应那张地图，
        /// 地图反向平移让主角永远落在遮罩中心；主角点固定在中心，
        /// 敌人画红点、可交互物画感叹号，都挂在地图下面跟着一起滑动。
        /// </summary>
        private void RefreshMap()
        {
            if (_player == null) return;

            if (_mapRoot == null)
            {
                _mapRoot = transform.Find("MapRoot") as RectTransform;
                if (_mapRoot == null)
                {
                    Debug.LogWarning("[HUD] 小地图：HUD 下找不到 MapRoot");
                    return;
                }

                _maps.Clear();
                for (int i = 0; i < _mapRoot.childCount; i++)
                {
                    if (_mapRoot.GetChild(i) is RectTransform rt) _maps.Add(rt);
                }

                // 主角点固定在遮罩中心，不跟地图滑动
                _playerMark = CreateMark(_mapRoot, "PlayerMark", CfgPlayerMarkSize,
                                         _cfg != null ? _cfg.playerMarkSprite : null, Color.white);
            }

            ApplyMapStage();

            // 调试按键微调（[ ] 缩放，, . 左右，; ' 上下）
            EnsureMapConfig();
            PollMapTuning();

            // 进本阶段后等一小会儿（转场传送完成）再记录锚点
            if (!_hasMapAnchor && Time.unscaledTime >= _mapAnchorReadyTime && _player != null)
            {
                _mapAnchorWorld = _player.transform.position;
                _hasMapAnchor = true;
                Debug.Log($"[小地图] 记录出生锚点：世界=({_mapAnchorWorld.x:F1}, {_mapAnchorWorld.y:F1})");
            }
            if (!_hasMapAnchor) return;

            RectTransform map = _mapStage >= 0 && _mapStage < _maps.Count ? _maps[_mapStage] : null;
            if (map == null) return;

            // 地图尺寸由贴图比例决定，免得手改 RectTransform 把高度写成 0
            FitMapSize(map);

            // 缩放：整张图覆盖 coveredWidth 个世界单位，所以每世界单位占 map.rect.width/coveredWidth 像素
            float covered = CoveredWorldWidth(_mapStage);
            _mapPxPerWorld = map.rect.width / Mathf.Max(0.001f, covered);

            // Y 轴单独算：贴图长宽比和世界区域长宽比不一定一致，用同一个缩放会让标记上下漂移
            float coveredH = CoveredWorldHeight(_mapStage);
            _mapPxPerWorldY = coveredH > 1f ? map.rect.height / coveredH : _mapPxPerWorld;

            // 图上出生点相对图片中心的像素偏移
            Vector2 sizePx = map.rect.size;
            Vector2 spawnUv = SpawnUv(_mapStage);
            _mapAnchorOffsetPx = (spawnUv - new Vector2(0.5f, 0.5f)) * sizePx;

            Vector2 world = _player.transform.position;

            // 钳制：图片边缘不要拖进遮罩中心。地图能覆盖的最大偏移 = 图片半宽
            Vector2 half = sizePx * 0.5f;
            Vector2 wanted = MapLocal(world);
            Vector2 clamped = new Vector2(Mathf.Clamp(wanted.x, -half.x, half.x),
                                          Mathf.Clamp(wanted.y, -half.y, half.y));
            map.anchoredPosition = -clamped;

            if (_cfg != null && _cfg.debugMapPosition && Time.unscaledTime >= _nextDebug)
            {
                _nextDebug = Time.unscaledTime + 1f;
                Vector2 uv = new Vector2(clamped.x / sizePx.x + 0.5f, clamped.y / sizePx.y + 0.5f);
                Debug.Log($"[小地图] 世界=({world.x:F1}, {world.y:F1})  " +
                          $"图上位置=({uv.x:F2}, {uv.y:F2})  " +
                          $"锚点=({_mapAnchorWorld.x:F1}, {_mapAnchorWorld.y:F1})  " +
                          $"缩放={_mapPxPerWorld:F2}px/单位");
            }

            ScanMarkers();
            PlacePool(map, _enemyDots, _enemyPositions, CfgEnemyDotSize,
                      _cfg != null ? _cfg.enemyDotSprite : null, new Color(1f, 0.25f, 0.25f, 1f));
            PlacePool(map, _interactMarks, _interactPositions, CfgInteractSize,
                      _cfg != null ? _cfg.interactSprite : null, new Color(1f, 0.9f, 0.2f, 1f));
        }

        /// <summary>某个世界坐标在地图上的局部像素坐标（原点是图片中心）</summary>
        private Vector2 MapLocal(Vector2 world)
        {
            return new Vector2(
                (world.x - _mapAnchorWorld.x) * _mapPxPerWorld + _mapAnchorOffsetPx.x,
                (world.y - _mapAnchorWorld.y) * _mapPxPerWorldY + _mapAnchorOffsetPx.y);
        }

        /// <summary>出生点在这张图上的归一化位置</summary>
        private Vector2 SpawnUv(int stageIndex)
        {
            EnsureMapConfig();
            return _uvOverride[Mathf.Clamp(stageIndex, 0, _uvOverride.Length - 1)];
        }

        /// <summary>这张图整张覆盖多少世界单位宽度</summary>
        private float CoveredWorldWidth(int stageIndex)
        {
            EnsureMapConfig();
            float v = _coveredOverride[Mathf.Clamp(stageIndex, 0, _coveredOverride.Length - 1)];
            return v > 10f ? v : 10f;
        }

        /// <summary>这张图整张覆盖多少世界单位高度。<=0 表示没配，退回用 X 的缩放（要求贴图比例恰好等于世界比例）</summary>
        private float CoveredWorldHeight(int stageIndex)
        {
            EnsureMapConfig();
            return _coveredHeightOverride[Mathf.Clamp(stageIndex, 0, _coveredHeightOverride.Length - 1)];
        }

        /// <summary>把资产里的配置读进运行期数组，之后可用调试按键微调</summary>
        private void EnsureMapConfig()
        {
            if (_coveredOverride != null) return;

            _coveredOverride = new float[3];
            _coveredHeightOverride = new float[3];
            _uvOverride = new Vector2[3];

            for (int i = 0; i < 3; i++)
            {
                _coveredOverride[i] = _cfg != null && _cfg.mapCoveredWorldWidth != null && i < _cfg.mapCoveredWorldWidth.Length
                    ? _cfg.mapCoveredWorldWidth[i]
                    : 150f;

                _coveredHeightOverride[i] = _cfg != null && _cfg.mapCoveredWorldHeight != null && i < _cfg.mapCoveredWorldHeight.Length
                    ? _cfg.mapCoveredWorldHeight[i]
                    : 0f;

                _uvOverride[i] = _cfg != null && _cfg.mapSpawnUv != null && i < _cfg.mapSpawnUv.Length
                    ? _cfg.mapSpawnUv[i]
                    : new Vector2(0.5f, 0.5f);
            }
        }

        /// <summary>
        /// 调试用实时微调（只在 debugMapPosition 打开时生效）：
        /// [ / ] 减/加覆盖宽度；, / . 左右挪出生点；; / ' 上下挪出生点。
        /// </summary>
        private void PollMapTuning()
        {
            if (_cfg == null || !_cfg.debugMapPosition) return;
            if (_mapStage < 0 || _mapStage >= 3) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            bool changed = false;

            if (kb.leftBracketKey.wasPressedThisFrame) { _coveredOverride[_mapStage] -= 5f; changed = true; }
            if (kb.rightBracketKey.wasPressedThisFrame) { _coveredOverride[_mapStage] += 5f; changed = true; }

            // PageUp / PageDown：按比例粗调整体缩放（一次 ±10%），差得远时用这个快
            if (kb.pageUpKey.wasPressedThisFrame) { _coveredOverride[_mapStage] *= 1.1f; changed = true; }
            if (kb.pageDownKey.wasPressedThisFrame) { _coveredOverride[_mapStage] /= 1.1f; changed = true; }

            if (kb.commaKey.wasPressedThisFrame) { _uvOverride[_mapStage] = _uvOverride[_mapStage] + new Vector2(-0.01f, 0f); changed = true; }
            if (kb.periodKey.wasPressedThisFrame) { _uvOverride[_mapStage] = _uvOverride[_mapStage] + new Vector2(0.01f, 0f); changed = true; }
            if (kb.semicolonKey.wasPressedThisFrame) { _uvOverride[_mapStage] = _uvOverride[_mapStage] + new Vector2(0f, -0.01f); changed = true; }
            if (kb.quoteKey.wasPressedThisFrame) { _uvOverride[_mapStage] = _uvOverride[_mapStage] + new Vector2(0f, 0.01f); changed = true; }

            if (!changed) return;

            _coveredOverride[_mapStage] = Mathf.Clamp(_coveredOverride[_mapStage], 10f, 2000f);
            Vector2 uv = _uvOverride[_mapStage];
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            _uvOverride[_mapStage] = uv;

            Debug.Log($"[小地图调参] 第 {_mapStage} 张图  覆盖宽度={_coveredOverride[_mapStage]:F0}  " +
                      $"出生点=({uv.x:F2}, {uv.y:F2})\n" +
                      $"          满意后填进 Map Covered World Width[{_mapStage}] / Map Spawn Uv[{_mapStage}]");
        }

        /// <summary>
        /// 按贴图的长宽比校正地图矩形：宽度保留美术设的值，高度按贴图比例算。
        /// 手改 RectTransform 很容易把高度写成 1（纵向缩放就变 0，表现为「地图整体偏上/不动」）。
        /// </summary>
        private void FitMapSize(RectTransform map)
        {
            if (_fittedMap == map) return;

            var image = map.GetComponent<Image>();
            if (image == null || image.sprite == null)
            {
                Debug.LogWarning($"[HUD] 小地图：{map.name} 上没有 Image 或没设 Sprite");
                return;
            }

            Rect spriteRect = image.sprite.rect;
            if (spriteRect.width < 1f || spriteRect.height < 1f) return;

            float w = map.rect.width >= 1f ? map.rect.width : spriteRect.width;
            float h = w * spriteRect.height / spriteRect.width;

            _fittedMap = map;
            if (Mathf.Abs(map.rect.height - h) <= 1f) return;

            map.sizeDelta = new Vector2(w, h);
            Debug.Log($"[HUD] 小地图：{map.name} 高度按贴图比例校正为 {w:F0}×{h:F0}" +
                      $"（贴图 {spriteRect.width:F0}×{spriteRect.height:F0}）");
        }

        /// <summary>按剧情阶段切换显示哪张地图（顺便记住它对应的世界范围）</summary>
        private void ApplyMapStage()
        {
            NodeConfig node = Global.Get<GameplayRoot>()?.Story?.CurrentNode;
            int stage = node != null ? (int)node.currentStage : 0;

            // 0=冰箱 1=厨房 3=超市 → 三张图的下标
            int index = stage <= 0 ? 0 : (stage == 1 ? 1 : 2);
            if (_maps.Count > 0 && index >= _maps.Count) index = _maps.Count - 1;

            if (index == _mapStage) return;

            _mapStage = index;
            for (int i = 0; i < _maps.Count; i++)
                _maps[i].gameObject.SetActive(i == index);

            _fittedMap = null;          // 换了地图，尺寸要重新校正

            // 每张图都要重新取出生锚点；等转场传送完再取，否则会记到传送前的位置
            _hasMapAnchor = false;
            _mapAnchorReadyTime = Time.unscaledTime + 1.2f;
        }

        /// <summary>每 0.3 秒扫一次场上的敌人与可交互物，记录世界坐标</summary>
        private void ScanMarkers()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.3f;

            _enemyPositions.Clear();
            _interactPositions.Clear();

            foreach (EnemyController enemy in FindObjectsOfType<EnemyController>())
            {
                if (enemy == null) continue;

                // 还会说话的剧情敌人挂感叹号，其余算敌人
                if (enemy.NeedsTalk) _interactPositions.Add(enemy.transform.position);
                else if (!enemy.IsDefeated) _enemyPositions.Add(enemy.transform.position);
            }

            foreach (ShopInteractable shop in FindObjectsOfType<ShopInteractable>())
            {
                if (shop != null && shop.CanInteract) _interactPositions.Add(shop.transform.position);
            }
        }

        /// <summary>把一组世界坐标摆成地图子物体上的标记，池化复用</summary>
        private void PlacePool(RectTransform map, System.Collections.Generic.List<Image> pool,
                               System.Collections.Generic.List<Vector2> points,
                               Vector2 size, Sprite sprite, Color color)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Image mark;
                if (i < pool.Count) mark = pool[i];
                else
                {
                    mark = CreateMark(map, "Mark", size, sprite, color);
                    if (mark == null) return;
                    pool.Add(mark);
                }

                mark.sprite = sprite;
                mark.rectTransform.SetParent(map, false);
                mark.gameObject.SetActive(true);
                mark.rectTransform.anchoredPosition = MapLocal(points[i]);
            }

            for (int i = points.Count; i < pool.Count; i++)
            {
                if (pool[i] != null) pool[i].gameObject.SetActive(false);
            }
        }

        private Image CreateMark(RectTransform parent, string name, Vector2 size, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = sprite;          // 有美术图就用图，留空就是纯色方块
            image.color = color;
            image.enabled = true;

            return image;
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
            // 优先问 GameplayRoot 要（最可靠）；拿不到再用 tag 兜底
            GameObject owner = Global.Get<GameplayRoot>()?.Player;
            _player = owner != null ? owner.GetComponent<PlayerController>() : null;

            if (_player == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(PlayerTag);
                _player = tagged != null ? tagged.GetComponentInParent<PlayerController>() : null;
            }

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
