using UnityEngine;

namespace CyberDefenseVR
{
    /// <summary>Subtle bobbing/spinning for ambient life.</summary>
    public class AmbientMotion : MonoBehaviour
    {
        public float bobAmplitude = 0.05f, bobSpeed = 0.8f, spinDegreesPerSecond = 10f;
        Vector3 basePos; float phase;
        void Awake() { basePos = transform.localPosition; phase = Random.value * 6.28f; }
        void Update()
        {
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude);
            if (spinDegreesPerSecond != 0f) transform.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
    }
}
