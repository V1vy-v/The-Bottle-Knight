using System;
using System.Collections.Generic;
using UnityEngine;
using GamePlay.Enemy;
using GamePlay.Interaction;

namespace GamePlay.Player
{
    /// <summary>主角交互检测：找出身边最优的可交互物，供 F 键调用</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float _radius = 1.5f;
        [SerializeField] private LayerMask _interactMask;
        [SerializeField] private float _scanInterval = 0.08f;
        private PlayerLiquids _liquids;

        private readonly Collider2D[] _buffer = new Collider2D[16];

        /// <summary>本次扫描命中的敌人（用来跟踪进入/离开，避免每帧重复报目标）</summary>
        private readonly HashSet<EnemyController> _present = new();
        private readonly HashSet<EnemyController> _talked = new();

        private IInteractable _current;
        private float _elapsed;
        private int _bufferCount;

        /// <summary>候选变化时触发，null 表示当前没有可交互目标</summary>
        public event Action<IInteractable> OnCandidateChanged;

        public IInteractable Current => _current;
        private string _prompt;

        /// <summary>当前提示文案，null / 空表示不显示</summary>
        public string CurrentPrompt => _prompt;

        /// <summary>提示文案变化时触发</summary>
        public event Action<string> OnPromptChanged;

        #region 生命周期
        public void Init(PlayerLiquids liquids)
        {
            _liquids = liquids;
        }
        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed < _scanInterval) return;
            _elapsed = 0f;

            Scan();
        }

        private void OnDisable()
        {
            SetCandidate(null);
            RefreshPrompt(null);
        }
        #endregion

        #region 检测
        private void Scan()
        {
            int n = Physics2D.OverlapCircleNonAlloc(transform.position, _radius, _buffer, _interactMask);
            _bufferCount = n;

            IInteractable best = null;
            int bestPriority = int.MinValue;
            float bestSqr = float.MaxValue;
            Vector2 self = transform.position;

            for (int i = 0; i < n; i++)
            {
                // 对话区不参与吸取判定：否则大号对话区会把吸取范围一起放大
                if (_buffer[i].GetComponent<EnemyTalkZone>() != null) continue;

                // 脚本可能和碰撞体不在同一物体上，所以向父级找
                var it = _buffer[i].GetComponentInParent<IInteractable>();
                if (it == null || !it.CanInteract) continue;
                if (it.PromptAnchor == null) continue;

                float sqr = ((Vector2)it.PromptAnchor.position - self).sqrMagnitude;
                if (it.Priority > bestPriority || (it.Priority == bestPriority && sqr < bestSqr))
                {
                    best = it; bestPriority = it.Priority; bestSqr = sqr;
                }
            }

            TickTalk();

            SetCandidate(best);
            RefreshPrompt(best);
        }

        /// <summary>
        /// 剧情敌人的对话触发。复用上面这次 OverlapCircle 的结果：
        /// 敌人身上的 InteractPoint 就在 Interactable 层，所以只要它在范围内就能扫到。
        /// 用本次/上次的集合做进入判定，玩家走开后会自动解除，不需要额外的退出回调。
        /// </summary>
        private void TickTalk()
        {
            _present.Clear();
            for (int i = 0; i < _bufferCount; i++)
            {
                // 只认带 EnemyTalkZone 标记的那个碰撞体（大号对话区）
                if (_buffer[i].GetComponent<EnemyTalkZone>() == null) continue;

                var e = _buffer[i].GetComponentInParent<EnemyController>();
                if (e != null) _present.Add(e);
            }

            foreach (var e in _present)
            {
                if (!_talked.Contains(e) && e.NeedsTalk)
                {
                    _talked.Add(e);
                    e.NotifyTalked();
                }
            }

            // 已经走开的，允许下次再触发
            _talked.RemoveWhere(e => e == null || !_present.Contains(e));
        }

        private void SetCandidate(IInteractable it)
        {
            if (ReferenceEquals(_current, it)) return;

            _current = it;
            Debug.Log($"[交互] 候选 = {(it == null ? "无" : it.GetType().Name)}");   // ← 临时
            OnCandidateChanged?.Invoke(_current);
        }

        private void RefreshPrompt(IInteractable it)
        {
            string text = (_liquids == null || it == null) ? null : it.GetPromptText(_liquids);
            if (text == _prompt) return;

            _prompt = text;
            Debug.Log($"[交互] 提示 = {(_prompt ?? "（不显示）")}");   // ← 临时，验完删
            OnPromptChanged?.Invoke(_prompt);
        }
        #endregion

        #region 对外
        /// <summary>由 PlayerController 在 F 键按下时调用</summary>
        public bool TryInteract()
        {
            if (_liquids == null) return false;
            // 接口引用不触发 Unity 的 == 重载，销毁后要显式判一次
            if (_current is not UnityEngine.Object obj || obj == null) return false;
            if (!_current.CanInteract) return false;

            _current.Interact(gameObject, _liquids);

            Scan();     // 交互后立刻重扫：状态可能已变（如吸取后备用槽满）
            return true;
        }
        #endregion
    }
}