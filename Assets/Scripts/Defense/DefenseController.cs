using System.Collections;
using UnityEngine;

namespace CyberDefenseVR
{
    /// <summary>Firewall shield protecting the router.</summary>
    public class DefenseController : MonoBehaviour
    {
        public Transform shield;
        public float activateSeconds = 0.6f;
        public bool IsShieldUp { get; private set; }

        Vector3 fullScale;
        bool grown;
        Coroutine growRoutine, pulseRoutine;

        void Awake()
        {
            fullScale = shield.localScale;
            shield.gameObject.SetActive(false);
        }

        public void Activate()
        {
            if (IsShieldUp) return;
            IsShieldUp = true;
            grown = false;
            shield.gameObject.SetActive(true);
            if (growRoutine != null) StopCoroutine(growRoutine);
            growRoutine = StartCoroutine(Grow());
        }

        IEnumerator Grow()
        {
            float t = 0f;
            while (t < activateSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / activateSeconds);
                shield.localScale = fullScale * k;
                yield return null;
            }
            shield.localScale = fullScale;
            grown = true;
            growRoutine = null;
        }

        /// <summary>Brief flash when traffic hits the shield.</summary>
        public void Pulse()
        {
            if (!IsShieldUp) return;
            if (pulseRoutine != null) StopCoroutine(pulseRoutine);
            pulseRoutine = StartCoroutine(PulseRoutine());
        }

        IEnumerator PulseRoutine()
        {
            grown = false;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                shield.localScale = fullScale * (1f + 0.18f * Mathf.Sin(t / 0.35f * Mathf.PI));
                yield return null;
            }
            shield.localScale = fullScale;
            grown = true;
            pulseRoutine = null;
        }

        void Update()
        {
            if (IsShieldUp && grown) shield.localScale = fullScale * (1f + 0.025f * Mathf.Sin(Time.time * 3f));
        }

        public void ResetImmediate()
        {
            if (growRoutine != null) { StopCoroutine(growRoutine); growRoutine = null; }
            if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
            IsShieldUp = false;
            grown = false;
            shield.localScale = fullScale;
            shield.gameObject.SetActive(false);
        }
    }
}
