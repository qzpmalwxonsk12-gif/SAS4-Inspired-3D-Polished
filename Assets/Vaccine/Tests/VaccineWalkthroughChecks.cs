#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Vaccine
{
    [DefaultExecutionOrder(-50)]
    public sealed class VaccineWalkthroughChecks : MonoBehaviour
    {
        public bool Done;
        public int Passed, Failed;
        public string Stage;
        VaccineWalkthrough player;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode routing;
        readonly List<string> lines = new List<string>();
        bool walking, cleaned;
        Vector3 destination;
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play required");
            new GameObject("Temporary Vaccine route checks").AddComponent<VaccineWalkthroughChecks>();
        }
        void Save()
        {
            Directory.CreateDirectory("Artifacts/Vaccine");
            File.WriteAllText("Artifacts/Vaccine/walkthrough-checks.txt", $"Completed: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n" + string.Join("\n", lines));
        }
        void Check(bool pass, string value) { if (pass) Passed++; else Failed++; lines.Add((pass ? "PASS " : "FAIL ") + value); Save(); }
        void Update()
        {
            if (player == null || cleaned) return;
            if (!player.MapVisible) player.Capture(true);
            if (!walking) return;
            Vector3 d = destination - player.transform.position; d.y = 0;
            if (d.sqrMagnitude > .01f) player.transform.rotation = Quaternion.LookRotation(d);
        }
        void Place(Vector3 p, float yaw)
        {
            var c = player.GetComponent<CharacterController>(); c.enabled = false;
            player.transform.SetPositionAndRotation(p + Vector3.up * .08f, Quaternion.Euler(0, yaw, 0)); c.enabled = true;
            Physics.SyncTransforms(); player.Capture(true);
        }
        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        IEnumerator WalkTo(Vector3 point, string label)
        {
            Stage = label; destination = point; walking = true;
            float start = Time.realtimeSinceStartup, limit = 7 + Vector3.Distance(player.transform.position, point) / player.walkSpeed * 2;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            while (Time.realtimeSinceStartup - start < limit)
            {
                Vector3 delta = player.transform.position - point; delta.y = 0;
                if (delta.magnitude < .65f) break;
                yield return null;
            }
            walking = false; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            Vector3 diff = player.transform.position - point; diff.y = 0;
            Check(diff.magnitude < .9f && Mathf.Abs(player.transform.position.y - point.y) < .6f,
                label + " reached " + player.transform.position.ToString("F2") + " expected " + point.ToString("F2"));
        }
        IEnumerator Start()
        {
            player = FindAnyObjectByType<VaccineWalkthrough>();
            background = InputSystem.settings.backgroundBehavior; routing = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); player.Actions.devices = new InputDevice[] { keyboard, mouse };
            yield return null;
            var lot = GameObject.Find("Parking lot - ship-sized clear floor").GetComponent<Collider>().bounds;
            Check(Mathf.Abs(lot.size.x - 66) < .01f && Mathf.Abs(lot.size.z - 39) < .01f, "Parking floor is 66 x 39 metres");
            bool revised=GameObject.Find("13 Narrow station and crash collapse")!=null;
            if(revised)
            {
                var corridor=GameObject.Find("East station corridor floor").GetComponent<Collider>().bounds;
                var passage=GameObject.Find("South passage floor").GetComponent<Collider>().bounds;
                Check(Mathf.Abs(corridor.size.x-10.8f)<.01f&&Mathf.Abs(passage.size.z-corridor.size.x)<.01f,"Corridor and passage both have 10.8m nominal width");
                Check(Mathf.Abs(GameObject.Find("Electrical room floor").GetComponent<Collider>().bounds.size.x-28)<.01f,"Electrical room retains its full 28m interior footprint");
                Check(Mathf.Abs(GameObject.Find("West yard boundary").transform.position.x+22)<.01f,"Cargo boundary leaves equal 5.8m frontage either side of the electrical door");
                Check(Physics.Linecast(new Vector3(19.25f,1.65f,-2),new Vector3(19.25f,1.65f,2),out var glassHit)&&glassHit.collider.name.StartsWith("Panoramic glass"),"Panoramic ticket-hall glass blocks player passage");
            }
            Check(FindObjectsByType<ShipEnemies.EnemyBrain>().Length == 0 && FindObjectsByType<Onslaught.Mission.MissionDirector>().Length == 0,
                "Review scene has no enemy AI or mission combat");
            var doors = FindObjectsByType<ReviewDoor>(); Check(doors.Length == 4, "Electrical room plus three waiting room inspection doors");
            foreach (var door in doors)
            {
                float width = door.roomName == "Electrical room" ? 2.4f : 2;
                var from = door.transform.TransformPoint(new Vector3(width * .5f, 0, -2.5f));
                Place(from, door.transform.eulerAngles.y);
                yield return null; yield return Press(Key.E); yield return new WaitForSeconds(.65f);
                Check(door.Open, "E opens " + door.roomName);
            }
            yield return Press(Key.Tab); Check(player.MapVisible && player.overview.enabled && !player.roofs.activeSelf, "Tab reveals roofless overhead plan");
            yield return Press(Key.Tab); Check(!player.MapVisible && player.eye.enabled && player.roofs.activeSelf, "Tab restores first person and roofs");
            yield return Press(Key.Enter); Check(Vector3.Distance(player.transform.position, new Vector3(-35, 0, -19)) < .5f, "Enter resets to helicopter parking lot");
            var plane = GameObject.Find("04 Crashed aircraft - belly on ground - no landing gear").transform;
            bool tail = Physics.Raycast(plane.TransformPoint(new Vector3(0, .02f, 2)), Vector3.up, out var tailHit, 10);
            bool belly = Physics.Raycast(plane.TransformPoint(new Vector3(0, .02f, 25)), Vector3.up, out var bellyHit, 10);
            Check(tail && tailHit.distance > 2.5f, "Upswept tail has real standing-height clearance: " + tailHit.distance.ToString("F2") + "m");
            Check(belly && bellyHit.distance < .15f, "Main fuselage rests against ground: " + bellyHit.distance.ToString("F2") + "m underside clearance");
            if(revised)
            {
                Check(!Physics.Linecast(plane.TransformPoint(new Vector3(8,3,46)),plane.TransformPoint(new Vector3(0,3,46))),"Large forward fuselage tear is an actual opening");
                Check(Physics.Linecast(plane.TransformPoint(new Vector3(-2,1,3)),plane.TransformPoint(new Vector3(-8,1,3)),out var backHit)&&backHit.collider.name.StartsWith("Invisible backstop"),"Far-side route is blocked behind the tail staging pocket");
                var stages=GameObject.Find("Four zombie staging positions - no active spawners").transform.Cast<Transform>().ToArray();
                Check(stages.Length==4&&stages.All(t=>!Physics.CheckCapsule(t.position+Vector3.up*.4f,t.position+Vector3.up*1.5f,.32f)),"Four zombie-sized capsules fit beneath the tail without overlap with scenery");
                Check(Physics.Linecast(new Vector3(45,1,15.8f),new Vector3(53,1,17.5f)),"Former breach beside aircraft nose is sealed");
            }
            Place(plane.TransformPoint(new Vector3(4, 0, 2)), 0);
            yield return WalkTo(plane.TransformPoint(new Vector3(0, 0, 2)), "Walk beneath raised tail");
            player.ResetPosition(); yield return null;
            var route = GameObject.Find("Review route checkpoints").transform.Cast<Transform>().OrderBy(t => t.name).ToArray();
            foreach (var point in route.Skip(1))
            {
                yield return WalkTo(point.position, point.name);
                if (Failed > 0) { Stage = "Stopped at failed route check"; Done = true; Save(); Cleanup(); yield break; }
            }
            if(GameObject.Find("15 Deep subway and connected trains"))
            {for(int i=route.Length-2;i>=19;i--)yield return WalkTo(route[i].position,"Climb three-flight route "+i);}
            else
            {
                yield return WalkTo(route[20].position, "Lower landing return");
                yield return WalkTo(route[19].position, "Climb back up all twenty steps");
            }
            Check(Physics.Linecast(new Vector3(9, 1, -34), new Vector3(2, 1, -34)), "First stair branch is physically blocked by rubble");
            Check(Physics.Linecast(new Vector3(60, 1, 15), new Vector3(60, 1, 23)), "Crash rubble blocks onward station corridor");
            Check(Physics.Linecast(new Vector3(-64, 1, -20), new Vector3(-68, 1, -20)), "Parking perimeter prevents leaving the map");
            Done = true; Stage = "Complete"; Save(); Cleanup();
        }
        void Cleanup()
        {
            if (cleaned || player == null) return; cleaned = true; walking = false;
            player.Actions.devices = null;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = routing;
        }
        void OnDestroy() => Cleanup();
    }
}
#endif
