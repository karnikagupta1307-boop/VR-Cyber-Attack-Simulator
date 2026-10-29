using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberDefenseVR
{
    /// <summary>Restrained, pooled "shard burst" (no ParticleSystem module needed). Reuses a material supplied by the caller.</summary>
    public class ImpactEffect : MonoBehaviour
    {
        const int Count = 16;
        Transform[] shards;
        Renderer[] renderers;
        Vector3[] dirs;
        Coroutine routine;

        void Awake()
        {
            shards = new Transform[Count]; renderers = new Renderer[Count]; dirs = new Vector3[Count];
            for (int i = 0; i < Count; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "Shard" + i;
                var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
                g.transform.SetParent(transform, false);
                var r = g.GetComponent<Renderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                g.SetActive(false);
                shards[i] = g.transform; renderers[i] = r; dirs[i] = Random.onUnitSphere;
            }
        }

        public void Play(Vector3 position, Material material, float size, float radius, float seconds)
        {
            if (shards == null) return;
            Clear();
            for (int i = 0; i < Count; i++) { if (material != null) renderers[i].sharedMaterial = material; shards[i].gameObject.SetActive(true); }
            routine = StartCoroutine(Run(position, size, radius, seconds));
        }

        IEnumerator Run(Vector3 pos, float size, float radius, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float ease = 1f - (1f - k) * (1f - k);
                for (int i = 0; i < Count; i++)
                {
                    shards[i].position = pos + dirs[i] * radius * ease;
                    shards[i].localScale = Vector3.one * size * (1f - k);
                }
                yield return null;
            }
            routine = null;
            HideShards();
        }

        public void Clear()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            HideShards();
        }

        void HideShards()
        {
            if (shards == null) return;
            for (int i = 0; i < Count; i++) shards[i].gameObject.SetActive(false);
        }
    }
}
