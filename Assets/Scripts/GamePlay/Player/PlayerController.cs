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

        // 点按型喷射：_sprintRemain = 本次喷射还剩多久，_sprintCdRemain = 冷却还剩多久
        private float _sprintRemain;
        private float _sprintCdRemain;


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
            _moveValue = _inputProvider.PlayerActions.Move.ReadValue<Vector2>();
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

        }
        private void FixedUpdate()
        {
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

            // TODO 喷射动画 / 音效 / 伤害。PlayerView 目前没有喷射接口，
            //      Animator 里也没有「喷射」状态，加的时候一起补。
        }
        private void OnSwitchModule(InputAction.CallbackContext ctx)
        {
            _playerCombat.SwitchModule();
        }

        private void OnSwitchLiquid(InputAction.CallbackContext ctx)
        {
            _playerLiquids.ChangeLiquid();
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