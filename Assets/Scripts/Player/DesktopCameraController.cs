using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberDefenseVR
{
    /// <summary>Hold the right mouse button and drag to look around (Editor / desktop testing).</summary>
    public class DesktopCameraController : MonoBehaviour
    {
        public float sensitivity = 0.12f;
        float yaw, pitch;

        void Start() { var e = transform.eulerAngles; yaw = e.y; pitch = e.x > 180f ? e.x - 360f : e.x; }

        void Update()
        {
            var m = Mouse.current;
            if (m == null || !m.rightButton.isPressed) return;
            Vector2 d = m.delta.ReadValue();
            yaw += d.x * sensitivity;
            pitch = Mathf.Clamp(pitch - d.y * sensitivity, -80f, 80f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
