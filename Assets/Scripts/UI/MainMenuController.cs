using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CyberDefenseVR
{
    /// <summary>Main menu + settings panel. Uses +/- buttons instead of sliders so everything works with the VR pointer.</summary>
    public class MainMenuController : MonoBehaviour
    {
        public Canvas canvas;
        public GameObject mainPanel, settingsPanel;
        public Button startButton, desktopButton, settingsButton, exitButton, backButton;
        public Button masterMinus, masterPlus, sfxMinus, sfxPlus, musicMinus, musicPlus, handButton;
        public TMP_Text masterValue, sfxValue, musicValue, handValue;

        void OnEnable()
        {
            startButton.onClick.AddListener(OnStart);
            desktopButton.onClick.AddListener(OnDesktop);
            settingsButton.onClick.AddListener(OnSettings);
            exitButton.onClick.AddListener(OnExit);
            backButton.onClick.AddListener(OnBack);
            masterMinus.onClick.AddListener(MasterDown); masterPlus.onClick.AddListener(MasterUp);
            sfxMinus.onClick.AddListener(SfxDown); sfxPlus.onClick.AddListener(SfxUp);
            musicMinus.onClick.AddListener(MusicDown); musicPlus.onClick.AddListener(MusicUp);
            handButton.onClick.AddListener(ToggleHand);
            SettingsManager.Changed += RefreshSettings;
        }

        void OnDisable()
        {
            startButton.onClick.RemoveListener(OnStart);
            desktopButton.onClick.RemoveListener(OnDesktop);
            settingsButton.onClick.RemoveListener(OnSettings);
            exitButton.onClick.RemoveListener(OnExit);
            backButton.onClick.RemoveListener(OnBack);
            masterMinus.onClick.RemoveListener(MasterDown); masterPlus.onClick.RemoveListener(MasterUp);
            sfxMinus.onClick.RemoveListener(SfxDown); sfxPlus.onClick.RemoveListener(SfxUp);
            musicMinus.onClick.RemoveListener(MusicDown); musicPlus.onClick.RemoveListener(MusicUp);
            handButton.onClick.RemoveListener(ToggleHand);
            SettingsManager.Changed -= RefreshSettings;
        }

        void Start()
        {
            var audioManager = AudioManager.Instance; // creates the persistent AudioManager (ambient music)
            if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
#if UNITY_ANDROID && !UNITY_EDITOR
            desktopButton.gameObject.SetActive(false); // desktop test is for the Editor / PC builds
#endif
            ShowSettings(false);
            RefreshSettings();
        }

        void ShowSettings(bool on) { mainPanel.SetActive(!on); settingsPanel.SetActive(on); }
        static void Click() { AudioManager.Instance.Play(Sfx.Click); }

        void OnStart() { Click(); SceneTransitionManager.Load(SceneTransitionManager.Training); }
        void OnDesktop() { Click(); SceneTransitionManager.Load(SceneTransitionManager.DesktopTest); }
        void OnSettings() { Click(); ShowSettings(true); }
        void OnBack() { Click(); ShowSettings(false); }
        void OnExit()
        {
            Click();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void MasterUp() { Click(); SettingsManager.MasterVolume += 0.1f; }
        void MasterDown() { Click(); SettingsManager.MasterVolume -= 0.1f; }
        void SfxUp() { Click(); SettingsManager.SfxVolume += 0.1f; }
        void SfxDown() { Click(); SettingsManager.SfxVolume -= 0.1f; }
        void MusicUp() { Click(); SettingsManager.MusicVolume += 0.1f; }
        void MusicDown() { Click(); SettingsManager.MusicVolume -= 0.1f; }
        void ToggleHand()
        {
            Click();
            SettingsManager.DominantHand = SettingsManager.DominantHand == Hand.Right ? Hand.Left : Hand.Right;
        }

        void RefreshSettings()
        {
            masterValue.text = Mathf.RoundToInt(SettingsManager.MasterVolume * 100f) + "%";
            sfxValue.text = Mathf.RoundToInt(SettingsManager.SfxVolume * 100f) + "%";
            musicValue.text = Mathf.RoundToInt(SettingsManager.MusicVolume * 100f) + "%";
            handValue.text = SettingsManager.DominantHand.ToString().ToUpper();
        }
    }
}
