using UnityEngine;

namespace CyberDefenseVR
{
    public enum ConnectionState { Normal, Threat, Blocked }

    /// <summary>A line between two anchors that changes colour by state and pulses while under threat.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class NetworkConnection : MonoBehaviour
    {
        public Transform from, to;
        public ConnectionState State { get; private set; }
        LineRenderer line;
        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake() { Init(); Refresh(); SetState(ConnectionState.Normal); }

        void Init()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            if (block == null) block = new MaterialPropertyBlock();
        }

        public void Refresh()
        {
            Init();
            if (from == null || to == null) return;
            line.positionCount = 2;
            line.SetPosition(0, from.position);
            line.SetPosition(1, to.position);
        }

        public void SetState(ConnectionState s)
        {
            Init();
            State = s;
            Color c = s == ConnectionState.Threat ? new Color(1f, 0.45f, 0.1f)
                    : s == ConnectionState.Blocked ? new Color(0.15f, 0.95f, 0.65f)
                    : new Color(0.1f, 0.6f, 0.9f);
            block.SetColor(BaseColorId, c);
            line.SetPropertyBlock(block);
            line.widthMultiplier = 0.03f;
        }

        void Update()
        {
            if (State == ConnectionState.Threat) line.widthMultiplier = 0.035f + 0.015f * Mathf.Sin(Time.time * 8f);
        }
    }
}
