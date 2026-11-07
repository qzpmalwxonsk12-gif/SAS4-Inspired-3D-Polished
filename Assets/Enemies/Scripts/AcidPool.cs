using System.Collections.Generic;
using UnityEngine;

namespace ShipEnemies
{
    public sealed class AcidPool : MonoBehaviour
    {
        public static int ActiveCount { get; private set; }
        public float radius = 1.3f, lifetime = 5, damagePerSecond = 6;
        float expires, nextTick;
        readonly Collider[] overlaps = new Collider[32];
        readonly HashSet<Vitality> victims = new HashSet<Vitality>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetCount() => ActiveCount = 0;
        void OnEnable() { ActiveCount++; expires = Time.time + lifetime; }
        void OnDisable() { ActiveCount--; }
        public void Configure(float size, float seconds, float damage)
        { radius = size; lifetime = seconds; damagePerSecond = damage; expires = Time.time + lifetime; transform.localScale = new Vector3(radius * 2, .025f, radius * 2); }
        void Update()
        {
            if (Time.time >= expires) { Destroy(gameObject); return; }
            if (Time.time < nextTick) return;
            nextTick = Time.time + .25f; victims.Clear();
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius + .6f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = overlaps[i].GetComponentInParent<Vitality>();
                if (target == null || !target.receivesEnemyAttacks || !victims.Add(target)) continue;
                var delta = target.transform.position - transform.position;
                if (Mathf.Abs(delta.y) < 1.5f && new Vector2(delta.x, delta.z).magnitude <= radius + .25f)
                    target.TakeDamage(damagePerSecond * .25f, DamageKind.Acid);
            }
            float shrink = Mathf.Clamp01((expires - Time.time) / .6f);
            transform.localScale = new Vector3(radius * 2 * shrink, .025f, radius * 2 * shrink);
        }
    }
}
