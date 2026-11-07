using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShipEnemies.Editor
{
    public static class EnemyAssetBuilder
    {
        const string Root = "Assets/Enemies/";
        public const string PreviewPath = Root + "Scenes/EnemyPreview.unity";
        static readonly string[] Names = { "Shambler", "Sprinter", "Spitter", "Bloater", "Regurgitator", "Worm", "PukeWorm" };
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, int> AgentTypes = new Dictionary<string, int>();

        [MenuItem("Tools/Enemies/Open asset preview")]
        public static void OpenPreview()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before opening the preview.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(PreviewPath);
        }

        [MenuItem("Tools/Enemies/Rebuild enemy assets and preview")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save your current scene first.");
            Mats.Clear(); AgentTypes.Clear();
            foreach (string folder in new[] { "Animations", "Definitions", "Materials", "Prefabs", "Scenes" })
                if (!AssetDatabase.IsValidFolder(Root + folder)) AssetDatabase.CreateFolder(Root.TrimEnd('/'), folder);
            ConfigureTexture();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/EnemySurfaceAtlas.png");
            foreach (string family in new[] { "Skin", "Muscle", "Mutant", "Cloth" })
                Mats[family] = Material(family, Color.white, atlas);
            Mats["Dark"] = Material("Dark", new Color(.018f, .023f, .021f));
            Mats["Bone"] = Material("Bone", new Color(.60f, .54f, .36f));
            Mats["Metal"] = Material("Metal", new Color(.30f, .33f, .31f)); Mats["Metal"].SetFloat("_Metallic", .65f);
            Mats["Acid"] = Material("Acid", new Color(.25f, .39f, .04f));
            Mats["Acid"].EnableKeyword("_EMISSION"); Mats["Acid"].SetColor("_EmissionColor", new Color(.13f, .19f, .008f));
            ConfigureAgent("Enemy standard", .38f, 2.15f);
            ConfigureAgent("Enemy heavy", .85f, 2.7f);
            ConfigureAgent("Enemy boss", 1.55f, 3.8f);
            ConfigureAgent("Enemy parasite", .23f, .55f);
            CreateEffects(out AcidProjectile projectile, out AcidPool pool);
            var prefabs = new EnemyBrain[Names.Length];
            // Parasites must exist before the main definitions are wired.
            foreach (int i in new[] { 5, 6, 0, 1, 2, 3, 4 })
            {
                string name = Names[i];
                var controller = ConfigureModel(name);
                var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Root + "Definitions/" + name + ".asset");
                if (def == null) { def = ScriptableObject.CreateInstance<EnemyDefinition>(); AssetDatabase.CreateAsset(def, Root + "Definitions/" + name + ".asset"); }
                SetDefinition(def, i); def.projectilePrefab = projectile; def.poolPrefab = pool;
                def.parasitePrefab = i == 4 ? prefabs[6] : i == 3 ? prefabs[5] : null;
                EditorUtility.SetDirty(def);
                var root = new GameObject(name);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/" + name + ".fbx"));
                visual.name = "Visual"; visual.transform.SetParent(root.transform, false);
                var animator = visual.GetComponent<Animator>(); if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Mats[m.name]).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    renderer.quality = SkinQuality.Bone2;
                    // Includes the full prone death pose; never clip a fallen body at the bind-pose bounds.
                    renderer.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(6, 7, 8));
                }
                var collider = root.AddComponent<CapsuleCollider>(); collider.height = def.height; collider.radius = def.radius;
                collider.center = Vector3.up * def.height * .5f;
                var health = root.AddComponent<Vitality>(); health.maximum = def.health; health.acidResistance = def.chemicalResistance;
                var agent = root.AddComponent<NavMeshAgent>(); agent.agentTypeID = AgentTypes[i == 4 ? "Enemy boss" : i == 3 ? "Enemy heavy" : i >= 5 ? "Enemy parasite" : "Enemy standard"];
                agent.radius = def.radius; agent.height = def.height; agent.speed = def.speed; agent.angularSpeed = def.turnSpeed;
                agent.acceleration = i == 1 ? 12 : 7; agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.autoBraking = true;
                var brain = root.AddComponent<EnemyBrain>(); brain.definition = def; brain.animator = animator;
                var head = visual.GetComponentsInChildren<Transform>().First(t => t.name == (i >= 5 ? "Segment0" : "Head"));
                var socket = new GameObject("MouthSocket").transform; socket.SetParent(head, true);
                socket.position = new Vector3(0, i == 4 ? 2.92f : i == 2 ? 1.7f : i >= 5 ? .22f : 1.68f, i == 4 ? .82f : .4f);
                brain.mouth = socket;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "Prefabs/" + name + ".prefab");
                prefabs[i] = prefab.GetComponent<EnemyBrain>(); UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets(); CreatePreview(prefabs); AssetDatabase.SaveAssets();
            Debug.Log("Enemy kit built: 7 animated prefabs, 7 definitions, 4 navigation sizes, isolated preview scene.");
        }
        static void ConfigureTexture()
        {
            var t = (TextureImporter)AssetImporter.GetAtPath(Root + "Textures/EnemySurfaceAtlas.png");
            t.textureType = TextureImporterType.Default; t.sRGBTexture = true; t.maxTextureSize = 1024;
            t.textureCompression = TextureImporterCompression.Compressed; t.mipmapEnabled = true; t.isReadable = false;
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; t.SaveAndReimport();
        }
        static Material Material(string name, Color color, Texture2D texture = null)
        {
            string path = Root + "Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetTexture("_BaseMap", texture); mat.SetFloat("_Smoothness", .16f); mat.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(mat); return mat;
        }
        static AnimatorController ConfigureModel(string name)
        {
            string path = Root + "Models/" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = true;
            importer.importCameras = false; importer.importLights = false; importer.isReadable = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clip.takeName.Substring(clip.takeName.LastIndexOf('_') + 1);
                clip.loopTime = clip.name == "Idle" || clip.name == "Move";
                clip.lockRootRotation = true; clip.lockRootPositionXZ = true; clip.lockRootHeightY = true;
            }
            importer.clipAnimations = clips; importer.SaveAndReimport();
            string controllerPath = Root + "Animations/" + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var old in machine.states) machine.RemoveState(old.state);
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
            {
                var state = machine.AddState(clip.name); state.motion = clip; state.writeDefaultValues = true;
                if (clip.name == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller); return controller;
        }
        static void ConfigureAgent(string name, float radius, float height)
        {
            int id = -1; bool found = false;
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            { int candidate = NavMesh.GetSettingsByIndex(i).agentTypeID; if (NavMesh.GetSettingsNameFromID(candidate) == name) { id = candidate; found = true; } }
            // Unity generates signed IDs; a negative ID is a valid existing type.
            if (!found) id = NavMesh.CreateSettings().agentTypeID;
            // Unity's navigation settings inspector uses these serialized properties; preserve every unrelated agent type.
            var serialized = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var settings = serialized.FindProperty("m_Settings"); var names = serialized.FindProperty("m_SettingNames");
            for (int i = 0; i < settings.arraySize; i++)
            {
                var entry = settings.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("agentTypeID").intValue != id) continue;
                entry.FindPropertyRelative("agentRadius").floatValue = radius; entry.FindPropertyRelative("agentHeight").floatValue = height;
                entry.FindPropertyRelative("agentClimb").floatValue = .25f; entry.FindPropertyRelative("agentSlope").floatValue = 40;
                entry.FindPropertyRelative("minRegionArea").floatValue = .1f;
                names.GetArrayElementAtIndex(i).stringValue = name;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); AgentTypes[name] = id;
        }
        static void SetDefinition(EnemyDefinition d, int i)
        {
            d.kind = (EnemyKind)i; d.displayName = i == 6 ? "Puke Worm" : Names[i];
            d.health = new[] { 95f, 175, 115, 1100, 7500, 12, 40 }[i];
            d.speed = new[] { 5f, 6.5f, 3, 1.05f, 5f, 2.4f, 2.8f }[i];
            d.radius = i == 4 ? 1.55f : i == 3 ? .85f : i >= 5 ? .23f : .38f;
            d.height = i == 4 ? 3.6f : i == 3 ? 2.5f : i >= 5 ? .45f : 2;
            d.turnSpeed = i == 3 || i == 4 ? 72 : 360;
            d.meleeDamage = new[] { 8f, 12, 7, 32, 45, 3, 6 }[i];
            d.meleeRange = i == 4 ? 2.5f : i == 3 ? 1.8f : i >= 5 ? .65f : 1.2f;
            d.meleeWindup = i == 3 || i == 4 ? .78f : i >= 5 ? .3f : .42f;
            d.meleeDuration = i == 3 || i == 4 ? 1.6f : i >= 5 ? .65f : .9f;
            d.meleeCooldown = i == 4 ? 2.2f : i == 3 ? 1.8f : i == 1 ? .95f : 1.2f;
            d.rangedRange = i == 4 ? 16 : i == 2 ? 12 : 0;
            d.rangedCooldown = i == 4 ? 8.5f : 2.7f; d.rangedWindup = i == 4 ? 1.1f : .6f; d.rangedDuration = i == 4 ? 2.4f : 1.3f;
            d.acidImpactDamage = i == 4 ? 18 : 8; d.acidDamagePerSecond = i == 4 ? 12 : 6;
            d.acidRadius = i == 4 ? 2.6f : 1.3f; d.acidLifetime = i == 4 ? 7 : 5;
            d.chemicalResistance = i == 4 ? .2f : i == 2 ? .1f : 0;
            d.deathParasites = i == 4 ? 6 : i == 3 ? 4 : 0; d.vomitParasites = i == 4 ? 3 : 0;
        }
        static void CreateEffects(out AcidProjectile projectile, out AcidPool pool)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Sphere); p.name = "AcidProjectile"; p.transform.localScale = Vector3.one * .22f;
            UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>()); p.GetComponent<Renderer>().sharedMaterial = Mats["Acid"];
            p.AddComponent<AcidProjectile>(); projectile = PrefabUtility.SaveAsPrefabAsset(p, Root + "Prefabs/AcidProjectile.prefab").GetComponent<AcidProjectile>(); UnityEngine.Object.DestroyImmediate(p);
            p = GameObject.CreatePrimitive(PrimitiveType.Cylinder); p.name = "AcidPool"; p.transform.localScale = new Vector3(2.6f, .025f, 2.6f);
            UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>()); p.GetComponent<Renderer>().sharedMaterial = Mats["Acid"];
            p.AddComponent<AcidPool>(); pool = PrefabUtility.SaveAsPrefabAsset(p, Root + "Prefabs/AcidPool.prefab").GetComponent<AcidPool>(); UnityEngine.Object.DestroyImmediate(p);
        }
        static GameObject Cube(string name, Vector3 p, Vector3 size, Material material, Transform parent = null)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name; o.transform.SetParent(parent); o.transform.position = p; o.transform.localScale = size;
            o.GetComponent<Renderer>().sharedMaterial = material; return o;
        }
        static TMPro.TMP_Text Sign(string name, string text, Vector3 position, float width, float height, float size)
        {
            var o = new GameObject(name); o.transform.position = position; o.transform.rotation = Quaternion.Euler(0, 180, 0);
            var t = o.AddComponent<TMPro.TextMeshPro>(); t.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            t.text = text; t.fontSize = size; t.alignment = TMPro.TextAlignmentOptions.Center; t.color = new Color(.65f, .79f, .79f);
            t.rectTransform.sizeDelta = new Vector2(width, height); return t;
        }
        static void CreatePreview(EnemyBrain[] prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.35f, .39f, .43f);
            RenderSettings.skybox = null; RenderSettings.fog = false;
            var floorMat = Material("LabDeck", new Color(.045f, .065f, .075f));
            var plinthMat = Material("LabPlinth", new Color(.095f, .125f, .14f));
            var obstacleMat = Material("LabObstacle", new Color(.15f, .20f, .22f));
            Cube("Gallery floor", new Vector3(0, -.3f, 0), new Vector3(25, .4f, 9), floorMat);
            Cube("Gallery backdrop", new Vector3(0, 2.3f, -3), new Vector3(25, 5, .25f), floorMat);
            var arena = new GameObject("Navigation test arena").transform;
            Cube("Arena floor", new Vector3(0, -.2f, 16), new Vector3(24, .4f, 22), floorMat, arena);
            Cube("LOS and routing obstacle", new Vector3(0, 1.1f, 13), new Vector3(3, 2.2f, 1), obstacleMat, arena);
            Cube("Left boundary", new Vector3(-12, 1, 16), new Vector3(.3f, 2, 22), obstacleMat, arena);
            Cube("Right boundary", new Vector3(12, 1, 16), new Vector3(.3f, 2, 22), obstacleMat, arena);
            foreach (var pair in AgentTypes)
            {
                var surface = arena.gameObject.AddComponent<NavMeshSurface>(); surface.agentTypeID = pair.Value;
                surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.overrideVoxelSize = true; surface.voxelSize = Mathf.Max(.07f, NavMesh.GetSettingsByID(pair.Value).agentRadius / 3);
                surface.BuildNavMesh();
                string navPath = Root + "Scenes/" + pair.Key.Replace(" ", "") + "NavMesh.asset";
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
                if (existing != null) AssetDatabase.DeleteAsset(navPath);
                AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            }
            prefabs = Names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + n + ".prefab").GetComponent<EnemyBrain>()).ToArray();
            var lab = new GameObject("Enemy preview controls").AddComponent<EnemyPreview>(); lab.prefabs = prefabs;
            lab.displays = new EnemyBrain[7];
            float[] xs = { 9, 5.8f, 2.6f, -1.1f, -6.3f, 2, -.5f };
            for (int i = 0; i < 7; i++)
            {
                float z = i >= 5 ? 3 : 0;
                Cube(Names[i] + " plinth", new Vector3(xs[i], -.04f, z), new Vector3(i == 4 ? 4.8f : 2.7f, .12f, i >= 5 ? 1.5f : 2.8f), plinthMat);
                var o = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i].gameObject);
                o.name = "Display " + Names[i]; o.transform.position = new Vector3(xs[i], .02f, z);
                var brain = o.GetComponent<EnemyBrain>(); brain.displayOnly = true; o.GetComponent<NavMeshAgent>().enabled = false;
                o.GetComponent<Collider>().enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
                lab.displays[i] = brain;
                Sign(Names[i] + " label", (i + 1) + "  " + prefabs[i].definition.displayName.ToUpperInvariant(), new Vector3(xs[i], i >= 5 ? .7f : 3.9f, z), i == 4 ? 5 : 3.1f, .6f, i >= 5 ? 2 : 2.5f);
            }
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule); dummy.name = "Moveable training target";
            dummy.transform.position = new Vector3(0, 1, 17); dummy.GetComponent<Renderer>().sharedMaterial = Material("Target", new Color(.35f, .53f, .58f));
            // Root at ground level, collider/visual at torso height, for accurate range and acid checks.
            var targetRoot = new GameObject("Explicit enemy target"); dummy.transform.SetParent(targetRoot.transform); targetRoot.transform.position = new Vector3(0, 0, 17); dummy.transform.localPosition = Vector3.up;
            lab.dummy = targetRoot.AddComponent<Vitality>(); lab.dummy.receivesEnemyAttacks = true; lab.dummy.maximum = 100000;
            lab.arenaSpawn = new GameObject("Arena spawn").transform; lab.arenaSpawn.position = new Vector3(0, 0, 8);
            lab.liveRoot = new GameObject("Live enemies - cleared on reset").transform;
            var cam = new GameObject("Enemy review camera").AddComponent<Camera>(); cam.tag = "MainCamera";
            cam.backgroundColor = new Color(.018f, .026f, .035f); cam.clearFlags = CameraClearFlags.SolidColor; cam.fieldOfView = 53; cam.nearClipPlane = .06f; cam.farClipPlane = 80;
            cam.transform.position = new Vector3(0, 5.1f, 13.5f); cam.transform.LookAt(new Vector3(0, 1.5f, 0));
            cam.allowHDR = false; cam.allowMSAA = false; cam.gameObject.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false; lab.reviewCamera = cam;
            var light = new GameObject("Soft key").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; light.color = new Color(.89f, .94f, 1);
            light.transform.rotation = Quaternion.Euler(36, -145, 0); light.shadows = LightShadows.None;
            var fill = new GameObject("Warm fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .65f; fill.color = new Color(1, .76f, .56f);
            fill.transform.rotation = Quaternion.Euler(20, 35, 0); fill.shadows = LightShadows.None;
            lab.statusSign = Sign("Preview instructions", "ENEMY ASSET LAB\n1-7 inspect   0 lineup   TAB behavior demo\nSPACE animation   E ability   H flinch   K death   R idle\nRight-drag orbit   Scroll zoom", new Vector3(0, 5.45f, -1), 22, 2, 2.4f);
            EditorSceneManager.SaveScene(scene, PreviewPath);
            Selection.activeGameObject = lab.gameObject;
        }
    }
}
