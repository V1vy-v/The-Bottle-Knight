using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.SubSystems
{
    /// <summary>
    /// 一条音频配置。key 是代码里用的键，clip 是实际音频。
    /// </summary>
    [Serializable]
    public struct AudioEntry
    {
        [Tooltip("代码里用的键，例如 BGM_Kitchen / UI_Open")]
        public string key;

        public AudioClip clip;

        [Tooltip("勾上则循环播放（BGM 用），否则当作一次性音效")]
        public bool loop;

        [Range(0f, 1f)]
        public float volume;
    }

    /// <summary>
    /// 全局音频表。用原生 AudioClip，不走 Wwise。
    /// 音频直接引用在这里，会随 Config 组一起加载。
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "GameCore/AudioConfig")]
    public class AudioConfig : ScriptableObjectSingleton<AudioConfig>
    {
        [Header("音频表")]
        [SerializeField] private List<AudioEntry> entries = new();

        [Header("总音量")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;

        public IReadOnlyList<AudioEntry> Entries => entries;
        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;
    }
}
