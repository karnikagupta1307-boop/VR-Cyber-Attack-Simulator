using UnityEngine;

namespace CyberDefenseVR
{
    /// <summary>Yaw-only billboard so world-space labels face the player.</summary>
    public class Billboard : MonoBehaviour
    {
        Transform cam;
        void LateUpdate()
        {
            if (cam == null) { var c = Camera.main; if (c == null) return; cam = c.transform; }
            Vector3 d = transform.position - cam.position; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}
