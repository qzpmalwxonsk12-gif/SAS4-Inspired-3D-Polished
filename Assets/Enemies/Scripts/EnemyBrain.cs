using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ShipEnemies
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Vitality))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public EnemyDefinition definition;
        public Animator animator;
        public Transform mouth;
        public Vitality target;
        public bool displayOnly;
        public string State { get; private set; } = "Idle";
        public int MeleeImpacts { get; private set; }
        public int RangedAttacks { get; private set; }
        public int ParasitesCreated { get; private set; }
        public event Action<EnemyBrain> Died;
        public Vitality Health { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public static readonly HashSet<EnemyBrain> Living = new HashSet<EnemyBrain>();
        // Optional mission hooks; asset previews retain their original behavior when these are unset.
        public static Func<bool> CanSpawnParasite;
        public Func<bool> MovementOverride;
        public const int ParasiteLimit = 16;
        float nextPath, nextMelee, nextRanged, attackStarted, hitUntil;
        bool rangedAttack, impacted, dead, initialized;
        string animationState;
        public string AnimationState=>animationState??"Idle";
        static readonly RaycastHit[] sightHits = new RaycastHit[24];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() { Living.Clear(); CanSpawnParasite = null; }
        void Awake()
        {
            displayOnly |= SASCoop.CoopSession.Client;
            Agent = GetComponent<NavMeshAgent>(); Health = GetComponent<Vitality>();
            if (definition == null) { enabled = false; return; }
            Health.maximum = definition.health * (definition.kind==EnemyKind.Regurgitator?SASCoop.CoopSession.Difficulty:1); Health.acidResistance = definition.chemicalResistance; Health.ResetHealth();
            Agent.speed = definition.speed; Agent.angularSpeed = definition.turnSpeed;
            if(definition.kind==EnemyKind.Shambler||definition.kind==EnemyKind.Regurgitator)
                Agent.speed=Onslaught.Mission.MissionDirector.Instance?4.5f:5f;
            if(!displayOnly&&!GetComponent<EnemyTargeting>())gameObject.AddComponent<EnemyTargeting>();
            Agent.stoppingDistance = Mathf.Max(.25f, definition.meleeRange * .78f);
            Health.Died += Die; Health.Damaged += OnHit; initialized = true;
        }
        void OnEnable() { if (!displayOnly) Living.Add(this); }
        void Start()
        {
            if (displayOnly) Agent.enabled = false;
            if(!displayOnly&&!GetComponent<SafeEnemyPlacement>())gameObject.AddComponent<SafeEnemyPlacement>();
            if (!displayOnly)
            {
                var myColliders = GetComponentsInChildren<Collider>(true);
                foreach (var cc in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                    foreach (var col in myColliders)
                        if (col && cc) Physics.IgnoreCollision(col, cc, true);
            }
            Play("Idle"); nextRanged = Time.time + 1.2f;
        }
        void OnDisable() { Living.Remove(this); }
        void OnDestroy() { if (Health != null) { Health.Died -= Die; Health.Damaged -= OnHit; } }
        public void SetTarget(Vitality value) { target = value; nextPath = 0; }
        public void Play(string state, bool restart = false)
        {
            if (animator == null || (!restart && animationState == state)) return;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) return;
            animator.CrossFadeInFixedTime(hash, .1f, 0, 0); animationState = state;
        }
        public void StopMoving() { if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = true; }
        public bool HasLineOfSight()
        {
            if (target == null) return false;
            Vector3 from = mouth != null ? mouth.position : transform.position + Vector3.up;
            Vector3 delta = target.AimPoint - from;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, sightHits, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // An obstruction anywhere before the target blocks the attack. Own colliders are ignored.
            for (int i = 0; i < count; i++)
                if (!sightHits[i].transform.IsChildOf(transform) && !sightHits[i].transform.IsChildOf(target.transform)) return false;
            return count < sightHits.Length;
        }
        void Update()
        {
            if (!initialized || dead || displayOnly) return;
            if (MovementOverride != null && MovementOverride()) return;
            if (target == null || !target.Alive || !target.isActiveAndEnabled)
            { StopMoving(); State = "Idle"; impacted = false; Play("Idle"); return; }
            Vector3 delta = target.transform.position - transform.position; delta.y = 0;
            float distance = delta.magnitude;
            if (State == "Melee" || State == "Ranged")
            {
                Face(delta);
                float elapsed = Time.time - attackStarted;
                float windup = rangedAttack ? definition.rangedWindup : definition.meleeWindup;
                if (!impacted && elapsed >= windup)
                {
                    impacted = true;
                    if (HasLineOfSight())
                    {
                        if (rangedAttack) ReleaseAcid();
                        else if (distance <= definition.meleeRange + .15f)
                        { target.TakeDamage(definition.meleeDamage); MeleeImpacts++; }
                    }
                }
                if (elapsed >= (rangedAttack ? definition.rangedDuration : definition.meleeDuration)) State = "Recover";
                return;
            }
            if (Time.time < hitUntil) { StopMoving(); return; }
            if (distance <= definition.meleeRange && Time.time >= nextMelee && HasLineOfSight())
            { BeginAttack(false); return; }
            if (definition.rangedRange > 0 && distance <= definition.rangedRange && distance > definition.meleeRange * .9f
                && Time.time >= nextRanged && HasLineOfSight())
            { BeginAttack(true); return; }
            if (Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.isStopped = false;
                if (Time.time >= nextPath) { Agent.SetDestination(target.transform.position); nextPath = Time.time + .2f; }
                bool moving = Agent.velocity.sqrMagnitude > .02f;
                State = moving ? "Chase" : "Idle"; Play(moving ? "Move" : "Idle");
                if (!moving) Face(delta);
            }
        }
        void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), definition.turnSpeed * Time.deltaTime);
        }
        void BeginAttack(bool ranged)
        {
            StopMoving(); rangedAttack = ranged; impacted = false; attackStarted = Time.time;
            State = ranged ? "Ranged" : "Melee";
            if (ranged) nextRanged = Time.time + Mathf.Max(definition.rangedDuration, definition.rangedCooldown);
            else nextMelee = Time.time + Mathf.Max(definition.meleeDuration, definition.meleeCooldown);
            Play(ranged ? (definition.kind == EnemyKind.Regurgitator ? "Vomit" : "Spit") : "Melee", true);
        }
        void ReleaseAcid()
        {
            if (definition.projectilePrefab == null || AcidProjectile.ActiveCount >= 16) return;
            RangedAttacks++;
            var projectile = Instantiate(definition.projectilePrefab, mouth.position, Quaternion.identity);
            projectile.Launch(this, target.AimPoint, definition);
            SpawnParasites(definition.vomitParasites);
        }
        void OnHit(float amount)
        {
            if (Health.Current <= 0 || displayOnly || State == "Melee" || State == "Ranged") return;
            // Flinch cannot be chained indefinitely by repeated hits.
            if (Time.time < hitUntil + .8f) return;
            hitUntil = Time.time + .22f; Play("Hit", true);
        }
        void Die()
        {
            if (dead) return;
            dead = true; State = "Dead"; StopMoving(); Agent.enabled = false;
            Living.Remove(this); foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Play("Death", true); SpawnParasites(definition.deathParasites); Died?.Invoke(this);
            if (!displayOnly) Destroy(gameObject, 9);
        }
        void SpawnParasites(int wanted)
        {
            if (displayOnly || wanted <= 0 || definition.parasitePrefab == null) return;
            int existing = 0;
            foreach (var e in Living) if (e != null && e.definition != null && (e.definition.kind == EnemyKind.Worm || e.definition.kind == EnemyKind.PukeWorm)) existing++;
            int count = Mathf.Min(wanted, ParasiteLimit - existing);
            for (int i = 0; i < count; i++)
            {
                if (CanSpawnParasite != null && !CanSpawnParasite()) break;
                float angle = (i + .5f) * Mathf.PI * 2 / Mathf.Max(1, count);
                Vector3 p = transform.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (definition.radius + .6f);
                var filter = new NavMeshQueryFilter { agentTypeID = definition.parasitePrefab.GetComponent<NavMeshAgent>().agentTypeID, areaMask = NavMesh.AllAreas };
                  var encounter=GetComponent<Vaccine.VaccineEnemyActor>();
                  Vector3 safe;
                  if(encounter&&encounter.mission)
                  {if(!encounter.mission.FindEncounterPosition(definition.parasitePrefab,p,encounter.zone,out safe,null,false))continue;}
                  else if (!SafeEnemyPlacement.Find(definition.parasitePrefab,p,out safe,2)) continue;
                var child = Instantiate(definition.parasitePrefab, safe, transform.rotation, transform.parent);
                  if(encounter&&encounter.mission)child.GetComponent<Vaccine.VaccineEnemyActor>().Bind(encounter.mission,encounter.zone);
                child.displayOnly = false; child.SetTarget(target); ParasitesCreated++;
            }
        }
    }
}
