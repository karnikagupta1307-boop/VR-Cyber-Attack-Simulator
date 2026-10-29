using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace CyberDefenseVR
{
    /// <summary>
    /// Applies OpenXR head and controller poses (via the Input System) to the XR Origin hierarchy.
    /// With no headset present (Editor/desktop) the camera stays at a standing eye height and controllers are hidden.
    /// </summary>
    public class VRRigDriver : MonoBehaviour
    {
        public Transform head, leftGrip, rightGrip, leftAim, rightAim;
        public float fallbackEyeHeight = 1.6f;
        [Tooltip("Pitch offset (degrees) applied when the aim pose is unavailable and the grip pose is used instead.")]
        public float gripToAimPitch = 0f;

        InputAction headPos, headRot, lGripPos, lGripRot, rGripPos, rGripRot, lAimPos, lAimRot, rAimPos, rAimRot;

        static InputAction V3(string path) { var a = new InputAction(type: InputActionType.Value, binding: path, expectedControlType: "Vector3"); return a; }
        static InputAction Q(string path) { var a = new InputAction(type: InputActionType.Value, binding: path, expectedControlType: "Quaternion"); return a; }

        void Awake()
        {
            headPos = V3("<XRHMD>/centerEyePosition"); headRot = Q("<XRHMD>/centerEyeRotation");
            lGripPos = V3("<XRController>{LeftHand}/devicePosition"); lGripRot = Q("<XRController>{LeftHand}/deviceRotation");
            rGripPos = V3("<XRController>{RightHand}/devicePosition"); rGripRot = Q("<XRController>{RightHand}/deviceRotation");
            lAimPos = V3("<XRController>{LeftHand}/pointerPosition"); lAimRot = Q("<XRController>{LeftHand}/pointerRotation");
            rAimPos = V3("<XRController>{RightHand}/pointerPosition"); rAimRot = Q("<XRController>{RightHand}/pointerRotation");
        }

        void OnEnable()
        {
            foreach (var a in All()) a.Enable();
            Application.onBeforeRender += Apply;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= Apply;
            foreach (var a in All()) a.Disable();
        }

        InputAction[] All()
        {
            return new[] { headPos, headRot, lGripPos, lGripRot, rGripPos, rGripRot, lAimPos, lAimRot, rAimPos, rAimRot };
        }

        void Update() { Apply(); }

        void Apply()
        {
            bool hmd = InputSystem.GetDevice<XRHMD>() != null;
            if (head != null)
            {
                if (hmd)
                {
                    if (headPos.activeControl != null) head.localPosition = headPos.ReadValue<Vector3>();
                    if (headRot.activeControl != null) head.localRotation = headRot.ReadValue<Quaternion>();
                }
                else head.localPosition = new Vector3(0f, fallbackEyeHeight, 0f);
            }
            ApplyHand(XRController.leftHand != null, leftGrip, leftAim, lGripPos, lGripRot, lAimPos, lAimRot);
            ApplyHand(XRController.rightHand != null, rightGrip, rightAim, rGripPos, rGripRot, rAimPos, rAimRot);
        }

        void ApplyHand(bool present, Transform grip, Transform aim, InputAction gp, InputAction gr, InputAction ap, InputAction ar)
        {
            if (grip != null && grip.gameObject.activeSelf != present) grip.gameObject.SetActive(present);
            if (!present) return;
            Vector3 gripPos = gp.ReadValue<Vector3>();
            Quaternion gripRot = gr.ReadValue<Quaternion>();
            if (grip != null) { grip.localPosition = gripPos; grip.localRotation = gripRot; }
            if (aim != null)
            {
                if (ap.activeControl != null && ar.activeControl != null)
                { aim.localPosition = ap.ReadValue<Vector3>(); aim.localRotation = ar.ReadValue<Quaternion>(); }
                else { aim.localPosition = gripPos; aim.localRotation = gripRot * Quaternion.Euler(gripToAimPitch, 0f, 0f); }
            }
        }
    }
}
