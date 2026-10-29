using System;
using System.Collections;
using UnityEngine;

namespace CyberDefenseVR
{
    /// <summary>Gameplay state machine + coordinator (GameManager / SimulationStateMachine).</summary>
    public class SimulationController : MonoBehaviour
    {
        public AttackController attack;
        public DefenseController defense;
        public NetworkNode attacker, router, server;
        public NetworkConnection attackerToRouter, routerToServer;
        public ThreatIndicator threatIndicator;

        public SimulationState State { get; private set; }
        public bool ThreatDetected { get; private set; }
        public string Notice { get; private set; }
        /// <summary>"" while no result yet, otherwise "ATTACK BLOCKED" or "SERVER BREACHED".</summary>
        public string Result { get; private set; }
        public float AttackProgress { get { return attack != null ? attack.Progress : 0f; } }
        public event Action Changed;

        Coroutine resetRoutine, noticeRoutine;

        void Awake()
        {
            Result = ""; Notice = "";
            // UI-independent controls (keyboard + Quest controller buttons); no scene change needed.
            var keys = GetComponent<SimulationHotkeys>();
            if (keys == null) keys = gameObject.AddComponent<SimulationHotkeys>();
            keys.simulation = this;
        }

        void OnEnable()
        {
            attack.ThreatDetected += OnThreatDetected;
            attack.Blocked += OnBlocked;
            attack.ServerHit += OnServerHit;
        }

        void OnDisable()
        {
            attack.ThreatDetected -= OnThreatDetected;
            attack.Blocked -= OnBlocked;
            attack.ServerHit -= OnServerHit;
            CancelInvoke();
            StopAllCoroutines();
            resetRoutine = null; noticeRoutine = null;
        }

        void Start()
        {
            var audioManager = AudioManager.Instance; // ensure audio exists
            ApplyReadyVisuals();
            State = SimulationState.Ready;
            Raise();
        }

        void Raise() { if (Changed != null) Changed(); }
        void SetState(SimulationState s) { State = s; Raise(); }

        void ApplyReadyVisuals()
        {
            attack.StopAndClear();
            defense.ResetImmediate();
            attacker.ResetNode(); router.ResetNode(); server.ResetNode();
            attackerToRouter.SetState(ConnectionState.Normal);
            routerToServer.SetState(ConnectionState.Normal);
            threatIndicator.SetLevel(ThreatLevel.Secure);
            ThreatDetected = false;
            Notice = ""; Result = "";
        }

        public bool StartAttack()
        {
            if (State != SimulationState.Ready)
            {
                Debug.Log("[CyberDefenseVR] StartAttack ignored: state is " + State + " (press RESET first).");
                return false;
            }
            ClearNotice();
            ThreatDetected = false; Result = "";
            attacker.SetStatus(NodeStatus.Hostile);
            attackerToRouter.SetState(ConnectionState.Threat);
            threatIndicator.SetLevel(ThreatLevel.Warning);
            AudioManager.Instance.Play(Sfx.AttackStarted);
            Debug.Log("[CyberDefenseVR] Attack started.");
            SetState(SimulationState.Attacking);
            attack.Launch(defense);
            return true;
        }

        public bool ActivateDefense()
        {
            if (State == SimulationState.Attacking)
            {
                defense.Activate();
                router.SetStatus(NodeStatus.Protected);
                AudioManager.Instance.Play(Sfx.DefenseActivated);
                Debug.Log("[CyberDefenseVR] Defense activated.");
                SetState(SimulationState.Defending);
                return true;
            }
            if (State == SimulationState.Ready) ShowNotice("No active threat. Press START ATTACK first.");
            else Debug.Log("[CyberDefenseVR] ActivateDefense ignored: state is " + State + ".");
            return false;
        }

        public bool ResetSimulation()
        {
            if (State == SimulationState.Resetting) return false;
            if (resetRoutine != null) StopCoroutine(resetRoutine);
            resetRoutine = StartCoroutine(ResetRoutine());
            return true;
        }

        IEnumerator ResetRoutine()
        {
            SetState(SimulationState.Resetting);
            CancelInvoke();
            if (noticeRoutine != null) { StopCoroutine(noticeRoutine); noticeRoutine = null; }
            ApplyReadyVisuals();
            yield return new WaitForSeconds(0.5f);
            resetRoutine = null;
            SetState(SimulationState.Ready);
        }

        void OnThreatDetected()
        {
            ThreatDetected = true;
            if (State == SimulationState.Attacking) router.SetStatus(NodeStatus.Alert);
            threatIndicator.SetLevel(ThreatLevel.Alert);
            AudioManager.Instance.Play(Sfx.ThreatDetected);
            Raise();
        }

        void OnBlocked()
        {
            attackerToRouter.SetState(ConnectionState.Blocked);
            router.SetStatus(NodeStatus.Protected);
            attacker.SetStatus(NodeStatus.Idle);
            threatIndicator.SetLevel(ThreatLevel.Mitigated);
            Result = "ATTACK BLOCKED";
            AudioManager.Instance.Play(Sfx.AttackBlocked);
            Invoke(nameof(PlayCompleted), 0.7f);
            SetState(SimulationState.AttackBlocked);
        }

        void PlayCompleted() { AudioManager.Instance.Play(Sfx.TrainingCompleted); }

        void OnServerHit()
        {
            routerToServer.SetState(ConnectionState.Threat);
            server.SetStatus(NodeStatus.Compromised);
            server.Shake();
            threatIndicator.SetLevel(ThreatLevel.Critical);
            Result = "SERVER BREACHED";
            AudioManager.Instance.Play(Sfx.AttackSucceeded);
            SetState(SimulationState.AttackSuccessful);
        }

        void ShowNotice(string msg)
        {
            Notice = msg;
            if (noticeRoutine != null) StopCoroutine(noticeRoutine);
            noticeRoutine = StartCoroutine(NoticeRoutine());
            Raise();
        }

        IEnumerator NoticeRoutine()
        {
            yield return new WaitForSeconds(3f);
            Notice = ""; noticeRoutine = null; Raise();
        }

        void ClearNotice()
        {
            if (noticeRoutine != null) { StopCoroutine(noticeRoutine); noticeRoutine = null; }
            Notice = "";
        }
    }
}
