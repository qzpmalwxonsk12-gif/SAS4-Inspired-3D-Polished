using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Vaccine.Editor
{
    public static class VaccineBlockoutBuilder
    {
        public const string Root = "Assets/Vaccine";
        public const string ScenePath = Root + "/Scenes/Vaccine_Blockout.unity";
        static Material ground, concrete, sheet, dark, prop, aircraft, rubble, white;
        static Transform lot, yard, electrical, station, rooms, plane, roofs, labels;
        static int meshIndex;

        [MenuItem("Vaccine/Create Separate Vaccine Blockout")]
        public static string Create()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Vaccine already exists; edit its saved scene objects.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
            foreach (var dir in new[] { "Scenes", "Materials", "Geometry" }) Directory.CreateDirectory(Root + "/" + dir);
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ground = Mat("Ground", .38f); concrete = Mat("Concrete", .65f); sheet = Mat("Sheet metal", .52f);
            dark = Mat("Dark recess", .16f); prop = Mat("Fixtures", .42f); aircraft = Mat("Aircraft skin", .73f);
            rubble = Mat("Rubble", .47f); white = Mat("White markings", .88f);
            lot = Group("01 Parking lot - clear interior 66 x 39 metres");
            yard = Group("02 Outdoor crash courtyard"); electrical = Group("03 Backup electrical room");
            station = Group("05 Station ticket hall and corridor"); rooms = Group("06 Three waiting rooms");
            roofs = Group("08 Roofs - hidden for overhead review"); labels = Group("09 Overhead labels");
            Parking(); Courtyard(); ElectricalRoom(); Aircraft(); Station(); WaitingRooms(); Stairs();
            Lighting(); Explorer(); Route();
            PlanLabel("PARKING LOT\n66 x 39 m", new Vector3(-33, 19, -30), 22);
            PlanLabel("CRASH COURTYARD", new Vector3(6, 19, 10), 28);
            PlanLabel("BACKUP POWER", new Vector3(-23, 19, 40), 24);
            PlanLabel("TAIL GAP", new Vector3(-3, 19, 31), 12);
            PlanLabel("TICKET HALL", new Vector3(17, 19, -11), 22);
            PlanLabel("BLOCKED", new Vector3(4, 19, -35), 12);
            PlanLabel("WAITING ROOMS", new Vector3(78, 19, -16), 20);
            PlanLabel("NO THROUGH ROUTE", new Vector3(59, 19, 21), 25);
            PlanLabel("STAIRS DOWN", new Vector3(79, 19, 11), 22);
            PlanLabel("LANDING", new Vector3(90, 19, 10.65f), 11);
            labels.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            var view = SceneView.lastActiveSceneView;
            if (view != null) view.LookAt(new Vector3(12, 0, 4), Quaternion.Euler(62, 0, 0), 110);
            Selection.activeGameObject = GameObject.Find("Vaccine walkthrough player");
            return ScenePath;
        }

        static Material Mat(string name, float grey)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", new Color(grey, grey, grey)); m.SetFloat("_Smoothness", .05f);
            AssetDatabase.CreateAsset(m, Root + "/Materials/" + name + ".mat"); return m;
        }
        static Transform Group(string name, Transform parent = null)
        { var t = new GameObject(name).transform; if (parent != null) t.SetParent(parent, false); return t; }
        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); go.isStatic = true; return go;
        }
        static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); go.isStatic = true; return go;
        }
        static void Floor(string name, Transform parent, float x0, float x1, float z0, float z1, float top = 0, Material material = null)
            => Box(name, parent, new Vector3((x0 + x1) / 2, top - .25f, (z0 + z1) / 2), new Vector3(x1 - x0, .5f, z1 - z0), material ?? ground);
        static void Wall(string name, Transform parent, Vector3 a, Vector3 b, float height = 5, float thick = .35f, Material material = null)
        {
            var go = Box(name, parent, (a + b) * .5f + Vector3.up * height / 2, new Vector3(thick, height, Vector3.Distance(a, b)), material ?? concrete);
            go.transform.rotation = Quaternion.LookRotation(b - a);
            CapWall(go);
        }
        static void CapWall(GameObject wall)
        {
            if (wall.transform.Find("Dark wall cap") != null) return;
            Box("Dark wall cap", wall.transform, new Vector3(0, .502f, 0), new Vector3(1.04f, .015f, 1.004f), dark, false);
        }
        public static void ImprovePlanReadability()
        {
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open Vaccine outside Play mode.");
            dark = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Dark recess.mat");
            foreach (var c in Object.FindObjectsByType<BoxCollider>())
            {
                var s = c.transform.localScale;
                if (s.x < .8f && s.y >= 3 && s.z >= 2 && !c.name.Contains("sheet barricade")) CapWall(c.gameObject);
            }
            var labelRoot = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "09 Overhead labels");
            foreach (var t in labelRoot.GetComponentsInChildren<TextMeshPro>(true)) t.fontSize = 16;
            var sign = GameObject.Find("TAIL OPENING"); if (sign != null) Object.DestroyImmediate(sign);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        static void Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, Material material, bool solid = true)
        {
            var go = Box(name, parent, (a + b) * .5f, new Vector3(width, width, Vector3.Distance(a, b)), material, solid);
            go.transform.localRotation = Quaternion.LookRotation(b - a);
        }
        static TextMeshPro Text(string name, Transform parent, string value, Vector3 p, float width, float height, float fontSize, Quaternion rotation)
        {
            var text = new GameObject(name).AddComponent<TextMeshPro>(); text.transform.SetParent(parent, false);
            text.transform.localPosition = p; text.transform.localRotation = rotation; text.rectTransform.sizeDelta = new Vector2(width, height);
            text.font = TMP_Settings.defaultFontAsset; text.text = value; text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
        static void Sign(string value, Transform parent, Vector3 position, float width, float yaw = 0)
        {
            var g = Group(value, parent); g.position = position; g.rotation = Quaternion.Euler(0, yaw, 0);
            Box("Sign backing", g, Vector3.zero, new Vector3(width, .9f, .12f), dark, false);
            Text("Sign text", g, value, new Vector3(0, 0, -.071f), width - .2f, .8f, 3.1f, Quaternion.identity);
        }
        static void PlanLabel(string value, Vector3 p, float width)
            => Text(value, labels, value, p, width, 5, 16, Quaternion.Euler(90, 0, 0));

        static void Fence(Vector3 a, Vector3 b)
        {
            float length = Vector3.Distance(a, b); int n = Mathf.CeilToInt(length / 3); Vector3 direction = (b - a).normalized;
            for (int i = 0; i < n; i++)
            {
                var panel = Box("Overlapping sheet barricade", lot, Vector3.Lerp(a, b, (i + .5f) / n) + Vector3.up * 1.7f,
                    new Vector3(.12f, 3.4f, length / n + .06f), i % 3 == 0 ? prop : sheet);
                panel.transform.rotation = Quaternion.LookRotation(direction);
            }
            for (int i = 0; i <= n; i++) Box("Barricade support", lot, Vector3.Lerp(a, b, (float)i / n) + Vector3.up * 1.5f, new Vector3(.2f, 3, .2f), dark);
        }
        static void Parking()
        {
            Floor("Parking lot - ship-sized clear floor", lot, -66, 0, -39, 0);
            Fence(new Vector3(-66.2f, 0, -39.2f), new Vector3(-66.2f, 0, .2f));
            Fence(new Vector3(-66.2f, 0, -39.2f), new Vector3(-.2f, 0, -39.2f));
            Fence(new Vector3(.2f, 0, -39), new Vector3(.2f, 0, 0));
            Fence(new Vector3(-66.2f, 0, .2f), new Vector3(-10, 0, .2f));
            Fence(new Vector3(-2, 0, .2f), new Vector3(.2f, 0, .2f));
            Sign("COURTYARD", lot, new Vector3(-6, 3.5f, .2f), 7);
            Beam("Exit lintel", lot, new Vector3(-10, 3.8f, 0), new Vector3(-2, 3.8f, 0), .2f, dark);
            for (int i = 0; i < 7; i++) Box("Parking bay stripe", lot, new Vector3(-59 + i * 7, .012f, -33), new Vector3(.1f, .02f, 8), white, false);
            for (int i = 0; i < 4; i++) Box("Parking bay stripe", lot, new Vector3(-60 + i * 7, .012f, -6), new Vector3(.1f, .02f, 8), white, false);
            Car("Abandoned sedan", new Vector3(-56, 0, -33), 0, false);
            Car("Abandoned van", new Vector3(-34, 0, -33), -8, false);
            Car("Burned wreck", new Vector3(-18, 0, -32), 20, true);
            Car("Overturned wreck", new Vector3(-56, .3f, -6), 70, true);
            Helicopter(new Vector3(-44, 0, -18));
            Hands(new Vector3(-65.9f, 1.4f, -12), 90); Hands(new Vector3(-65.9f, 1.6f, -27), 90);
            Hands(new Vector3(-28, 1.5f, -.05f), 180);
        }
        static void Car(string name, Vector3 p, float yaw, bool wreck)
        {
            var g = Group(name, lot); g.position = p; g.rotation = Quaternion.Euler(0, yaw, wreck ? 8 : 0);
            Box("Chassis", g, new Vector3(0, .65f, 0), new Vector3(2.1f, .85f, 4.8f), prop);
            Box("Cabin", g, new Vector3(0, 1.35f, -.2f), new Vector3(1.85f, .8f, 2.5f), wreck ? dark : sheet);
            Box("Windshield", g, new Vector3(0, 1.43f, 1.07f), new Vector3(1.55f, .48f, .05f), dark, false);
            foreach (float x in new[] { -1.05f, 1.05f }) foreach (float z in new[] { -1.5f, 1.5f })
            {
                var wheel = Shape(PrimitiveType.Cylinder, "Wheel", g, new Vector3(x, .44f, z), new Vector3(.85f, .18f, .85f), dark);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            if (wreck) Box("Detached bonnet", lot, p + new Vector3(3, .25f, 1), new Vector3(2, .15f, 1.6f), sheet).transform.Rotate(12, 32, 8);
        }
        static void Helicopter(Vector3 p)
        {
            var g = Group("Stranded helicopter", lot); g.position = p; g.rotation = Quaternion.Euler(0, 30, 0);
            Shape(PrimitiveType.Capsule, "Cabin fuselage", g, new Vector3(0, 2.3f, 0), new Vector3(3.4f, 3.7f, 3.4f), prop).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Box("Cockpit glazing", g, new Vector3(0, 2.6f, 2.7f), new Vector3(2.5f, 1.4f, .65f), dark);
            Beam("Tail boom", g, new Vector3(0, 2.6f, -2.7f), new Vector3(0, 3.1f, -10), .65f, sheet);
            Box("Tail fin", g, new Vector3(0, 4, -9.8f), new Vector3(.2f, 2.7f, 1.8f), sheet);
            Box("Rotor mast", g, new Vector3(0, 4.8f, -.2f), new Vector3(.22f, 1.4f, .22f), dark);
            Box("Main rotor", g, new Vector3(0, 5.5f, -.2f), new Vector3(14, .12f, .48f), dark, false);
            Box("Main rotor crossing blade", g, new Vector3(0, 5.5f, -.2f), new Vector3(.48f, .12f, 14), dark, false);
            foreach (float x in new[] { -1.7f, 1.7f })
            {
                Beam("Landing skid", g, new Vector3(x, .22f, -2.6f), new Vector3(x, .22f, 2.9f), .16f, dark);
                foreach (float z in new[] { -1.7f, 1.7f }) Beam("Skid brace", g, new Vector3(x, .22f, z), new Vector3(x * .6f, 1.2f, z), .15f, dark);
            }
            Sign("STRANDED / NO FUEL", lot, p + new Vector3(0, .8f, -5), 7);
        }
        static void Hands(Vector3 p, float yaw)
        {
            var g = Group("Undead hands through seam - static blockout", lot); g.position = p; g.rotation = Quaternion.Euler(0, yaw, 0);
            foreach (float x in new[] { -.22f, .25f })
            {
                Beam("Forearm", g, new Vector3(x, 0, -.4f), new Vector3(x, .12f, .2f), .14f, dark, false);
                Box("Palm", g, new Vector3(x, .15f, .23f), new Vector3(.18f, .1f, .22f), prop, false);
                for (int i = 0; i < 4; i++) Beam("Reaching finger", g, new Vector3(x - .07f + i * .045f, .15f, .32f), new Vector3(x - .07f + i * .045f, .23f, .47f), .035f, prop, false);
            }
        }

        static void Courtyard()
        {
            Floor("Courtyard", yard, -38, 50, 0, 50);
            Floor("Exterior approach to station entrance", yard, 33, 50, -28, 0);
            Wall("West yard boundary", yard, new Vector3(-38, 0, 0), new Vector3(-38, 0, 50), 4);
            Wall("North yard boundary", yard, new Vector3(-38, 0, 50), new Vector3(50, 0, 50), 4);
            Wall("East yard boundary", yard, new Vector3(50, 0, 24), new Vector3(50, 0, 50), 4);
            foreach (float z in new[] { 7f, 19f })
            {
                Box("Cargo container", yard, new Vector3(-34, 1.6f, z), new Vector3(6, 3.2f, 9), prop);
                for (int i = 0; i < 7; i++) Box("Container ribs", yard, new Vector3(-30.95f, 1.6f, z - 3.6f + i * 1.2f), new Vector3(.1f, 2.9f, .08f), dark, false);
            }
            // A wide usable route around the southern wing remains open toward the station door.
            for (int i = 0; i < 5; i++) Box("Crash debris", yard, new Vector3(34 + i * 1.9f, .3f + i % 2 * .2f, 40 - i * 1.3f), new Vector3(2, .6f, 1.5f), rubble).transform.Rotate(7, i * 24, 9);
        }
        static void ElectricalRoom()
        {
            Floor("Electrical room floor", electrical, -36, -8, 27, 47, .04f);
            Wall("West electrical wall", electrical, new Vector3(-36, 0, 27), new Vector3(-36, 0, 47));
            Wall("Rear electrical wall", electrical, new Vector3(-36, 0, 47), new Vector3(-8, 0, 47));
            Wall("Damaged east wall lower section", electrical, new Vector3(-8, 0, 27), new Vector3(-8, 0, 47), 2.5f);
            Wall("Electrical frontage left", electrical, new Vector3(-36, 0, 27), new Vector3(-16.2f, 0, 27));
            Wall("Electrical frontage right", electrical, new Vector3(-13.8f, 0, 27), new Vector3(-8, 0, 27));
            Box("Electrical door lintel", electrical, new Vector3(-15, 4, 27), new Vector3(2.4f, 2, .35f), concrete);
            Door("Electrical room", electrical, new Vector3(-16.2f, 0, 27), 0, 2.4f);
            Sign("BACKUP POWER", electrical, new Vector3(-15, 3.35f, 26.75f), 8);
            Floor("Intact electrical roof", roofs, -36, -17, 27, 47, 5.25f, concrete);
            for (int i = 0; i < 5; i++)
            {
                var beam = Box("Snapped roof edge", electrical, new Vector3(-16 + i * .9f, 4.8f - i * .4f, 29 + i * 3.4f), new Vector3(4, .45f, 2.1f), rubble);
                beam.transform.Rotate(i * 8, i * 13, -12 - i * 8);
            }
            for (int i = 0; i < 5; i++) Box("Switchgear cabinet", electrical, new Vector3(-33.5f, 1.2f, 31 + i * 3), new Vector3(1.8f, 2.4f, 2), prop);
            Box("Backup generator body", electrical, new Vector3(-24, 1.2f, 41), new Vector3(7, 2.4f, 3.4f), prop);
            Box("Generator control panel", electrical, new Vector3(-24, 1.7f, 39.23f), new Vector3(2.4f, 1.3f, .12f), dark);
            Box("Power switch", electrical, new Vector3(-24, 1.7f, 39.1f), new Vector3(.18f, .65f, .18f), white);
            Sign("BACKUP GENERATOR", electrical, new Vector3(-24, 3.1f, 39.2f), 9);
        }
        static ReviewDoor Door(string name, Transform parent, Vector3 hinge, float yaw, float width = 1.8f)
        {
            var g = Group(name + " - breakable door placeholder", parent); g.position = hinge; g.rotation = Quaternion.Euler(0, yaw, 0);
            var door = g.gameObject.AddComponent<ReviewDoor>(); door.roomName = name;
            var pivot = Group("Door hinge", g); door.leaf = pivot;
            var leaf = Box("Door leaf - E to open for inspection", pivot, new Vector3(width * .5f, 1.5f, 0), new Vector3(width, 3, .14f), prop);
            leaf.isStatic = false;
            Box("Handle", pivot, new Vector3(width - .18f, 1.25f, -.13f), new Vector3(.08f, .3f, .12f), dark, false).isStatic = false;
            return door;
        }

        static void MeshObject(string name, Transform parent, Vector3[] vertices, int[] triangles, Material material)
        {
            var mesh = new Mesh { name = name }; mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Root + "/Geometry/" + (++meshIndex).ToString("D2") + "-" + name + ".asset");
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)); go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshCollider>().sharedMesh = mesh; go.isStatic = true;
        }
        static void Prism(string name, Transform parent, Vector2[] outline, float bottom, float top, Material material)
        {
            float area = 0; for (int i = 0; i < outline.Length; i++) { var a = outline[i]; var b = outline[(i + 1) % outline.Length]; area += a.x * b.y - b.x * a.y; }
            if (area < 0) Array.Reverse(outline);
            int n = outline.Length; var verts = new Vector3[n * 2]; var tris = new List<int>();
            for (int i = 0; i < n; i++) { verts[i] = new Vector3(outline[i].x, bottom, outline[i].y); verts[i + n] = new Vector3(outline[i].x, top, outline[i].y); }
            for (int i = 1; i < n - 1; i++) tris.AddRange(new[] { 0, i, i + 1, n, n + i + 1, n + i });
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; tris.AddRange(new[] { i, n + i, n + j, i, n + j, j }); }
            MeshObject(name, parent, verts, tris.ToArray(), material);
        }
        static void Aircraft()
        {
            plane = Group("04 Crashed aircraft - belly on ground - no landing gear"); plane.position = new Vector3(-8, 0, 36);
            plane.rotation = Quaternion.LookRotation(new Vector3(60, 0, -20));
            // Rings: forward distance, half width, belly height, crown height. Only the rear rises off the ground.
            var rings = new[] { new Vector4(0, 1.3f, 3.6f, 6.1f), new Vector4(6, 2.8f, 2, 6.6f), new Vector4(13, 3.5f, .08f, 6.8f),
                new Vector4(37, 3.6f, .08f, 6.8f), new Vector4(49, 3.3f, .12f, 6.2f), new Vector4(57, 2.6f, .3f, 5.2f), new Vector4(63.25f, .12f, 1.3f, 3.7f) };
            const int sides = 12; var vs = new List<Vector3>(); var ts = new List<int>();
            foreach (var r in rings) for (int j = 0; j < sides; j++)
            { float a = j * Mathf.PI * 2 / sides; vs.Add(new Vector3(Mathf.Cos(a) * r.y, (r.z + r.w) * .5f + Mathf.Sin(a) * (r.w - r.z) * .5f, r.x)); }
            for (int i = 0; i < rings.Length - 1; i++) for (int j = 0; j < sides; j++)
            { int a = i * sides + j, b = i * sides + (j + 1) % sides, c = a + sides, d = b + sides; ts.AddRange(new[] { a, b, d, a, d, c }); }
            for (int j = 1; j < sides - 1; j++) { int last = (rings.Length - 1) * sides; ts.AddRange(new[] { 0, j + 1, j, last, last + j, last + j + 1 }); }
            MeshObject("Grounded fuselage with upswept tail cavity", plane, vs.ToArray(), ts.ToArray(), aircraft);
            foreach (float side in new[] { -1f, 1f })
            {
                Prism("Main wing " + side, plane, new[] { new Vector2(side * 3, 23), new Vector2(side * 20, 36), new Vector2(side * 20, 40), new Vector2(side * 3, 37) }, 2.2f, 2.7f, aircraft);
                Prism("Tail stabilizer " + side, plane, new[] { new Vector2(side, 1), new Vector2(side * 7, 5), new Vector2(side * 7, 7), new Vector2(side, 6) }, 4.9f, 5.2f, aircraft);
                foreach (float lateral in new[] { 10f, 16f })
                {
                    float t = lateral == 10 ? 30 : 35;
                    var engine = Shape(PrimitiveType.Cylinder, "Crushed engine nacelle", plane, new Vector3(side * lateral, 1.3f, t), new Vector3(2.5f, 2.4f, 2.5f), prop);
                    engine.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var intake = Shape(PrimitiveType.Cylinder, "Dark engine inlet", plane, new Vector3(side * lateral, 1.3f, t + 2.42f), new Vector3(2.1f, .03f, 2.1f), dark, false);
                    intake.transform.localRotation = Quaternion.Euler(90, 0, 0);
                }
                Box("Cockpit glazing", plane, new Vector3(side * 1.55f, 4.45f, 57), new Vector3(.12f, .8f, 2.3f), dark, false).transform.localRotation = Quaternion.Euler(0, side * -25, 0);
                Box("Broken gear mounting stub", plane, new Vector3(side * 3.15f, .5f, 29), new Vector3(.45f, .8f, 1.3f), dark).transform.localRotation = Quaternion.Euler(0, 0, side * 35);
            }
            // Upright fin: a thin extruded side profile, separate from the actual under-tail gap.
            var fin = Group("Vertical tail fin", plane); fin.localRotation = Quaternion.Euler(0, 0, -90);
            Prism("Swept tail fin", fin, new[] { new Vector2(-6, 0), new Vector2(-14, 1.5f), new Vector2(-14, 3), new Vector2(-6, 12) }, -.22f, .22f, aircraft);
            Box("C4 placement blockout", plane, new Vector3(2.78f, 3.1f, 6), new Vector3(.14f, .45f, .65f), dark, false);
            // No box collider spans the rear void: its clearance comes from the fuselage mesh itself.
        }

        static void Station()
        {
            Floor("Ticket hall floor", station, 0, 33, -28, 0, 0, concrete);
            Floor("South passage floor", station, 0, 87, -40, -28, 0, concrete);
            Floor("East station corridor floor", station, 50, 68, -28, 24, 0, concrete);
            Wall("Ticket hall north wall", station, new Vector3(0, 0, 0), new Vector3(33, 0, 0));
            Wall("Ticket hall west wall", station, new Vector3(0, 0, -40), new Vector3(0, 0, 0));
            Wall("Entrance wall north", station, new Vector3(33, 0, 0), new Vector3(33, 0, -5));
            Wall("Entrance wall south", station, new Vector3(33, 0, -14), new Vector3(33, 0, -28));
            Box("Station entrance lintel", station, new Vector3(33, 4.5f, -9.5f), new Vector3(.35f, 1, 9), concrete);
            Sign("METRO / STATION ENTRANCE", station, new Vector3(33.25f, 3.6f, -9.5f), 8, -90);
            Wall("Exterior approach end wall", station, new Vector3(33, 0, -28), new Vector3(50, 0, -28));
            Wall("Station corridor west wall", station, new Vector3(50, 0, -28), new Vector3(50, 0, 14.5f));
            Wall("Station north boundary", station, new Vector3(50, 0, 24), new Vector3(87, 0, 24));
            Wall("Station south boundary", station, new Vector3(0, 0, -40), new Vector3(87, 0, -40));
            Box("Ticket counter", station, new Vector3(3.5f, .65f, -12), new Vector3(3, 1.3f, 15), prop);
            Box("Ticket booth back panel", station, new Vector3(1, 2, -12), new Vector3(.2f, 4, 17), dark);
            for (int i = 0; i < 3; i++) Box("Ticket window divider", station, new Vector3(3, 2.3f, -7 - i * 5), new Vector3(3, 2, .12f), sheet);
            Sign("TICKETS", station, new Vector3(5.1f, 3.5f, -12), 7, -90);
            Wall("Ticket barrier west return", station, new Vector3(0, 0, -23), new Vector3(8, 0, -23), 1.2f, .2f, dark);
            for (int i = 0; i < 7; i++)
            {
                float x = 8.5f + i * 4;
                Box("Ticket checking pedestal", station, new Vector3(x, .55f, -23), new Vector3(.65f, 1.1f, 2.1f), prop);
                Box("Ticket reader", station, new Vector3(x, 1.17f, -22.55f), new Vector3(.5f, .14f, .45f), dark, false);
            }
            Sign("PLATFORMS  >", station, new Vector3(21, 3.4f, -26), 10);
            Wall("Ticket hall south short return", station, new Vector3(0, 0, -28), new Vector3(11, 0, -28));
            Wall("Ticket hall south east return", station, new Vector3(25, 0, -28), new Vector3(33, 0, -28));
            for (int i = 0; i < 5; i++) Box("Blocked stair tread", station, new Vector3(7 - i * 1.25f, -.15f - i * .18f, -34), new Vector3(1.3f, .3f, 5), concrete);
            Box("Collapsed stairwell solid core", station, new Vector3(4, 1.45f, -34), new Vector3(5, 2.9f, 8), rubble);
            for (int i = 0; i < 9; i++) Box("Blocked stair rubble", station, new Vector3(3 + i % 3 * 1.6f, .7f + i / 3 * .8f, -37 + i % 3 * 2.4f), new Vector3(2.3f, 1.5f, 2.2f), concrete).transform.Rotate(i * 11, i * 29, i * 5);
            Sign("STAIRS CLOSED", station, new Vector3(8, 3.4f, -34), 7, 90);
            // The plane's nose penetrates the west wall here. Debris closes the entire continuation north.
            for (int i = 0; i < 7; i++)
            {
                var chunk = Box("Nose impact rubble blocks onward route", station, new Vector3(51.5f + i * 2.5f, 1.4f + (i % 3) * .4f, 18.9f + i % 2), new Vector3(3.8f, 3.4f + i % 2, 4.5f), rubble);
                chunk.transform.Rotate(i * 3, i * 17, i % 2 == 0 ? -8 : 8);
            }
            Box("Severed station wall edge", station, new Vector3(50, 3.8f, 14.7f), new Vector3(.6f, 2.4f, 2), concrete).transform.Rotate(14, 0, -12);
            Sign("PLATFORM ACCESS  >", station, new Vector3(60, 4.25f, 15.8f), 12);
            Floor("Ticket hall roof", roofs, 0, 33, -28, 0, 5.35f, concrete);
            Floor("South passage roof", roofs, 0, 87, -40, -28, 5.35f, concrete);
            Floor("Corridor roof before crash opening", roofs, 50, 68, -28, 12, 5.35f, concrete);
        }
        static void WaitingRooms()
        {
            Floor("Waiting rooms floor", rooms, 68, 87, -40, 6, 0, concrete);
            Wall("Waiting rooms exterior east wall", rooms, new Vector3(87, 0, -40), new Vector3(87, 0, 6));
            float[] bounds = { -40, -24, -9, 6 };
            for (int i = 0; i < 3; i++)
            {
                float z0 = bounds[i], z1 = bounds[i + 1], middle = (z0 + z1) * .5f;
                if (i > 0) Wall("Waiting room partition " + i, rooms, new Vector3(68, 0, z0), new Vector3(87, 0, z0));
                Wall("Waiting room " + (i + 1) + " west wall south", rooms, new Vector3(68, 0, z0), new Vector3(68, 0, middle - 1));
                Wall("Waiting room " + (i + 1) + " west wall north", rooms, new Vector3(68, 0, middle + 1), new Vector3(68, 0, z1));
                Box("Waiting room door header", rooms, new Vector3(68, 4, middle), new Vector3(.35f, 2, 2), concrete);
                Door("Waiting room " + (i + 1), rooms, new Vector3(68, 0, middle + 1), 90, 2);
                Sign("WAITING " + (i + 1), rooms, new Vector3(67.75f, 3.5f, middle), 5, 90);
                for (int row = 0; row < 2; row++) for (int seat = 0; seat < 5; seat++)
                {
                    Vector3 p = new Vector3(74 + row * 6, 0, z0 + 3 + seat * 2.1f);
                    Box("Waiting chair seat", rooms, p + Vector3.up * .48f, new Vector3(.8f, .13f, .85f), prop);
                    Box("Waiting chair back", rooms, p + new Vector3(.4f, .92f, 0), new Vector3(.12f, .85f, .85f), prop);
                    Box("Chair support", rooms, p + Vector3.up * .23f, new Vector3(.5f, .45f, .5f), dark, false);
                }
                Box("Water dispenser", rooms, new Vector3(85.5f, .65f, z1 - 1.5f), new Vector3(.75f, 1.3f, .65f), white);
                Shape(PrimitiveType.Cylinder, "Water bottle", rooms, new Vector3(85.5f, 1.6f, z1 - 1.5f), new Vector3(.5f, .3f, .5f), sheet, false);
                Floor("Waiting room roof " + (i + 1), roofs, 68, 87, z0, z1, 5.35f, concrete);
            }
            Wall("Waiting room block north wall", rooms, new Vector3(68, 0, 6), new Vector3(87, 0, 6));
        }
        static void Stairs()
        {
            var group = Group("07 Only open descent - platform layout pending");
            Floor("Upper stair landing", group, 68, 71, 6.3f, 15);
            for (int i = 0; i < 20; i++)
            {
                float top = -(i + 1) * .2f, x = 71 + (i + .5f) * .65f;
                Box("Descending step " + (i + 1), group, new Vector3(x, top - .18f, 10.65f), new Vector3(.65f, .36f, 8.7f), concrete);
                Box("Step edge", group, new Vector3(71 + i * .65f + .03f, top + .008f, 10.65f), new Vector3(.06f, .016f, 8.7f), dark, false);
            }
            Floor("Lower platform access landing", group, 84, 96, 6.3f, 15, -4, concrete);
            Wall("Stair north enclosure", group, new Vector3(68, -4.5f, 15.3f), new Vector3(96, -4.5f, 15.3f), 10);
            Wall("Stair south enclosure", group, new Vector3(71, -4.5f, 6), new Vector3(96, -4.5f, 6), 10);
            Wall("Lower landing end boundary", group, new Vector3(96, -4, 6), new Vector3(96, -4, 15.3f), 4);
            Beam("Stair centre handrail", group, new Vector3(71, 1.05f, 10.65f), new Vector3(84, -2.95f, 10.65f), .08f, dark);
            for (int i = 0; i < 8; i++) Beam("Handrail support", group, new Vector3(71 + i * 1.8f, -i * 1.8f * 4 / 13, 10.65f), new Vector3(71 + i * 1.8f, 1 - i * 1.8f * 4 / 13, 10.65f), .06f, dark);
            Sign("PLATFORM / NEXT AREA", group, new Vector3(95.7f, -1.5f, 10.65f), 8, 90);
        }
        static void Lighting()
        {
            var light = new GameObject("One daylight - blockout visibility").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.15f; light.color = new Color(1, .97f, .93f);
            light.transform.rotation = Quaternion.Euler(52, -35, 0); light.shadows = LightShadows.Hard; light.shadowCustomResolution = 1024;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .57f, .6f);
            RenderSettings.skybox = null; RenderSettings.fog = false;
        }
        static void CameraSettings(Camera c)
        {
            c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(.22f, .24f, .27f);
            c.allowHDR = false; c.allowMSAA = false; c.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }
        static TextMeshProUGUI Ui(Transform parent, string value, Vector2 anchor, Vector2 offset, Vector2 size, float fontSize)
        {
            var text = new GameObject(value, typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = anchor;
            text.rectTransform.anchoredPosition = offset; text.rectTransform.sizeDelta = size;
            text.font = TMP_Settings.defaultFontAsset; text.fontSize = fontSize; text.text = value;
            text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.raycastTarget = false;
            return text;
        }
        static void Explorer()
        {
            var g = new GameObject("Vaccine walkthrough player"); g.transform.SetPositionAndRotation(new Vector3(-35, .08f, -19), Quaternion.Euler(0, 45, 0));
            var cc = g.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .32f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .3f; cc.slopeLimit = 50;
            var player = g.AddComponent<VaccineWalkthrough>();
            var eye = new GameObject("Vaccine first person camera").AddComponent<Camera>(); eye.transform.SetParent(g.transform, false); eye.transform.localPosition = Vector3.up * 1.62f;
            eye.fieldOfView = 75; eye.nearClipPlane = .05f; eye.farClipPlane = 260; eye.tag = "MainCamera"; CameraSettings(eye); eye.gameObject.AddComponent<AudioListener>();
            var map = new GameObject("Vaccine overhead camera").AddComponent<Camera>(); map.transform.SetPositionAndRotation(new Vector3(15, 160, 5), Quaternion.Euler(90, 0, 0));
            map.orthographic = true; map.orthographicSize = 53; map.nearClipPlane = .1f; map.farClipPlane = 230; CameraSettings(map); map.enabled = false;
            var marker = Shape(PrimitiveType.Sphere, "Player position", g.transform, new Vector3(0, 20, 0), new Vector3(1.5f, .1f, 1.5f), white, false); marker.isStatic = false; marker.SetActive(false);
            var canvas = new GameObject("Vaccine walkthrough controls", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1366, 768); scaler.matchWidthOrHeight = .5f;
            var bar = new GameObject("Control strip", typeof(RectTransform), typeof(UnityEngine.UI.Image)); bar.transform.SetParent(canvas.transform, false);
            var rect = bar.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 0); rect.sizeDelta = new Vector2(0, 44);
            bar.GetComponent<UnityEngine.UI.Image>().color = new Color(.05f, .06f, .07f, .85f); bar.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            player.area = Ui(canvas.transform, "VACCINE / LAYOUT REVIEW", new Vector2(.5f, 1), new Vector2(0, -18), new Vector2(1000, 35), 22);
            player.prompt = Ui(canvas.transform, "", new Vector2(.5f, 0), new Vector2(0, 70), new Vector2(1000, 32), 19);
            Ui(canvas.transform, "WASD  Walk     SHIFT  Sprint     E  Open doors     TAB  Map     ENTER  Reset     ESC  Cursor", new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(1300, 28), 17);
            Ui(canvas.transform, "+", new Vector2(.5f, .5f), Vector2.zero, new Vector2(18, 18), 17);
            player.eye = eye; player.overview = map; player.roofs = roofs.gameObject; player.mapLabels = labels.gameObject; player.marker = marker;
        }
        static void Route()
        {
            var group = Group("Review route checkpoints");
            var points = new[] { new Vector3(-35, 0, -19), new Vector3(-6, 0, -12), new Vector3(-6, 0, 4),
                new Vector3(-15, 0, 22), new Vector3(-15, 0, 31), new Vector3(-23, 0, 36), new Vector3(-15, 0, 31),
                new Vector3(-15, 0, 22), new Vector3(-8, 0, 9), new Vector3(32, 0, 2), new Vector3(41, 0, -9.5f),
                new Vector3(27, 0, -9.5f), new Vector3(22.5f, 0, -25), new Vector3(20, 0, -33), new Vector3(58, 0, -33),
                new Vector3(58, 0, -16.5f), new Vector3(72, 0, -16.5f), new Vector3(58, 0, -16.5f), new Vector3(61, 0, 8),
                new Vector3(69, 0, 8), new Vector3(85, -4, 8), new Vector3(91, -4, 10) };
            for (int i = 0; i < points.Length; i++) { var p = Group("Route " + i.ToString("D2"), group); p.position = points[i]; }
        }
    }
}
