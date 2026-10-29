using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberDefenseVR
{
    /// <summary>
    /// Animates a purely visual, fictional "traffic" attack: attacker -> router -> server, drawn as a pulsing beam
    /// plus lead packets. No real networking happens. If the shield is up when traffic reaches the router it is blocked.
    /// </summary>
    public class AttackController : MonoBehaviour
    {
        public Transform attackerAnchor, routerAnchor, serverAnchor;
        public Transform[] packets;
        public float segmentOneSeconds = 6f, segmentTwoSeconds = 2.5f, detectAt = 0.4f, packetSpacing = 0.1f;
        public float packetScale = 1.6f, beamWidth = 0.05f;

        public event Action ThreatDetected, Blocked, ServerHit;
        public bool IsRunning { get { return routine != null; } }
        /// <summary>0..1 along attacker -> router (0..0.5) -> server (0.5..1).</summary>
        public float Progress { get; private set; }

        Coroutine routine;
        DefenseController defense;
        Vector3[] baseScales;
        LineRenderer beam;      // created at runtime so existing scenes need no change
        ImpactEffect impact;    // created at runtime

        void Awake()
        {
            baseScales = new Vector3[packets.Length];
            for (int i = 0; i < packets.Length; i++) baseScales[i] = packets[i].localScale * packetScale;
            EnsureEffects();
            HideAll();
        }

        void EnsureEffects()
        {
            if (beam == null)
            {
                var go = new GameObject("AttackBeam");
                go.transform.SetParent(transform, false);
                beam = go.AddComponent<LineRenderer>();
                beam.useWorldSpace = true; beam.positionCount = 2; beam.numCapVertices = 4;
                beam.shadowCastingMode = ShadowCastingMode.Off; beam.receiveShadows = false;
                var r = packets.Length > 0 ? packets[0].GetComponent<Renderer>() : null;
                if (r != null) beam.sharedMaterial = r.sharedMaterial;
                beam.enabled = false;
            }
            if (impact == null)
            {
                var go = new GameObject("ImpactEffect");
                go.transform.SetParent(transform, false);
                impact = go.AddComponent<ImpactEffect>();
            }
        }

        public void Launch(DefenseController d)
        {
            Stop();
            defense = d;
            routine = StartCoroutine(Run());
        }

        public void StopAndClear()
        {
            Stop();
            HideAll();
            Progress = 0f;
            if (impact != null) impact.Clear();
        }

        void Stop()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
        }

        Material PacketMaterial()
        {
            var r = packets.Length > 0 ? packets[0].GetComponent<Renderer>() : null;
            return r != null ? r.sharedMaterial : null;
        }

        Material ShieldMaterial()
        {
            if (defense != null && defense.shield != null)
            {
                var r = defense.shield.GetComponent<Renderer>();
                if (r != null) return r.sharedMaterial;
            }
            return PacketMaterial();
        }

        IEnumerator Run()
        {
            HideAll();
            Progress = 0f;
            float lead = 0f; bool detected = false;
            while (lead < 1f)
            {
                lead += Time.deltaTime / segmentOneSeconds;
                float l = Mathf.Min(lead, 1f);
                Place(l); Progress = l * 0.5f;
                if (!detected && lead >= detectAt) { detected = true; if (ThreatDetected != null) ThreatDetected(); }
                yield return null;
            }
            Place(1f); Progress = 0.5f;

            if (defense != null && defense.IsShieldUp)
            {
                defense.Pulse();
                if (impact != null) impact.Play(routerAnchor.position, ShieldMaterial(), 0.07f, 1.1f, 0.8f);
                if (beam != null) beam.enabled = false;
                float t = 0f;
                while (t < 0.4f)
                {
                    t += Time.deltaTime;
                    float k = 1f - t / 0.4f;
                    for (int i = 0; i < packets.Length; i++) packets[i].localScale = baseScales[i] * Mathf.Max(k, 0f);
                    yield return null;
                }
                HideAll();
                routine = null;
                if (Blocked != null) Blocked();
                yield break;
            }

            while (lead < 2f)
            {
                lead += Time.deltaTime / segmentTwoSeconds;
                float l = Mathf.Min(lead, 2f);
                Place(l); Progress = l * 0.5f;
                yield return null;
            }
            Place(2f); Progress = 1f;
            if (impact != null) impact.Play(serverAnchor.position, PacketMaterial(), 0.09f, 1.3f, 1.0f);
            yield return new WaitForSeconds(0.15f);
            HideAll();
            routine = null;
            if (ServerHit != null) ServerHit();
        }

        void Place(float lead)
        {
            for (int i = 0; i < packets.Length; i++)
            {
                float p = lead - i * packetSpacing;
                var tr = packets[i];
                if (p < 0f) { if (tr.gameObject.activeSelf) tr.gameObject.SetActive(false); continue; }
                if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
                tr.localScale = baseScales[i];
                tr.position = p <= 1f ? Vector3.Lerp(attackerAnchor.position, routerAnchor.position, p)
                                      : Vector3.Lerp(routerAnchor.position, serverAnchor.position, p - 1f);
            }
            UpdateBeam(lead);
        }

        void UpdateBeam(float lead)
        {
            if (beam == null) return;
            beam.enabled = true;
            Vector3 a = attackerAnchor.position, r = routerAnchor.position;
            if (lead <= 1f)
            {
                beam.positionCount = 2;
                beam.SetPosition(0, a); beam.SetPosition(1, Vector3.Lerp(a, r, lead));
            }
            else
            {
                beam.positionCount = 3;
                beam.SetPosition(0, a); beam.SetPosition(1, r); beam.SetPosition(2, Vector3.Lerp(r, serverAnchor.position, lead - 1f));
            }
            beam.widthMultiplier = beamWidth * (1f + 0.35f * Mathf.Sin(Time.time * 24f));
        }

        void HideAll()
        {
            if (beam != null) beam.enabled = false;
            if (packets == null) return;
            for (int i = 0; i < packets.Length; i++)
            {
                if (packets[i] == null) continue;
                packets[i].gameObject.SetActive(false);
                if (baseScales != null && i < baseScales.Length) packets[i].localScale = baseScales[i];
            }
        }
    }
}
