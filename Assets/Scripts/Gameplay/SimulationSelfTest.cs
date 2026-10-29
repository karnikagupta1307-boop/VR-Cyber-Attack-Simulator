using System;
using System.Collections;
using UnityEngine;

namespace CyberDefenseVR
{
    /// <summary>
    /// Runtime smoke test of repeated attack/defend/reset cycles. Add to a scene (already present, disabled, in DesktopTest),
    /// tick Run On Start, press Play, and read the Console. Time is sped up while running.
    /// </summary>
    public class SimulationSelfTest : MonoBehaviour
    {
        public SimulationController simulation;
        public bool runOnStart = false;
        public int cycles = 3;
        public float timeScale = 6f;

        int failures; bool conditionMet;

        void Start() { if (runOnStart) StartCoroutine(Run()); }

        [ContextMenu("Run Self Test")]
        public void RunNow() { StartCoroutine(Run()); }

        IEnumerator Run()
        {
            Time.timeScale = timeScale; failures = 0;
            for (int i = 1; i <= cycles; i++)
            {
                Check(simulation.State == SimulationState.Ready, "cycle " + i + ": starts Ready");
                Check(simulation.StartAttack(), "cycle " + i + ": StartAttack accepted");
                Check(!simulation.StartAttack(), "cycle " + i + ": second StartAttack rejected");
                yield return Wait(() => simulation.ThreatDetected, 20f);
                Check(conditionMet, "cycle " + i + ": threat detected");
                Check(simulation.ActivateDefense(), "cycle " + i + ": defense accepted");
                yield return Wait(() => simulation.State == SimulationState.AttackBlocked, 20f);
                Check(conditionMet, "cycle " + i + ": attack blocked");
                simulation.ResetSimulation();
                yield return Wait(() => simulation.State == SimulationState.Ready, 5f);
                Check(conditionMet, "cycle " + i + ": reset to Ready (after block)");

                simulation.StartAttack();
                yield return Wait(() => simulation.State == SimulationState.AttackSuccessful, 30f);
                Check(conditionMet, "cycle " + i + ": undefended attack succeeds");
                simulation.ResetSimulation();
                yield return Wait(() => simulation.State == SimulationState.Ready, 5f);
                Check(conditionMet, "cycle " + i + ": reset to Ready (after breach)");

                simulation.StartAttack(); yield return new WaitForSeconds(0.5f);
                simulation.ResetSimulation();
                yield return Wait(() => simulation.State == SimulationState.Ready, 5f);
                Check(conditionMet && !simulation.attack.IsRunning, "cycle " + i + ": reset mid-attack stops attack");
            }
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[SelfTest] PASSED" : "[SelfTest] FAILED (" + failures + " failures)");
        }

        IEnumerator Wait(Func<bool> cond, float timeoutRealSeconds)
        {
            float end = Time.realtimeSinceStartup + timeoutRealSeconds;
            conditionMet = false;
            while (Time.realtimeSinceStartup < end) { if (cond()) { conditionMet = true; yield break; } yield return null; }
        }

        void Check(bool ok, string what)
        {
            if (!ok) failures++;
            Debug.Log("[SelfTest] " + (ok ? "PASS " : "FAIL ") + what);
        }
    }
}
