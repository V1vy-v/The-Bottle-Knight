using Framework;
using Framework.SubSystems;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 设置面板：目前只有音量（音乐 / 音效）+ 两个静音开关 + 关闭。
    /// 注意 UIController 的 Awake 顺序是 AddListener() 先于 Init()，
    /// 所以控件引用在 AddListener() 里解析，不要在 Init() 里缓存后再用。
    /// </summary>
    public partial class SettingPanel
    {
        private Slider _musicSliderRef;
        private Slider _soundSliderRef;
        private Toggle _musicToggleRef;
        private Toggle _soundToggleRef;
        private Button _closeRef;

        private float _musicBeforeMute = 0.5f;
        private float _soundBeforeMute = 0.8f;

        #region 生命周期

        protected override void Init()
        {
            // 控件引用在 AddListener() 里解析，这里只留空
        }

        protected override void AddListener()
        {
            _musicSliderRef = AsSlider(_musicSlider);
            _soundSliderRef = AsSlider(_soundSlider);
            _musicToggleRef = AsToggle(_musicToggle);
            _soundToggleRef = AsToggle(_soundToggle);
            _closeRef = AsButton(_closeBtn);

            if (_musicSliderRef != null) _musicSliderRef.onValueChanged.AddListener(OnMusicChanged);
            if (_soundSliderRef != null) _soundSliderRef.onValueChanged.AddListener(OnSoundChanged);
            if (_musicToggleRef != null) _musicToggleRef.onValueChanged.AddListener(OnMusicToggle);
            if (_soundToggleRef != null) _soundToggleRef.onValueChanged.AddListener(OnSoundToggle);
            if (_closeRef != null) _closeRef.onClick.AddListener(OnClose);
        }

        protected override void RemoveListener()
        {
            if (_musicSliderRef != null) _musicSliderRef.onValueChanged.RemoveListener(OnMusicChanged);
            if (_soundSliderRef != null) _soundSliderRef.onValueChanged.RemoveListener(OnSoundChanged);
            if (_musicToggleRef != null) _musicToggleRef.onValueChanged.RemoveListener(OnMusicToggle);
            if (_soundToggleRef != null) _soundToggleRef.onValueChanged.RemoveListener(OnSoundToggle);
            if (_closeRef != null) _closeRef.onClick.RemoveListener(OnClose);

            base.RemoveListener();
        }

        protected override void UpdateView()
        {
            AudioConfig config = AudioConfig.Instance;

            if (_musicSliderRef != null)
                _musicSliderRef.SetValueWithoutNotify(config != null ? config.MusicVolume : 0.5f);

            if (_soundSliderRef != null)
                _soundSliderRef.SetValueWithoutNotify(config != null ? config.SfxVolume : 0.8f);

            if (_musicToggleRef != null)
                _musicToggleRef.SetIsOnWithoutNotify(true);

            if (_soundToggleRef != null)
                _soundToggleRef.SetIsOnWithoutNotify(true);
        }

        #endregion

        #region 回调

        private void OnMusicChanged(float value)
        {
            _musicBeforeMute = value;
            Global.Get<AudioManager>()?.ChangeMusicVolume(value);
        }

        private void OnSoundChanged(float value)
        {
            _soundBeforeMute = value;
            Global.Get<AudioManager>()?.ChangeSfxVolume(value);
        }

        private void OnMusicToggle(bool on)
        {
            if (_musicSliderRef != null) _musicSliderRef.interactable = on;

            // 关掉 = 静音；打开 = 恢复静音前的音量
            float value = on ? _musicBeforeMute : 0f;
            Global.Get<AudioManager>()?.ChangeMusicVolume(value);
        }

        private void OnSoundToggle(bool on)
        {
            if (_soundSliderRef != null) _soundSliderRef.interactable = on;

            float value = on ? _soundBeforeMute : 0f;
            Global.Get<AudioManager>()?.ChangeSfxVolume(value);
        }

        private void OnClose()
        {
            Global.Get<AudioManager>()?.PlaySfx("UI_close");
            Global.HideUI("SettingPanel");
        }

        #endregion

        #region 取组件

        private static Slider AsSlider(RectTransform rt) => rt != null ? rt.GetComponent<Slider>() : null;

        private static Toggle AsToggle(RectTransform rt) => rt != null ? rt.GetComponent<Toggle>() : null;

        private static Button AsButton(RectTransform rt) => rt != null ? rt.GetComponent<Button>() : null;

        #endregion
    }
}
