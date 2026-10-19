using Framework;
using GamePlay.Configs;
using GamePlay.Player;
using GamePlay.Enemy;
using System;
using System.Collections;
using UnityEngine;

namespace GamePlay.Interaction
{
    /// <summary>
    /// 可被主角用 F 吸取的敌人。
    /// 挂在敌人本体上（与 EnemyController 同物体）：可吸取配置在 EnemyConfig 上，
    /// 「是否已被击败」的状态在同物体的 EnemyController 上，拆成两个物体迟早不同步。
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class AbsorbSource : MonoBehaviour, IInteractable
    {
        /// <summary>覆盖 EnemyConfig 里的 AbsorbLiquid；留空则用配置值</summary>
        [SerializeField] private LiquidConfig _liquidOverride;

        /// <summary>吸取表现时长，播完才消失。表现是即时的就填 0</summary>
        [SerializeField] private float _absorbDuration = 0.6f;

        private EnemyController _enemy;
        private bool _absorbed;

        /// <summary>
        /// 吸取完成（在消失之前触发）。监听方据此开门 / 解锁出口；
        /// 不要持有这个引用，回调之后本物体会被释放。
        /// </summary>
        public event Action<AbsorbSource> OnAbsorbed;

        /// <summary>实际产出的液体</summary>
        public LiquidConfig Liquid =>
            _liquidOverride != null ? _liquidOverride : _enemy?.config?.AbsorbLiquid;

        private void Awake() => _enemy = GetComponent<EnemyController>();

        #region IInteractable

        // 约定：剧情 100 / 商店 50 / 吸取 10。三者位置重叠时按这个顺序取
        public int Priority => 10;

        /// <summary>约等于原来的 1.5 查询半径 + 吸点碰撞体半径 3</summary>
        public float InteractRadius => 4.5f;

        public float PromptRadius => InteractRadius;      // 提示和交互同距离

        public Transform PromptAnchor => transform;

        public bool CanInteract =>
            !_absorbed
            && _enemy != null
            && _enemy.config != null
            && _enemy.config.CanAbsorb
            && _enemy.IsDefeated
            && Liquid != null;

        /// <summary>还留在场上等人吸取（战斗结束时据此决定是否暂留尸体）</summary>
        public bool IsAwaitingAbsorb => CanInteract;

        public string GetPromptText(PlayerLiquids liquids)
        {
            // 备用槽非空就吸不进去，先把原因说清楚，别让玩家以为按键坏了
            if (liquids == null || liquids.StandbyLiquid != null) return "备用槽已满";
            return "按 F 吸取";
        }

        public void Interact(GameObject player, PlayerLiquids liquids)
        {
            if (!CanInteract) return;
            if (liquids == null) return;

            var cfg = Liquid;
            if (cfg == null) return;

            if (!liquids.GetLiquid(cfg))   // 二次确认：扫描间隔内玩家可能刚换了液体
                return;

            _absorbed = true;
            Global.Get<GameplayRoot>()?.ReportGoalReached("absorb");
            StartCoroutine(AbsorbRoutine());
        }

        #endregion

        private IEnumerator AbsorbRoutine()
        {
            PlayAbsorbPresentation();

            if (_absorbDuration > 0f)
                yield return new WaitForSeconds(_absorbDuration);

            OnAbsorbed?.Invoke(this);      // 剧情侧在这里开门 / 解锁出口

            // 敌人可能是门面生成的（要还池），也可能是场景里直接摆的剧情敌人（要 Destroy）
            var enemy = GetComponent<EnemyController>();
            if (enemy != null) enemy.ReleaseSelf();
            else Destroy(gameObject);
        }

        /// <summary>
        /// 吸取表现：动画 + 特效 + 音效。
        /// TODO 美术资源未就绪（吸取特效在美术需求清单里是缺失项），先留空。
        /// </summary>
        private void PlayAbsorbPresentation()
        {
            // _enemy.PlayAnimation("Absorb");
            // Global.Instantiate("VFX_Absorb", ...);
        }
    }
}