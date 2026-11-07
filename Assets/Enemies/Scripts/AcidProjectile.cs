using UnityEngine;

namespace ShipEnemies
{
    public sealed class AcidProjectile : MonoBehaviour
    {
        public static int ActiveCount { get; private set; }
        public float speed = 12;
        EnemyBrain owner;
        EnemyDefinition definition;
        Vector3 velocity, destination;
        float expires;
        bool launched;
        readonly RaycastHit[] hits = new RaycastHit[16];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetCount() => ActiveCount = 0;
        void OnEnable() { ActiveCount++; }
        void OnDisable() { ActiveCount--; }
        public void Launch(EnemyBrain source, Vector3 point, EnemyDefinition data)
        {
            owner = source; definition = data; destination = point;
            velocity = (point - transform.position).normalized * speed; expires = Time.time + 4; launched = true;
        }
        void Update()
        {
            if (!launched) return;
            Vector3 move = velocity * Time.deltaTime;
            int count = Physics.SphereCastNonAlloc(transform.position, .12f, move.normalized, hits, move.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity; int chosen = -1;
            for (int i = 0; i < count; i++)
            {
                if (owner != null && hits[i].transform.IsChildOf(owner.transform)) continue;
                if (hits[i].distance < nearest) { nearest = hits[i].distance; chosen = i; }
            }
            if (chosen >= 0)
            {
                var victim = hits[chosen].collider.GetComponentInParent<Vitality>();
                if (victim != null && victim.receivesEnemyAttacks) victim.TakeDamage(definition.acidImpactDamage, DamageKind.Acid);
                Impact(hits[chosen].point); return;
            }
            transform.position += move;
            if (Time.time >= expires || Vector3.Dot(destination - transform.position, velocity) <= 0) Impact(transform.position);
        }
        void Impact(Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * .2f, Vector3.down, hits, 5, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int floor = -1; float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (hits[i].normal.y > .7f && hits[i].collider.GetComponentInParent<Vitality>() == null && hits[i].distance < nearest)
                { floor = i; nearest = hits[i].distance; }
            if (definition.poolPrefab != null && AcidPool.ActiveCount < 12 && floor >= 0)
            {
                var pool = Instantiate(definition.poolPrefab, hits[floor].point + Vector3.up * .025f, Quaternion.identity);
                pool.Configure(definition.acidRadius, definition.acidLifetime, definition.acidDamagePerSecond);
            }
            Destroy(gameObject);
        }
    }
}
