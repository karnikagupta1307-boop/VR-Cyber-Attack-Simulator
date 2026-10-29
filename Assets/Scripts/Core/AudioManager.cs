using UnityEngine;

namespace CyberDefenseVR
{
    public enum Sfx { Click, AttackStarted, ThreatDetected, DefenseActivated, AttackBlocked, TrainingCompleted, AttackSucceeded }

    /// <summary>Persistent audio. All sounds are synthesised in code (soft, short tones) so no audio files are needed.</summary>
    public class AudioManager : MonoBehaviour
    {
        static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<AudioManager>();
                    if (instance == null) new GameObject("AudioManager").AddComponent<AudioManager>();
                }
                return instance;
            }
        }

        AudioSource sfxSource, musicSource;
        AudioClip[] clips;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false; sfxSource.spatialBlend = 0f;
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false; musicSource.loop = true; musicSource.spatialBlend = 0f;

            clips = new AudioClip[7];
            clips[(int)Sfx.Click] = Tone("click", new[] { 880f }, 0.06f, 0.25f);
            clips[(int)Sfx.AttackStarted] = Tone("attack", new[] { 220f, 262f, 330f }, 0.12f, 0.25f);
            clips[(int)Sfx.ThreatDetected] = Tone("threat", new[] { 660f, 495f, 660f, 495f }, 0.1f, 0.25f);
            clips[(int)Sfx.DefenseActivated] = Tone("defense", new[] { 392f, 523f, 659f }, 0.13f, 0.25f);
            clips[(int)Sfx.AttackBlocked] = Tone("blocked", new[] { 523f, 784f }, 0.18f, 0.25f);
            clips[(int)Sfx.TrainingCompleted] = Tone("complete", new[] { 523f, 659f, 784f, 1047f }, 0.16f, 0.25f);
            clips[(int)Sfx.AttackSucceeded] = Tone("fail", new[] { 196f, 147f }, 0.3f, 0.25f);

            musicSource.clip = MakePad();
            ApplyVolumes();
            musicSource.Play();
            SettingsManager.Changed += ApplyVolumes;
        }

        void OnDestroy()
        {
            if (instance == this) { SettingsManager.Changed -= ApplyVolumes; instance = null; }
        }

        void ApplyVolumes()
        {
            float master = SettingsManager.MasterVolume;
            sfxSource.volume = master * SettingsManager.SfxVolume;
            musicSource.volume = master * SettingsManager.MusicVolume * 0.35f;
        }

        public void Play(Sfx sfx)
        {
            if (clips == null) return;
            sfxSource.PlayOneShot(clips[(int)sfx]);
        }

        static AudioClip Tone(string name, float[] freqs, float noteSeconds, float amp)
        {
            const int sr = 44100;
            int per = (int)(sr * noteSeconds);
            var data = new float[per * freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)sr;
                    float env = Mathf.Clamp01(i / (sr * 0.01f)) * Mathf.Exp(-3f * i / per);
                    data[k * per + i] = Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * env * amp;
                }
            var c = AudioClip.Create(name, data.Length, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip MakePad()
        {
            const int sr = 22050; const float len = 8f;
            int n = (int)(sr * len);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float lfo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 82.5f * t)) * 0.2f * lfo;
            }
            var c = AudioClip.Create("ambient", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
