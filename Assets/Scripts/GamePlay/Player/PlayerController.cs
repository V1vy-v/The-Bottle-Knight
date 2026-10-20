using Framework;
using GamePlay.Configs;
using GamePlay.Liquid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GamePlay.Player
{
    /// <summary>
    /// 主角总控制器
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private const string PlayerConfigId = "Player_001";

        private LocalInputProvider _inputProvider;
        private PlayerLiquids _playerLiquids;
        private PlayerCombat _playerCombat;
        private PlayerStats _playerStats;
        public PlayerStats PlayerStats => _playerStats;
        private PlayerView _playerView;
        private PlayerInteractor _playerInteractor;
        private Rigidbody2D _rb;
        public PlayerLiquids PlayerLiquids => _playerLiquids;

        #region 给 UI 的只读状态

        /// <summary>是否正在喷射</summary>
        public bool IsSprinting => _sprintRemain > 0f;

        /// <summary>喷射剩余冷却秒数，供 UI 读取</summary>
        public float SprintCdRemain => _sprintCdRemain;

        /// <summary>喷射冷却总时长，供 UI 读取</summary>
        public float SprintCdTotal => _playerStats != null ? _playerStats.SprintCd : 0f;

        /// <summary>现在能不能喷（不在冷却里 且 不在喷射中）</summary>
        public bool SprintReady => _sprintCdRemain <= 0f && _sprintRemain <= 0f;

        #endregion

        private Vector2 _moveValue;

        /// <summary>当前移动音状态，用来判断要不要换音源（避免每帧重播）</summary>
        private enum FootstepState { None, Move, Spray }
        private FootstepState _footstepState = FootstepState.None;

        // 点按型喷射：_sprintRemain = 本次喷射还剩多久，_sprintCdRemain = 冷却还剩多久
        private float _sprintRemain;
        private float _sprintCdRemain;

        /// <summary>死亡已上报（避免每帧重复切 GameOver）</summary>
        private bool _deathReported;

        /// <summary>战败重试：原地复活，回满血</summary>
        public void Revive()
        {
            if (_playerStats == null) return;

            _playerStats.IsDead = false;
            _playerStats.Hp = _playerStats.MaxHp;
            _deathReported = false;

            // 死亡动画是 Trigger，复活要把动画机拉回默认状态
            _playerView?.ResetAnimator();
            _playerView?.SetMoving(false);

            if (_rb != null) _rb.velocity = Vector2.zero;
        }


        #region 生命周期
        private void Awake()
        {
            _inputProvider = Global.Get<LocalInputManager>().Provider;

            _playerCombat = GetComponent<PlayerCombat>();
            _playerView = GetComponent<PlayerView>();
            _playerInteractor = GetComponent<PlayerInteractor>();
            _playerLiquids = new();
            _playerStats = new();

            _rb = GetComponent<Rigidbody2D>();
        }
        private void Start()
        {
            // 配置读不到就停用
            if (!ConfigManager.Instance.TryGetConfig<PlayerConfig>(PlayerConfigId, out PlayerConfig config))
            {
                Debug.LogError($"[PlayerController] 找不到配置 {PlayerConfigId}，主角数值未初始化，已停用");
                enabled = false;
                return;
            }

            _playerStats.Init(config);
            _playerCombat.Init(_playerStats, _playerView, _playerLiquids);
            _playerLiquids.Init(_playerStats);
            _playerInteractor.Init(_playerLiquids);

            // 形态动画跟着主槽液体走：吸取进主槽、Tab 切换都要换
            _playerLiquids.OnChangeLiquid += RefreshForm;
            _playerLiquids.OnGainLiquid += RefreshForm;
            RefreshForm();
        }

        /// <summary>主槽液体变了就换形态动画</summary>
        private void RefreshForm()
        {
            LiquidBase main = _playerLiquids?.CurrentLiquid;
            _playerView.ApplyForm(main != null ? main.Config.Type : (LiquidType?)null);
        }
        private void Update()
        {
            // 数值组件是纯 C# 类，死亡只体现在 IsDead 上。
            // 这里上报一次，由 GameplayRoot 切到 GameOver（重试 / 回菜单都在那边处理）。
            if (_playerStats != null && _playerStats.IsDead)
            {
                if (!_deathReported)
                {
                    _deathReported = true;
                    if (_rb != null) _rb.velocity = Vector2.zero;

                    // 死亡表现：动画 + 停止移动输入
                    _playerView?.PlayDeath();
                    _playerView?.SetMoving(false);

                    Global.Get<GameplayRoot>()?.NotifyPlayerDead();
                }
                return;
            }

            // 保鲜膜：按间隔补护盾层
            _playerStats?.TickShield(Time.deltaTime);

            _moveValue = _inputProvider.PlayerActions.Move.ReadValue<Vector2>();

            // 脱战回血：站着不动、且不在战斗/结算里才慢慢回
            var mode = Global.Get<GameplayRoot>()?.CurrentMode ?? GamePlay.Session.GameplayMode.FreeRoam;
            bool idleAndSafe = _moveValue == Vector2.zero
                               && mode != GamePlay.Session.GameplayMode.Battle
                               && mode != GamePlay.Session.GameplayMode.GameOver;
            _playerStats?.TickRegen(Time.deltaTime, idleAndSafe);

            //移动动画
            if (_moveValue == Vector2.zero)
            {
                _playerView.SetMoving(false);
            }
            else
            {
                _playerView.SetMoving(true);
                _playerView.SetFacing(_moveValue.x);
            }

            // 切换液体CD Tick
            _playerLiquids.TickChangeLiquid(Time.deltaTime);

            // 喷射与喷射冷却
            if (_sprintRemain > 0f) _sprintRemain = Mathf.Max(0f, _sprintRemain - Time.deltaTime);
            if (_sprintCdRemain > 0f) _sprintCdRemain = Mathf.Max(0f, _sprintCdRemain - Time.deltaTime);

            // 脚步 / 喷射的循环音（状态变了才动音源，避免每帧重播）
            UpdateFootstepAudio(_moveValue != Vector2.zero);
        }

        /// <summary>
        /// 移动循环音：站着无声、走路循环脚步声、喷射期间换成喷射音。
        /// 只在状态切换时调用音源，音频本身是 loop 的，会一直循环播放。
        /// </summary>
        private void UpdateFootstepAudio(bool moving)
        {
            var audio = Global.Get<Framework.SubSystems.AudioManager>();
            if (audio == null) return;

            FootstepState want = _sprintRemain > 0f
                ? FootstepState.Spray
                : (moving ? FootstepState.Move : FootstepState.None);

            if (want == _footstepState) return;      // 状态没变，保持当前循环，不要重启
            _footstepState = want;

            switch (want)
            {
                case FootstepState.Move:
                    audio.StopLoop("SFX_Player_Spray");
                    audio.PlayLoop("SFX_Player_Move");
                    break;

                case FootstepState.Spray:
                    audio.StopLoop("SFX_Player_Move");
                    audio.PlayLoop("SFX_Player_Spray");
                    break;

                default:
                    audio.StopLoop("SFX_Player_Move");
                    audio.StopLoop("SFX_Player_Spray");
                    break;
            }
        }
        private void FixedUpdate()
        {
            // 死了就别再动（死亡/重试的停顿期间）
            if (_playerStats == null || _playerStats.IsDead)
            {
                if (_rb != null) _rb.velocity = Vector2.zero;
                return;
            }

            //移动逻辑。喷射期间换成喷射速度（点按型，按一下冲 SprintDuration 秒）
            float speed = _sprintRemain > 0f ? _playerStats.SprintSpeed : _playerStats.MoveSpeed;
            _rb.velocity = _moveValue * speed * (1f + _playerLiquids.MoveSpeedBonus);
        }
        private void OnEnable()
        {
            _inputProvider.PlayerActions.PrimaryAttack.performed += OnPrimaryAttack;
            _inputProvider.PlayerActions.Skill.performed += OnSkill;
            _inputProvider.PlayerActions.SwitchModule.performed += OnSwitchModule;
            _inputProvider.PlayerActions.SwitchLiquid.performed += OnSwitchLiquid;
            _inputProvider.PlayerActions.Sprint.performed += OnSprint;
            _inputProvider.PlayerActions.Pour.performed += OnPour;
            _inputProvider.PlayerActions.Interact.performed += OnInteract;
        }
        private void OnDisable()
        {
            _inputProvider.PlayerActions.PrimaryAttack.performed -= OnPrimaryAttack;
            _inputProvider.PlayerActions.Skill.performed -= OnSkill;
            _inputProvider.PlayerActions.SwitchModule.performed -= OnSwitchModule;
            _inputProvider.PlayerActions.SwitchLiquid.performed -= OnSwitchLiquid;
            _inputProvider.PlayerActions.Sprint.performed -= OnSprint;
            _inputProvider.PlayerActions.Pour.performed -= OnPour;
            _inputProvider.PlayerActions.Interact.performed -= OnInteract;
        }
        #endregion

        #region 输入操作与转发
        private void OnPrimaryAttack(InputAction.CallbackContext ctx)
        {
            //普通攻击逻辑
            _playerCombat.PrimaryAttack();
        }
        private void OnSkill(InputAction.CallbackContext ctx)
        {
            //技能逻辑
            _playerCombat.UseSkill();
        }
        private void OnSprint(InputAction.CallbackContext context)
        {
            // 点按型喷射：按下就冲，冷却从按下这一刻开始算。冷却中或正在喷射时忽略
            if (!SprintReady) return;

            _sprintRemain = _playerStats.SprintDuration;
            _sprintCdRemain = _playerStats.SprintCd;

            // 喷射音由 UpdateFootstepAudio 统一处理（循环播，冲完自动切回脚步/静音）
            UpdateFootstepAudio(true);
        }
        private void OnSwitchModule(InputAction.CallbackContext ctx)
        {
            _playerCombat.SwitchModule();
        }

        private void OnSwitchLiquid(InputAction.CallbackContext ctx)
        {
            // 只有真的切成功了才出声音：冷却中 / 只有一个槽位时静默
            if (!_playerLiquids.CanChangeLiquid) return;

            _playerLiquids.ChangeLiquid();
            Global.Get<Framework.SubSystems.AudioManager>()?.PlaySfx("SFX_Player_SwitchLiquid");
        }

        private void OnPour(InputAction.CallbackContext context)
        {
            _playerLiquids.PourOutLiquid();
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            _playerInteractor.TryInteract();
        }
        #endregion
    }

}