using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace CyberDefenseVR
{
    /// <summary>
    /// Lightweight controller ray for world-space canvases. Intersects the ray with each world canvas plane,
    /// finds the Button under the ray, drives hover/pressed visuals and fires the click on trigger release
    /// (press + release on the same button, with a short cooldown to prevent accidental repeats).
    /// Only the dominant-hand pointer is active. Mouse input in the Editor is handled by the EventSystem.
    /// </summary>
    public class VRUIPointer : MonoBehaviour
    {
        public Hand hand = Hand.Right;
        public LineRenderer line;
        public Transform reticle;
        public float maxDistance = 6f, clickCooldown = 0.25f;

        InputAction trigger;
        readonly List<Canvas> canvases = new List<Canvas>();
        readonly List<Button> buttonBuffer = new List<Button>();
        Button hovered, pressed;
        PointerEventData ped;
        float nextClickTime, nextScan;

        void Awake()
        {
            string h = hand == Hand.Left ? "LeftHand" : "RightHand";
            trigger = new InputAction(type: InputActionType.Button);
            trigger.AddBinding("<XRController>{" + h + "}/triggerPressed");
            trigger.AddBinding("<XRController>{" + h + "}/{TriggerButton}");
        }

        void OnEnable() { trigger.Enable(); }
        void OnDisable() { trigger.Disable(); ClearHover(); if (line != null) line.enabled = false; if (reticle != null) reticle.gameObject.SetActive(false); }

        void ScanCanvases()
        {
            canvases.Clear();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.WorldSpace) canvases.Add(c);
        }

        void Update()
        {
            bool device = hand == Hand.Left ? XRController.leftHand != null : XRController.rightHand != null;
            bool active = device && SettingsManager.DominantHand == hand;
            if (line != null) line.enabled = active;
            if (!active) { ClearHover(); if (reticle != null) reticle.gameObject.SetActive(false); return; }

            if (Time.unscaledTime >= nextScan) { ScanCanvases(); nextScan = Time.unscaledTime + 2f; }
            if (ped == null && EventSystem.current != null) ped = new PointerEventData(EventSystem.current);
            if (ped == null) return;

            var ray = new Ray(transform.position, transform.forward);
            Button hit = null; float best = maxDistance; Vector3 hitPoint = Vector3.zero;

            for (int i = 0; i < canvases.Count; i++)
            {
                var c = canvases[i];
                if (c == null || !c.isActiveAndEnabled) continue;
                Transform t = c.transform;
                if (Vector3.Dot(ray.direction, t.forward) <= 0.01f) continue;
                var plane = new Plane(-t.forward, t.position);
                float d;
                if (!plane.Raycast(ray, out d) || d > best) continue;
                Vector3 p = ray.GetPoint(d);
                c.GetComponentsInChildren(false, buttonBuffer);
                for (int b = 0; b < buttonBuffer.Count; b++)
                {
                    var btn = buttonBuffer[b];
                    if (!btn.IsInteractable()) continue;
                    var rt = (RectTransform)btn.transform;
                    Vector3 lp = rt.InverseTransformPoint(p);
                    if (rt.rect.Contains(new Vector2(lp.x, lp.y))) { hit = btn; best = d; hitPoint = p; }
                }
            }

            if (hit != hovered)
            {
                if (hovered != null) ExecuteEvents.Execute(hovered.gameObject, ped, ExecuteEvents.pointerExitHandler);
                hovered = hit;
                if (hovered != null) ExecuteEvents.Execute(hovered.gameObject, ped, ExecuteEvents.pointerEnterHandler);
            }

            if (trigger.WasPressedThisFrame() && hovered != null && Time.unscaledTime >= nextClickTime)
            {
                pressed = hovered;
                ExecuteEvents.Execute(pressed.gameObject, ped, ExecuteEvents.pointerDownHandler);
            }
            if (trigger.WasReleasedThisFrame() && pressed != null)
            {
                ExecuteEvents.Execute(pressed.gameObject, ped, ExecuteEvents.pointerUpHandler);
                if (pressed == hovered && pressed.IsInteractable())
                {
                    nextClickTime = Time.unscaledTime + clickCooldown;
                    ExecuteEvents.Execute(pressed.gameObject, ped, ExecuteEvents.pointerClickHandler);
                }
                pressed = null;
            }

            Vector3 end = hovered != null ? hitPoint : ray.GetPoint(maxDistance * 0.5f);
            if (line != null) { line.positionCount = 2; line.SetPosition(0, transform.position); line.SetPosition(1, end); }
            if (reticle != null)
            {
                reticle.gameObject.SetActive(hovered != null);
                if (hovered != null) reticle.position = hitPoint;
            }
        }

        void ClearHover()
        {
            if (hovered != null && ped != null) ExecuteEvents.Execute(hovered.gameObject, ped, ExecuteEvents.pointerExitHandler);
            hovered = null; pressed = null;
        }
    }
}
