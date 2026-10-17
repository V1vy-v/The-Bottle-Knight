using System;
using System.Collections;
using Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay
{
    /// <summary>
    /// 全屏黑幕转场。场景里放一个 Canvas（SortingOrder 高于 UIManager）+ 一张全屏纯黑 Image。
    /// </summary>
    public class ScreenFader : MonoSingleton<ScreenFader>
    {
        [SerializeField] private Image _black;
        [SerializeField] private float _outDuration = 0.35f;
        [SerializeField] private float _holdDuration = 0.25f;
        [SerializeField] private float _inDuration = 0.35f;

        public bool IsPlaying { get; private set; }

        protected override void Init()
        {
            if (_black == null)
            {
                Debug.LogError("[ScreenFader] 没有指定黑幕 Image，转场不会显示");
                return;
            }

            // 不要 SetActive(false)：本脚本可能就和 _black 挂在同一个物体上，
            // 关掉它等于关掉自己，之后 StartCoroutine 会报 "game object is inactive"。
            // 只用 alpha 控制显隐（Image 记得取消 Raycast Target）。
            SetAlpha(0f);
        }


        /// <summary>变黑 → 执行 midAction → 变亮</summary>
        public void Play(Action midAction)
        {
            if (_black == null) { midAction?.Invoke(); return; }   // 没配就直通，不影响流程
            if (IsPlaying) return;                                  // 正在转场，忽略重复请求

            StartCoroutine(Routine(midAction));
        }

        private IEnumerator Routine(Action midAction)
        {
            IsPlaying = true;
            SetAlpha(0f);

            yield return Fade(0f, 1f, _outDuration);
            midAction?.Invoke();
            yield return new WaitForSecondsRealtime(_holdDuration);
            yield return Fade(1f, 0f, _inDuration);

            SetAlpha(0f);
            IsPlaying = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f) { SetAlpha(to); yield break; }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;          // 用 unscaled，暂停时也能播
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            if (_black == null) return;
            Color c = _black.color;
            c.a = a;
            _black.color = c;
        }
    }
}