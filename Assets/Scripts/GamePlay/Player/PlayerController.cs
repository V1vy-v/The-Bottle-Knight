using Framework;
using GamePlay.Configs;
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

        private Vector2 _moveValue;


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
            // 喷射CD tick

        }
        private void FixedUpdate()
        {
            //移动逻辑
            _rb.velocity = _moveValue * _playerStats.MoveSpeed * (1f + _playerLiquids.MoveSpeedBonus);
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
            //喷射动画
            //喷射逻辑待定
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