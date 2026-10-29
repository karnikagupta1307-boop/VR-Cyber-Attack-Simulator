using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberDefenseVR
{
    /// <summary>
    /// Alternative, UI-independent controls so the simulation can always be driven:
    /// Keyboard: SPACE = start attack, D = activate defense, R = reset.
    /// Quest controllers: right A = start attack, right B = activate defense, left X = reset.
    /// Added automatically by SimulationController at runtime (existing scenes need no changes).
    /// </summary>
    public class SimulationHotkeys : MonoBehaviour
    {
        public SimulationController simulation;
        InputAction start, defend, reset;

        void Awake()
        {
            start = new InputAction(type: InputActionType.Button);
            start.AddBinding("<Keyboard>/space"); start.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            defend = new InputAction(type: InputActionType.Button);
            defend.AddBinding("<Keyboard>/d"); defend.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            reset = new InputAction(type: InputActionType.Button);
            reset.AddBinding("<Keyboard>/r"); reset.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
        }

        void OnEnable() { start.Enable(); defend.Enable(); reset.Enable(); }
        void OnDisable() { start.Disable(); defend.Disable(); reset.Disable(); }

        void Update()
        {
            if (simulation == null) return;
            if (start.WasPressedThisFrame()) { Click(); simulation.StartAttack(); }
            if (defend.WasPressedThisFrame()) { Click(); simulation.ActivateDefense(); }
            if (reset.WasPressedThisFrame()) { Click(); simulation.ResetSimulation(); }
        }

        static void Click() { AudioManager.Instance.Play(Sfx.Click); }
    }
}
