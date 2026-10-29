using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CyberDefenseVR
{
    /// <summary>
    /// Floating world-space dashboard: status, training feedback, node status, attack progress, result and the control buttons.
    /// All values come from SimulationController. The extra node/progress/result strip is built at runtime so
    /// scenes generated earlier keep working without regeneration.
    /// </summary>
    public class DashboardController : MonoBehaviour
    {
        public SimulationController simulation;
        public Canvas canvas;
        public TMP_Text networkStatusValue, threatValue, stageValue, defenseValue, coachBody;
        public Button startButton, defenseButton, resetButton, menuButton;

        static readonly Color Cyan = new Color(0.2f, 0.9f, 1f);
        static readonly Color Orange = new Color(1f, 0.6f, 0.2f);
        static readonly Color Red = new Color(1f, 0.35f, 0.35f);
        static readonly Color Green = new Color(0.3f, 1f, 0.7f);
        static readonly Color Dim = new Color(0.55f, 0.6f, 0.68f);

        // runtime-built extras (private on purpose: not serialized scene references)
        TMP_Text nodesText, progressLabel, resultText;
        RectTransform progressFill;
        int shownPercent = -1;

        void Awake() { BuildExtras(); }

        void OnEnable()
        {
            startButton.onClick.AddListener(OnStart);
            defenseButton.onClick.AddListener(OnDefense);
            resetButton.onClick.AddListener(OnReset);
            menuButton.onClick.AddListener(OnMenu);
            simulation.Changed += Refresh;
        }

        void OnDisable()
        {
            startButton.onClick.RemoveListener(OnStart);
            defenseButton.onClick.RemoveListener(OnDefense);
            resetButton.onClick.RemoveListener(OnReset);
            menuButton.onClick.RemoveListener(OnMenu);
            simulation.Changed -= Refresh;
        }

        void Start()
        {
            if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
            Refresh();
            var es = EventSystem.current;
            Debug.Log("[CyberDefenseVR] Dashboard ready. EventSystem: " + (es != null ? es.name : "NONE") +
                      ", canvas camera: " + (canvas != null && canvas.worldCamera != null ? canvas.worldCamera.name : "NONE") +
                      ", GraphicRaycaster: " + (canvas != null && canvas.GetComponent<GraphicRaycaster>() != null) +
                      ". Keyboard: SPACE start, D defense, R reset. Quest: A start, B defense, X reset.");
        }

        void Update()
        {
            if (progressFill == null) return;
            int pct = Mathf.RoundToInt(simulation.AttackProgress * 100f);
            if (pct == shownPercent) return;
            shownPercent = pct;
            progressFill.anchorMax = new Vector2(pct / 100f, 1f);
            progressLabel.text = "ATTACK PROGRESS  " + pct + "%";
        }

        void OnStart() { Debug.Log("[CyberDefenseVR] START ATTACK button pressed."); Click(); simulation.StartAttack(); }
        void OnDefense() { Debug.Log("[CyberDefenseVR] ACTIVATE DEFENSE button pressed."); Click(); simulation.ActivateDefense(); }
        void OnReset() { Debug.Log("[CyberDefenseVR] RESET button pressed."); Click(); simulation.ResetSimulation(); }
        void OnMenu() { Click(); SceneTransitionManager.Load(SceneTransitionManager.MainMenu); }
        static void Click() { AudioManager.Instance.Play(Sfx.Click); }

        void Refresh()
        {
            var s = simulation.State;
            string net, stage, def, coach; Color netC;
            switch (s)
            {
                case SimulationState.Attacking:
                    net = "UNDER ATTACK"; netC = Orange; def = "STANDBY";
                    if (simulation.ThreatDetected)
                    {
                        stage = "2/4  THREAT DETECTED at router";
                        coach = "THREAT DETECTED: threat detection flagged an unusual traffic surge heading for the router. Press ACTIVATE DEFENSE (or B / D) to raise a firewall before it reaches the server.";
                    }
                    else
                    {
                        stage = "1/4  Attacker sending traffic";
                        coach = "The ATTACKER (left) is sending suspicious traffic toward the ROUTER (center). Watch the beam travel along the connection.";
                    }
                    break;
                case SimulationState.Defending:
                    net = "DEFENDING"; netC = Cyan; def = "ACTIVE: firewall shield"; stage = "3/4  Defense active at router";
                    coach = "The firewall shield now filters traffic at the router, the network's entry point. Wait for the traffic to arrive.";
                    break;
                case SimulationState.AttackBlocked:
                    net = "PROTECTED"; netC = Green; def = "ACTIVE: firewall shield"; stage = "4/4  Attack blocked";
                    coach = "THREAT NEUTRALIZED. The firewall stopped the malicious traffic at the router, so the SERVER was never reached. Stopping threats early is the idea behind network defense. Press RESET to try again.";
                    break;
                case SimulationState.AttackSuccessful:
                    net = "COMPROMISED"; netC = Red; def = "NOT ACTIVATED"; stage = "Server reached (breach)";
                    coach = "Simulated breach: the traffic reached the server because no defense was active in time. Detecting a threat is not enough; you must respond. Press RESET and try again.";
                    break;
                case SimulationState.Resetting:
                    net = "RESETTING"; netC = Dim; def = "OFFLINE"; stage = "Restoring network"; coach = "Restoring the simulation...";
                    break;
                default:
                    net = "SECURE"; netC = Cyan; def = "STANDBY"; stage = "Standby: no active threat";
                    coach = "Your network is secure. The ATTACKER (left) is a fictional hostile machine, the ROUTER (center) forwards traffic, and the PROTECTED SERVER (right) holds critical data.\n\nPress START ATTACK (or A / SPACE) to begin.";
                    break;
            }
            if (!string.IsNullOrEmpty(simulation.Notice)) coach = simulation.Notice;

            networkStatusValue.text = net; networkStatusValue.color = netC;
            threatValue.text = s == SimulationState.Ready ? "None" : "Suspicious traffic flood (simulated)";
            stageValue.text = stage;
            defenseValue.text = def;
            coachBody.text = coach;

            if (nodesText != null)
                nodesText.text = "ATTACKER<pos=34%>" + simulation.attacker.StatusWord + "\nROUTER<pos=34%>" + simulation.router.StatusWord + "\nSERVER<pos=34%>" + simulation.server.StatusWord;
            if (resultText != null)
            {
                string r = simulation.Result;
                if (string.IsNullOrEmpty(r)) r = (s == SimulationState.Attacking || s == SimulationState.Defending) ? "IN PROGRESS" : "--";
                resultText.text = "RESULT: " + r;
                resultText.color = r == "ATTACK BLOCKED" ? Green : (r == "SERVER BREACHED" ? Red : Color.white);
            }

            SetButton(startButton, s == SimulationState.Ready);
            SetButton(defenseButton, s == SimulationState.Attacking);
            SetButton(resetButton, s != SimulationState.Ready && s != SimulationState.Resetting);
            SetButton(menuButton, true);
        }

        static void SetButton(Button b, bool on)
        {
            b.interactable = on;
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t != null) t.color = on ? Color.white : new Color(0.6f, 0.65f, 0.72f);
        }

        // ------------------------------------------------------------ runtime extras
        static RectTransform Rt(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }

        static Image Box(RectTransform r, Color c)
        {
            var img = r.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false; return img;
        }

        static TMP_Text Txt(RectTransform r, float size, Color c, TextAlignmentOptions a, bool bold)
        {
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        void BuildExtras()
        {
            if (nodesText != null || canvas == null) return;
            var root = canvas.transform;
            var crt = (RectTransform)root;

            // Give the dashboard 25% more height for the extra strip; all existing children are anchor-based and stretch with it.
            if (Mathf.Abs(crt.sizeDelta.y - 720f) < 1f)
            {
                crt.sizeDelta = new Vector2(crt.sizeDelta.x, 900f);
                var bezel = transform.parent != null ? transform.parent.Find("Bezel") : null;
                if (bezel != null) bezel.localScale = new Vector3(bezel.localScale.x, 0.96f, bezel.localScale.z);
            }
            var status = root.Find("StatusPanel") as RectTransform;
            var coach = root.Find("CoachPanel") as RectTransform;
            if (status != null) { status.anchorMin = new Vector2(0.03f, 0.37f); status.anchorMax = new Vector2(0.55f, 0.83f); }
            if (coach != null) { coach.anchorMin = new Vector2(0.58f, 0.37f); coach.anchorMax = new Vector2(0.97f, 0.83f); }

            var left = Rt("NodesPanel", root, new Vector2(0.03f, 0.235f), new Vector2(0.55f, 0.355f));
            Box(left, new Color(1f, 1f, 1f, 0.05f));
            nodesText = Txt(Rt("NodesText", left, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f)), 22, Color.white, TextAlignmentOptions.MidlineLeft, true);

            var right = Rt("ProgressPanel", root, new Vector2(0.58f, 0.235f), new Vector2(0.97f, 0.355f));
            Box(right, new Color(0.1f, 0.85f, 1f, 0.08f));
            progressLabel = Txt(Rt("ProgressLabel", right, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.97f)), 20, Dim, TextAlignmentOptions.MidlineLeft, false);
            progressLabel.text = "ATTACK PROGRESS  0%";
            var barBg = Rt("ProgressBar", right, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.58f));
            Box(barBg, new Color(0.15f, 0.2f, 0.28f, 1f));
            progressFill = Rt("ProgressFill", barBg, Vector2.zero, new Vector2(0f, 1f));
            Box(progressFill, Orange);
            resultText = Txt(Rt("ResultText", right, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.40f)), 22, Color.white, TextAlignmentOptions.MidlineLeft, true);

            if (networkStatusValue != null) networkStatusValue.fontSize = 30;
        }
    }
}
