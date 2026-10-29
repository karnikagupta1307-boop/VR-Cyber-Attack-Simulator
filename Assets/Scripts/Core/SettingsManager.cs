using System;
using UnityEngine;

namespace CyberDefenseVR
{
    public enum Hand { Left, Right }

    /// <summary>Persistent user settings (PlayerPrefs).</summary>
    public static class SettingsManager
    {
        const string MasterKey = "cdvr.master", SfxKey = "cdvr.sfx", MusicKey = "cdvr.music", HandKey = "cdvr.hand";
        public static event Action Changed;

        public static float MasterVolume { get { return PlayerPrefs.GetFloat(MasterKey, 0.8f); } set { SetFloat(MasterKey, value); } }
        public static float SfxVolume { get { return PlayerPrefs.GetFloat(SfxKey, 0.8f); } set { SetFloat(SfxKey, value); } }
        public static float MusicVolume { get { return PlayerPrefs.GetFloat(MusicKey, 0.5f); } set { SetFloat(MusicKey, value); } }
        public static Hand DominantHand
        {
            get { return (Hand)PlayerPrefs.GetInt(HandKey, (int)Hand.Right); }
            set { PlayerPrefs.SetInt(HandKey, (int)value); PlayerPrefs.Save(); if (Changed != null) Changed(); }
        }

        static void SetFloat(string key, float v)
        {
            PlayerPrefs.SetFloat(key, Mathf.Round(Mathf.Clamp01(v) * 10f) / 10f);
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }
    }
}
