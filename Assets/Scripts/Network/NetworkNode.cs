using System.Collections;
using TMPro;
using UnityEngine;

namespace CyberDefenseVR
{
    public enum NodeStatus { Idle, Hostile, Alert, Protected, Compromised }

    /// <summary>A node in the network visualisation (attacker, router, server). Status is shown with colour AND text.</summary>
    public class NetworkNode : MonoBehaviour
    {
        public string displayName = "NODE";
        public string idleWord = "ONLINE";
        public Renderer[] glowRenderers;
        public TMP_Text label;
        public Transform anchor;

        static readonly Color IdleC = new Color(0.1f, 0.8f, 1f);
        static readonly Color HostileC = new Color(1f, 0.5f, 0.1f);
        static readonly Color AlertC = new Color(1f, 0.2f, 0.25f);
        static readonly Color ProtectedC = new Color(0.15f, 0.95f, 0.65f);

        public NodeStatus Status { get; private set; }
        public string StatusWord { get; private set; }
        MaterialPropertyBlock block;
        Vector3 originLocalPos;
        Coroutine shakeRoutine;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            originLocalPos = transform.localPosition;
            SetStatus(NodeStatus.Idle);
        }

        public void SetStatus(NodeStatus s)
        {
            if (block == null) block = new MaterialPropertyBlock();
            Status = s;
            Color c; string word;
            switch (s)
            {
                case NodeStatus.Hostile: c = HostileC; word = "HOSTILE"; break;
                case NodeStatus.Alert: c = AlertC; word = "THREAT DETECTED"; break;
                case NodeStatus.Protected: c = ProtectedC; word = "DEFENDED"; break;
                case NodeStatus.Compromised: c = AlertC; word = "COMPROMISED"; break;
                default: c = IdleC; word = idleWord; break;
            }
            StatusWord = word;
            block.SetColor(EmissionId, c * 2.2f);
            if (glowRenderers != null)
                for (int i = 0; i < glowRenderers.Length; i++)
                    if (glowRenderers[i] != null) glowRenderers[i].SetPropertyBlock(block);
            if (label != null) label.text = displayName + "\n<size=60%>" + word + "</size>";
        }

        public void Shake(float seconds = 0.7f)
        {
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(ShakeRoutine(seconds));
        }

        IEnumerator ShakeRoutine(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                transform.localPosition = originLocalPos + Random.insideUnitSphere * 0.04f * (1f - t / seconds);
                yield return null;
            }
            transform.localPosition = originLocalPos;
            shakeRoutine = null;
        }

        public void ResetNode()
        {
            if (shakeRoutine != null) { StopCoroutine(shakeRoutine); shakeRoutine = null; }
            transform.localPosition = originLocalPos;
            SetStatus(NodeStatus.Idle);
        }
    }
}
