using System.Collections.Generic;
using UnityEngine;

namespace Framework.SubSystems
{
    /// <summary>
    /// 全局音频管理器（原生 AudioSource 实现，不依赖 Wwise）。
    /// BGM 走一条常驻的循环音源，音效走 PlayOneShot。
    /// </summary>
    public class AudioManager : SubSystemBase
    {
        private readonly Dictionary<string, AudioEntry> _entries = new();

        private AudioConfig _config;
        private Transform _container;
        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private AudioSource _ambientSource;
        private AudioSource _loopSource;

        private string _currentBgmKey;
        private string _currentAmbientKey;
        private string _currentLoopKey;

        #region 属性
        public override int Priority => (int)SubSystemPriority.AudioManager;

        /// <summary>当前正在播的 BGM 键，没有则为空</summary>
        public string CurrentBgmKey => _currentBgmKey;

        /// <summary>当前正在播的环境音键，没有则为空</summary>
        public string CurrentAmbienceKey => _currentAmbientKey;
        #endregion

        #region 播放

        /// <summary>
        /// 播放一条音频。配置里 loop = true 的当作 BGM（会顶掉上一首），否则当作一次性音效。
        /// </summary>
        public void Play(string key, float volumeScale = 1f)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (!_entries.TryGetValue(key, out AudioEntry entry) || entry.clip == null)
            {
                Debug.LogWarning($"[AudioManager] 音频未配置或丢失: {key}");
                return;
            }

            if (entry.loop) PlayBGM(key, volumeScale);
            else PlaySfx(entry, volumeScale);
        }

