using Framework;
using GamePlay.Configs;
using GamePlay.Player;
using GamePlay.Enemy;
using GamePlay.Story;
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

        /// <summary>
        /// 强制吸进主槽，无视槽位状态。
        /// 只给剧情强制的第一次吸取用（国窖），普通怪走 GetLiquid 进备用槽。
        /// </summary>
        [SerializeField] private bool _forceToMainSlot;

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

        public bool CanInteract => !_absorbed && NodeAllowsAbsorb() && CanBeAbsorbed;

        /// <summary>
        /// 还留在场上等人吸取（战斗结束时据此决定是否暂留尸体）。
        /// 注意：这里<b>不</b>看节点 —— 战斗刚结束时节点已经推进到「战后对话」，
        /// 若按节点判断会把尸体当场清掉，玩家走到吸取节点时就没东西可吸了。
        /// </summary>
        public bool IsAwaitingAbsorb => !_absorbed && CanBeAbsorbed;

        /// <summary>结构上可被吸取（倒地 + 配置允许 + 有液体）</summary>
        private bool CanBeAbsorbed =>
            _enemy != null
            && _enemy.config != null
            && _enemy.config.CanAbsorb
            && _enemy.IsDefeated
            && Liquid != null;

        /// <summary>
        /// 只有「当前节点就是在等吸取」时才允许按 F。
        /// 否则玩家可以先跑到尸体边把剧情道具吃掉，对话节点再进场时就没东西可吸 —— 流程卡死。
        /// </summary>
        private static bool NodeAllowsAbsorb()
        {
            var root = Global.Get<GameplayRoot>();
            NodeConfig node = root?.Story?.CurrentNode;
            if (node == null) return false;

            if (node.goalId == "absorb") return true;

            // 刚在吸取节点吸掉一份、流程已经推进到下一个漫游节点：
            // 场上剩下的可吸尸体（例如擂台里打死的料酒）仍然允许吸。
            // 不然「先吸可乐 → 立刻推进节点」会把另一具尸体直接废掉。
            return node.mode == GamePlay.Session.GameplayMode.FreeRoam && root.PreviousNodeWasAbsorb;
        }

        public string GetPromptText(PlayerLiquids liquids)
        {
            // 备用槽有液体也会被替换掉，不再有「吸不进去」的情况
            return liquids == null ? "备用槽已满" : "按 F 吸取";
        }

        public void Interact(GameObject player, PlayerLiquids liquids)
        {
            if (!CanInteract) return;
            if (liquids == null) return;

            var cfg = Liquid;
            if (cfg == null) return;

            // 剧情强制（国窖 / 三拳）：直接塞主槽，保证立刻切到对应形态
            // 三拳是 MainPassive 主形态，走备用槽的话外观和被动都不会生效
            bool forceMain = _forceToMainSlot || cfg.Type == GamePlay.Configs.LiquidType.SanQuan;

            bool ok = forceMain
                ? liquids.ForceGetLiquid(cfg, LiquidTankType.Current)
                : liquids.GetLiquid(cfg);      // 二次确认：扫描间隔内玩家可能刚换了液体

            if (!ok) return;

            // 开局即倒地的剧情单位（中三拳）死亡时没掉瓶盖，改在吸取这一下结算
            if (_enemy != null && _enemy.StartedDefeated && _enemy.config != null && _enemy.config.CapDrop > 0)
            {
                var pc = player != null ? player.GetComponent<PlayerController>() : null;
                if (pc != null && pc.PlayerStats != null)
                {
                    pc.PlayerStats.Caps += _enemy.config.CapDrop;
                    Debug.Log($"[瓶盖] 剧情给予 {_enemy.config.CapDrop} 个，共 {pc.PlayerStats.Caps}");
                }
            }

            _absorbed = true;
            if (_enemy != null) _enemy.MarkAbsorbed();     // 剧情已过：战败重试时不再把它复活
            Global.Get<GameplayRoot>()?.ReportGoalReached("absorb");

            // 吸取音：循环播，持续到吸取动作结束
            Framework.Global.Get<Framework.SubSystems.AudioManager>()?.PlayLoop("SFX_Player_Siphon");

            StartCoroutine(AbsorbRoutine());
        }

        #endregion

        private IEnumerator AbsorbRoutine()
        {
            PlayAbsorbPresentation();

            if (_absorbDuration > 0f)
                yield return new WaitForSeconds(_absorbDuration);

            OnAbsorbed?.Invoke(this);      // 剧情侧在这里开门 / 解锁出口

            // 吸取音收尾
            Framework.Global.Get<Framework.SubSystems.AudioManager>()?.StopLoop("SFX_Player_Siphon");

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