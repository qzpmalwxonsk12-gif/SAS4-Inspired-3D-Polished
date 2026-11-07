#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShipEnemies
{
    // Runs inside the real preview scene through Editor automation. Excluded from builds.
    public sealed class EnemyChecks : MonoBehaviour
    {
        public bool Done;
        public int Passed, Failed;
        public string Stage;
        EnemyPreview lab;
        readonly List<string> lines = new List<string>();
        public static void Begin() { if (!Application.isPlaying) throw new System.InvalidOperationException("Play required"); new GameObject("Enemy verification").AddComponent<EnemyChecks>(); }
        void Check(bool okay, string text) { if (okay) Passed++; else Failed++; lines.Add((okay ? "PASS " : "FAIL ") + text); Save(); }
        void Save() { Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/enemy-checks.txt", $"Completed: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n" + string.Join("\n", lines)); }
        EnemyBrain Spawn(int index, Vector3 p)
        { var e = Instantiate(lab.prefabs[index], p, Quaternion.identity, lab.liveRoot); e.SetTarget(lab.dummy); return e; }
        IEnumerator Clean()
        { lab.ClearDemo(); yield return null; yield return null; lab.dummy.ResetHealth(); }
        IEnumerator Start()
        {
            lab = FindAnyObjectByType<EnemyPreview>();
            Stage = "Asset references and navigation sizes";
            Check(lab != null && lab.displays.Length == 7, "isolated preview loads seven displays");
            for (int i = 0; i < lab.prefabs.Length; i++)
            {
                var prefab = lab.prefabs[i]; var d = prefab.definition;
                var mesh = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
                Check(d != null && prefab.animator != null && prefab.mouth != null && mesh.bones.Length >= 5, d.name + " rig and required references");
                Check(mesh.sharedMaterials.All(m => m != null && m.shader.name.StartsWith("Universal Render Pipeline/")), d.name + " URP materials");
                var clips = prefab.animator.runtimeAnimatorController.animationClips;
                Check(new[] { "Idle", "Move", "Melee", "Hit", "Death" }.All(n => clips.Any(c => c.name == n && c.length > .2f)), d.name + " five base clips");
                var agent = prefab.GetComponent<NavMeshAgent>(); var settings = NavMesh.GetSettingsByID(agent.agentTypeID);
                Check(Mathf.Abs(settings.agentRadius - agent.radius) < .01f && settings.agentHeight >= agent.height, d.name + " baked clearance matches body size");
                var path = new NavMeshPath(); var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
                Check(NavMesh.CalculatePath(new Vector3(0, 0, 8), new Vector3(0, 0, 17), filter, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 2, d.name + " path routes around central wall");
            }
            Check(lab.prefabs[1].definition.speed > lab.prefabs[0].definition.speed * 2, "Sprinter exceeds twice Shambler speed");
            Check(lab.prefabs[2].animator.runtimeAnimatorController.animationClips.Any(c => c.name == "Spit") && lab.prefabs[4].animator.runtimeAnimatorController.animationClips.Any(c => c.name == "Vomit"), "special ability clips imported");
            Stage = "Actual animated bone movement";
            var bones = lab.displays.Select((e, i) => e.GetComponentsInChildren<Transform>().First(t => t.name == (i >= 5 ? "Segment1" : "ThighL"))).ToArray();
            foreach (var e in lab.displays) { e.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; e.Play("Move", true); }
            yield return new WaitForSeconds(.12f); var before = bones.Select(t => t.localRotation).ToArray();
            var movement = new float[bones.Length];
            for (int sample = 0; sample < 7; sample++)
            {
                yield return new WaitForSeconds(.09f);
                for (int i = 0; i < bones.Length; i++) movement[i] = Mathf.Max(movement[i], Quaternion.Angle(before[i], bones[i].localRotation));
            }
            for (int i = 0; i < bones.Length; i++) Check(movement[i] > 5, lab.prefabs[i].name + " rig actually animates in Play (" + movement[i].ToString("F1") + " degrees)");
            foreach (var e in lab.displays) e.Play("Idle", true);
            Stage = "Melee timing and cancellation"; yield return Clean();
            lab.dummy.transform.position = new Vector3(-6, 0, 17);
            var enemy = Spawn(0, new Vector3(-6, 0, 16));
            yield return new WaitForSeconds(.16f); Check(lab.dummy.Hits == 0 && enemy.State == "Melee", "melee wind-up does not apply instant damage");
            yield return new WaitForSeconds(.43f); Check(lab.dummy.Hits == 1 && enemy.MeleeImpacts == 1, "one damage event at melee impact");
            enemy.Health.TakeDamage(99999); int hits = lab.dummy.Hits;
            yield return new WaitForSeconds(1.3f); Check(lab.dummy.Hits == hits && enemy.State == "Dead", "dead enemy stops attacking and disables collision");
            yield return Clean(); lab.dummy.transform.position = new Vector3(-6, 0, 17);
            enemy = Spawn(0, new Vector3(-6, 0, 16)); yield return new WaitForSeconds(.13f);
            lab.dummy.transform.position += Vector3.forward * 5;
            yield return new WaitForSeconds(.5f); Check(lab.dummy.Hits == 0, "moving out of reach avoids committed melee strike");
            yield return Clean(); lab.dummy.transform.position = new Vector3(0, 0, 14);
            enemy = Spawn(2, new Vector3(0, 0, 12)); yield return null;
            Check(!enemy.HasLineOfSight(), "solid wall blocks direct attacks");
            Stage = "Live navigation around obstacle"; yield return Clean();
            lab.dummy.transform.position = new Vector3(0, 0, 17); enemy = Spawn(1, new Vector3(0, 0, 8));
            float deadline = Time.time + 9;
            while (Time.time < deadline && enemy.MeleeImpacts == 0) yield return null;
            Check(enemy.MeleeImpacts > 0 && enemy.transform.position.z > 15, "Sprinter navigates around wall and reaches target");
            Stage = "Spit projectile, damage and pool lifetime"; yield return Clean();
            lab.dummy.transform.position = new Vector3(-6, 0, 19); enemy = Spawn(2, new Vector3(-6, 0, 10));
            deadline = Time.time + 5;
            while (Time.time < deadline && AcidPool.ActiveCount == 0) yield return null;
            Check(enemy.RangedAttacks > 0 && AcidPool.ActiveCount > 0, "Spitter releases projectile and creates acid pool");
            Check(lab.dummy.TotalDamage >= enemy.definition.acidImpactDamage, "acid projectile damages target");
            var pool = FindAnyObjectByType<AcidPool>();
            Check(pool != null && pool.transform.position.y < .2f, "acid lands on floor, not on top of target");
            if (pool != null) lab.dummy.transform.position = new Vector3(pool.transform.position.x, 0, pool.transform.position.z);
            float damage = lab.dummy.TotalDamage; Destroy(enemy.gameObject);
            yield return new WaitForSeconds(.65f); Check(lab.dummy.TotalDamage > damage, "acid pool applies damage over time");
            yield return new WaitForSeconds(5.3f); Check(AcidPool.ActiveCount == 0 && AcidProjectile.ActiveCount == 0, "acid effects expire and release budget");
            Stage = "Bloater death parasites"; yield return Clean();
            lab.dummy.transform.position = new Vector3(-6, 0, 20); enemy = Spawn(3, new Vector3(-6, 0, 15)); yield return null;
            enemy.Health.TakeDamage(99999); enemy.Health.TakeDamage(99999); yield return null;
            Check(enemy.ParasitesCreated == 4 && EnemyBrain.Living.Count(e => e.definition.kind == EnemyKind.Worm) == 4, "Bloater releases four Worms exactly once");
            var worm = EnemyBrain.Living.FirstOrDefault(e => e.definition.kind == EnemyKind.Worm); Vector3 wormStart = worm != null ? worm.transform.position : Vector3.zero;
            yield return new WaitForSeconds(.8f); Check(worm != null && Vector3.Distance(wormStart, worm.transform.position) > .4f && worm.target == lab.dummy, "spawned Worm inherits target and navigates");
            Stage = "Boss vomit and capped parasite spawning"; yield return Clean();
            lab.dummy.transform.position = new Vector3(-7, 0, 21); enemy = Spawn(4, new Vector3(-7, 0, 9));
            deadline = Time.time + 5;
            while (Time.time < deadline && enemy.ParasitesCreated == 0) yield return null;
            Check(enemy.RangedAttacks == 1 && enemy.ParasitesCreated == 3, "Regurgitator vomits acid and three Puke Worms");
            Check(EnemyBrain.Living.Where(e => e.definition.kind == EnemyKind.PukeWorm).All(e => e.target == lab.dummy), "Puke Worms inherit explicit target");
            enemy.Health.TakeDamage(99999); yield return null;
            Check(enemy.ParasitesCreated == 9, "boss death adds six Puke Worms");
            for (int i = 0; i < 4; i++) { var boss = Spawn(4, new Vector3(6, 0, 19)); boss.Health.TakeDamage(99999); }
            yield return null;
            int count = EnemyBrain.Living.Count(e => e.definition.kind == EnemyKind.PukeWorm || e.definition.kind == EnemyKind.Worm);
            Check(count == EnemyBrain.ParasiteLimit, "simultaneous deaths obey shared 16-parasite cap");
            Stage = "Resistance and preview reset"; yield return Clean();
            enemy = Spawn(2, new Vector3(-6, 0, 10)); enemy.Health.TakeDamage(10, DamageKind.Acid);
            Check(Mathf.Abs(enemy.Health.TotalDamage - 9) < .01f, "Spitter chemical resistance applies to incoming acid");
            yield return Clean(); Check(EnemyBrain.Living.Count == 0 && AcidPool.ActiveCount == 0, "preview reset cleans live actors and effects");
            Stage = "Input System preview controls";
            var originalBackground = InputSystem.settings.backgroundBehavior;
            var originalRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit5)); yield return null; yield return null;
            Check(lab.Selected == 4, "number key selects boss through Input System");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); yield return null; yield return null;
            Check(lab.Demo && lab.Active != null && lab.Active.definition.kind == EnemyKind.Regurgitator, "TAB starts selected behavior demo");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); Vector3 start = lab.dummy.transform.position;
            yield return new WaitForSeconds(.3f); Check(lab.dummy.transform.position.z > start.z + .5f, "WASD moves training target");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit0)); yield return null; yield return null;
            Check(!lab.Demo && lab.Active == null, "zero returns to gallery and clears demo");
            InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = originalBackground; InputSystem.settings.editorInputBehaviorInPlayMode = originalRouting;
            lab.ShowLineup(); Stage = "Complete"; Done = true; Save();
            Debug.Log($"Enemy checks finished: {Passed} passed, {Failed} failed.");
        }
    }
}
#endif