        /// <summary>播一次性音效（可叠放）</summary>
        public void PlaySfx(string key, float volumeScale = 1f)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!_entries.TryGetValue(key, out AudioEntry entry) || entry.clip == null)
            {
                Debug.LogWarning($"[AudioManager] 音效未配置或丢失: {key}");
                return;
            }

            PlaySfx(entry, volumeScale);
        }

        private void PlaySfx(AudioEntry entry, float volumeScale)
        {
            if (_sfxSource == null) return;

            float v = Mathf.Clamp01(entry.volume <= 0f ? 1f : entry.volume) * volumeScale;
            _sfxSource.PlayOneShot(entry.clip, v);
        }

        /// <summary>切 BGM。同一首重复调用不会重播。</summary>
        public void PlayBGM(string key, float volumeScale = 1f)
        {
            if (_bgmSource == null) return;
            if (_currentBgmKey == key && _bgmSource.isPlaying) return;

            if (!_entries.TryGetValue(key, out AudioEntry entry) || entry.clip == null)
            {
                Debug.LogWarning($"[AudioManager] BGM 未配置或丢失: {key}");
                return;
            }

            _currentBgmKey = key;
            _bgmSource.clip = entry.clip;
            _bgmSource.loop = true;
            _bgmSource.volume = BgmVolume(entry, volumeScale);
            _bgmSource.Play();

            Debug.Log($"[AudioManager] 播放 BGM: {key}（音量 {_bgmSource.volume:F2}）");
        }

        /// <summary>停止 BGM</summary>
        public void StopBGM()
        {
            _currentBgmKey = null;
            if (_bgmSource != null) _bgmSource.Stop();
        }

        /// <summary>停止某条音频（只对 BGM 有意义，音效是一次性的）</summary>
        public void Stop(string key)
        {
            if (_currentBgmKey == key) StopBGM();
        }

        /// <summary>切环境音（独立音源，和 BGM 分层叠放）。同一首重复调用不会重播。</summary>
        public void PlayAmbience(string key, float volumeScale = 1f)
        {
            if (_ambientSource == null) return;
            if (_currentAmbientKey == key && _ambientSource.isPlaying) return;

            if (!_entries.TryGetValue(key, out AudioEntry entry) || entry.clip == null)
            {
                Debug.LogWarning($"[AudioManager] 环境音未配置或丢失: {key}");
                return;
            }

            _currentAmbientKey = key;
            _ambientSource.clip = entry.clip;
            _ambientSource.loop = true;
            _ambientSource.volume = BgmVolume(entry, volumeScale);
            _ambientSource.Play();

            Debug.Log($"[AudioManager] 播放环境音: {key}");
        }

        /// <summary>停止环境音</summary>
        public void StopAmbience()
        {
            _currentAmbientKey = null;
            if (_ambientSource != null) _ambientSource.Stop();
        }

        /// <summary>
        /// 播放/替换一条「状态型循环音效」（跑动、喷射这类跟着玩家动作起停的声音）。
        /// 传入的 key 变了就换曲子，相同则继续播。
        /// </summary>
        public void PlayLoop(string key, float volumeScale = 1f)
        {
            if (_loopSource == null) return;
            if (_currentLoopKey == key && _loopSource.isPlaying) return;

            if (!_entries.TryGetValue(key, out AudioEntry entry) || entry.clip == null)
            {
                Debug.LogWarning($"[AudioManager] 循环音效未配置或丢失: {key}");
                return;
            }

            _currentLoopKey = key;
            _loopSource.clip = entry.clip;
            _loopSource.loop = true;
            _loopSource.volume = SfxVolume(entry, volumeScale);
            _loopSource.Play();
        }

        /// <summary>停止循环音效。传 key 时只有当前正是它才会停。</summary>
        public void StopLoop(string key = null)
        {
            if (key != null && _currentLoopKey != key) return;

            _currentLoopKey = null;
            if (_loopSource != null) _loopSource.Stop();
        }

        #endregion

        #region 音量

        private float BgmVolume(AudioEntry entry, float volumeScale)
        {
            float cfg = _config != null ? _config.MasterVolume * _config.MusicVolume : 1f;
            float local = entry.volume <= 0f ? 1f : entry.volume;
            return Mathf.Clamp01(cfg * local * volumeScale);
        }

        private void ApplySfxVolume()
        {
            if (_sfxSource == null) return;
            float cfg = _config != null ? _config.MasterVolume * _config.SfxVolume : 1f;
            _sfxSource.volume = Mathf.Clamp01(cfg);
        }

        private float SfxVolume(AudioEntry entry, float volumeScale)
        {
            float cfg = _config != null ? _config.MasterVolume * _config.SfxVolume : 1f;
            float local = entry.volume <= 0f ? 1f : entry.volume;
            return Mathf.Clamp01(cfg * local * volumeScale);
        }

        /// <summary>总音量 0~1</summary>
        public void ChangeMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }

        /// <summary>音乐音量 0~1（乘在配置的基础音量上）</summary>
        public void ChangeMusicVolume(float volume)
        {
            if (_bgmSource != null) _bgmSource.volume = Mathf.Clamp01(volume);
            if (_ambientSource != null) _ambientSource.volume = Mathf.Clamp01(volume * 0.6f);
        }

        /// <summary>音效音量 0~1</summary>
        public void ChangeSfxVolume(float volume)
        {
            if (_sfxSource != null) _sfxSource.volume = Mathf.Clamp01(volume);
            if (_loopSource != null) _loopSource.volume = Mathf.Clamp01(volume * 0.8f);
        }

        #endregion

        #region 生命周期

        public override void Init()
        {
            _config = AudioConfig.Instance;
            if (_config == null)
            {
                Debug.LogError("[AudioManager] 缺少 Resources/AudioConfig.asset");
                return;
            }

            _entries.Clear();
            foreach (AudioEntry entry in _config.Entries)
            {
                if (string.IsNullOrEmpty(entry.key) || entry.clip == null) continue;
                _entries[entry.key] = entry;
            }

            var go = new GameObject("[AudioManager]");
            Object.DontDestroyOnLoad(go);
            _container = go.transform;

            // 本工程原来是 Wwise，场景里没有任何 AudioListener。
            // 只有 AudioSource 没有 Listener 等于完全静音，所以这里补一个。
            if (Object.FindObjectOfType<AudioListener>() == null)
                go.AddComponent<AudioListener>();

            _bgmSource = go.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.volume = _config.MasterVolume * _config.MusicVolume;

            _sfxSource = go.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;
            ApplySfxVolume();

            // 环境音（和 BGM 分层）
            _ambientSource = go.AddComponent<AudioSource>();
            _ambientSource.playOnAwake = false;
            _ambientSource.loop = true;
            _ambientSource.spatialBlend = 0f;
            _ambientSource.volume = _config.MasterVolume * _config.MusicVolume * 0.6f;

            // 状态型循环音效（跑动 / 喷射）
            _loopSource = go.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
            _loopSource.loop = true;
            _loopSource.spatialBlend = 0f;
            _loopSource.volume = _config.MasterVolume * _config.SfxVolume * 0.8f;

            Debug.Log($"[AudioManager] 已加载 {_entries.Count} 条音频");
        }

        public override void Destroy()
        {
            if (_container != null) Object.Destroy(_container.gameObject);

            _entries.Clear();
            _container = null;
            _bgmSource = null;
            _sfxSource = null;
            _ambientSource = null;
            _loopSource = null;
            _currentBgmKey = null;
            _currentAmbientKey = null;
            _currentLoopKey = null;
        }

        #endregion
    }
}
