using TMPro;
using UnityEngine;

namespace CyberDefenseVR
{
    public enum ThreatLevel { Secure, Warning, Alert, Critical, Mitigated }

    /// <summary>Large wall beacon showing overall threat level with text and colour.</summary>
    public class ThreatIndicator : MonoBehaviour
    {
        public Renderer panel;
        public TMP_Text text;
        public ThreatLevel Level { get; private set; }
        MaterialPropertyBlock block;
        Color current;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake() { SetLevel(ThreatLevel.Secure); }

        public void SetLevel(ThreatLevel level)
        {
            if (block == null) block = new MaterialPropertyBlock();
            Level = level;
            string word;
            switch (level)
            {
                case ThreatLevel.Warning: current = new Color(1f, 0.55f, 0.1f); word = "SUSPICIOUS TRAFFIC"; break;
                case ThreatLevel.Alert: current = new Color(1f, 0.2f, 0.25f); word = "THREAT DETECTED"; break;
                case ThreatLevel.Critical: current = new Color(1f, 0.1f, 0.1f); word = "BREACH"; break;
                case ThreatLevel.Mitigated: current = new Color(0.15f, 0.95f, 0.65f); word = "THREAT BLOCKED"; break;
                default: current = new Color(0.1f, 0.8f, 1f); word = "NETWORK SECURE"; break;
            }
            if (text != null) text.text = word;
            Apply(1f);
        }

        void Apply(float k)
        {
            if (panel == null) return;
            block.SetColor(EmissionId, current * (1.2f * k));
            panel.SetPropertyBlock(block);
        }

        void Update()
        {
            if (Level == ThreatLevel.Warning || Level == ThreatLevel.Alert || Level == ThreatLevel.Critical)
                Apply(0.7f + 0.3f * Mathf.Sin(Time.time * 6f));
        }
    }
}
