#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ShipEnemies;

namespace Onslaught.Mission
{
    // Runs before weapons so the real controller moves first, then the test keeps aim on the target.
    [DefaultExecutionOrder(50)]
    public sealed class ShootingMotionChecks : MonoBehaviour
    {
        public bool Done;
        public int Passed, Failed;
        public string Stage;
        readonly List<string> results = new List<string>();
        readonly List<GameObject> fixtures = new List<GameObject>();
        MissionDirector mission;
        PlayerWeapons weapons;
        EnemyBrain target;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode routing;
        float captureDelta, nextSpawn;
        bool cleaned, observing;
        int lastShots, selfHits, frames;
        float deltaSum;
        string rawHit;
        string report;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Begin(string report = "Artifacts/shooting-motion-checks.txt")
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play required");
            var check = new GameObject("Temporary shooting motion checks").AddComponent<ShootingMotionChecks>();
            check.report = report;
        }
        void Save() => File.WriteAllText(report, $"Completed: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n" + string.Join("\n", results));
        void Check(bool valid, string message) { if (valid) Passed++; else Failed++; results.Add((valid ? "PASS " : "FAIL ") + message); Save(); }
        void Update()
        {
            if (cleaned || mission == null || target == null) return;
            mission.explorer.Capture(true);
            var eye = mission.explorer.eye.transform;
            eye.rotation = Quaternion.LookRotation(target.transform.position + Vector3.up * 1.25f - eye.position);
            if (!observing) return;
            frames++; deltaSum += Time.deltaTime;
            rawHit = Physics.Raycast(eye.position, eye.forward, out var hit, weapons.Current.range,
                Physics.DefaultRaycastLayers & ~(1 << weapons.gateMovementLayer), QueryTriggerInteraction.Ignore) ? hit.collider.name : "none";
        }
        void LateUpdate()
        {
            if (!observing || weapons.ShotsFired == lastShots) return;
            lastShots = weapons.ShotsFired;
            float distance = Vector3.Distance(mission.explorer.eye.transform.position, weapons.tracer.GetPosition(1));
            if (rawHit == mission.explorer.name) selfHits++;
            results.Add($"  shot {lastShots}: unfiltered hit={rawHit}, tracer distance={distance:F3}");
        }
        void MovePlayer()
        {
            var controller = mission.explorer.GetComponent<CharacterController>();
            controller.enabled = false;
            mission.explorer.transform.SetPositionAndRotation(new Vector3(12, .08f, 4), Quaternion.Euler(0, 90, 0));
            controller.enabled = true;
            Physics.SyncTransforms();
        }
        IEnumerator Burst(string label, int fps, params Key[] keys)
        {
            Stage = label; observing = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null; yield return null;
            MovePlayer();
            Time.captureDeltaTime = 1f / fps;
            yield return new WaitForSeconds(.35f);
            int shots = weapons.ShotsFired, hits = weapons.Hits;
            lastShots = shots; selfHits = 0; frames = 0; deltaSum = 0;
            var from = mission.explorer.transform.position;
            observing = true;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return new WaitForSeconds(.7f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null; yield return null;
            observing = false;
            int fired = weapons.ShotsFired - shots, landed = weapons.Hits - hits;
            float moved = Vector3.Distance(from, mission.explorer.transform.position);
            Check(fired > 0 && landed == fired && (keys.Length == 0 || moved > 1),
                $"{label}: {landed}/{fired} enemy hits; moved {moved:F2}m; unfiltered self hits {selfHits}; average delta {deltaSum / Mathf.Max(1, frames):F4}s");
            if (weapons.Rounds < 8) { weapons.BeginReload(); yield return new WaitForSeconds(2.3f); }
        }
        IEnumerator Start()
        {
            mission = MissionDirector.Instance; weapons = mission.weapons;
            nextSpawn = (float)typeof(MissionDirector).GetField("nextSpawn", Private).GetValue(mission);
            typeof(MissionDirector).GetField("nextSpawn", Private).SetValue(mission, float.PositiveInfinity);
            background = InputSystem.settings.backgroundBehavior; routing = InputSystem.settings.editorInputBehaviorInPlayMode;
            captureDelta = Time.captureDeltaTime;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            var devices = new InputDevice[] { keyboard, mouse };
            mission.Actions.devices = devices; weapons.Actions.devices = devices; mission.explorer.Actions.devices = devices;
            target = mission.Spawn(0, 0); target.displayOnly = true; target.Agent.enabled = false;
            target.transform.position = new Vector3(17, 0, 4); target.Health.maximum = 100000; target.Health.ResetHealth();
            weapons.Select(1);
            yield return Burst("Rifle stationary", 60);
            yield return Burst("Rifle backward 60", 60, Key.S);
            yield return Burst("Rifle backward 30", 30, Key.S);
            yield return Burst("Rifle backward 15", 15, Key.S);
            yield return Burst("Rifle sprint backward 15", 15, Key.S, Key.LeftShift);
            yield return Burst("Rifle forward 15", 15, Key.W);
            yield return Burst("Rifle strafe left 30", 30, Key.A);
            yield return Burst("Rifle strafe right 30", 30, Key.D);
            weapons.Select(0);
            yield return Burst("Pistol backward 15", 15, Key.S);
            yield return Burst("Pistol sprint backward 15", 15, Key.S, Key.LeftShift);
            yield return CoverChecks();
            Done = true; Stage = "Complete"; Save(); Cleanup();
        }
        GameObject Box(string name, float x, bool owned)
        {
            var box = new GameObject(name);
            if (owned) box.transform.SetParent(mission.explorer.transform);
            box.transform.position = new Vector3(x, 1, 4);
            box.AddComponent<BoxCollider>().size = new Vector3(.06f, 2, 1);
            fixtures.Add(box);
            return box;
        }
        IEnumerator SingleShot()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return new WaitForSeconds(.4f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null; yield return null;
        }
        IEnumerator CoverChecks()
        {
            Stage = "Owner exclusion and solid cover";
            Time.captureDeltaTime = 1f / 60;
            MovePlayer();
            var child = Box("Test owner attachment", 13, true);
            Physics.SyncTransforms();
            int shots = weapons.ShotsFired, hits = weapons.Hits;
            yield return SingleShot();
            Check(weapons.ShotsFired == shots + 1 && weapons.Hits == hits + 1,
                "Shot excludes colliders on child objects belonging to the player");
            var wall = Box("Test solid cover", 15, false);
            Physics.SyncTransforms();
            shots = weapons.ShotsFired; hits = weapons.Hits;
            yield return SingleShot();
            Check(weapons.ShotsFired == shots + 1 && weapons.Hits == hits && Mathf.Abs(weapons.tracer.GetPosition(1).x - 14.97f) < .01f,
                "Nearest solid cover still stops shots with owner collider in front");
            for (int i = 0; i < 24; i++) Box("Test crowded owner attachment", 12.5f + i * .08f, true);
            Physics.SyncTransforms();
            shots = weapons.ShotsFired; hits = weapons.Hits;
            yield return SingleShot();
            Check(weapons.ShotsFired == shots + 1 && weapons.Hits == hits && Mathf.Abs(weapons.tracer.GetPosition(1).x - 14.97f) < .01f,
                "Overflow beyond 16 ray hits still finds solid cover rather than shooting through it");
            Destroy(wall); yield return null;
            Physics.SyncTransforms();
            shots = weapons.ShotsFired; hits = weapons.Hits;
            yield return SingleShot();
            Check(weapons.ShotsFired == shots + 1 && weapons.Hits == hits + 1,
                "Overflow still hits enemy behind all owner attachments when cover is removed");
            Check(mission.player.gameObject.layer == 0 && mission.player.receivesEnemyAttacks && mission.explorer.GetComponent<CharacterController>().enabled,
                "Player remains physically collidable and available to enemy damage");
        }
        void Cleanup()
        {
            if (cleaned || mission == null) return;
            cleaned = true; observing = false;
            mission.Actions.devices = null; weapons.Actions.devices = null; mission.explorer.Actions.devices = null;
            typeof(MissionDirector).GetField("nextSpawn", Private).SetValue(mission, nextSpawn);
            if (target != null) Destroy(target.gameObject);
            foreach (var fixture in fixtures) if (fixture != null) Destroy(fixture);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = routing;
            Time.captureDeltaTime = captureDelta;
        }
        void OnDestroy() => Cleanup();
    }
}
#endif
