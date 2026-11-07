using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Vaccine.Editor
{
    public static class VaccinePolishPass
    {
        const string Root = "Assets/Vaccine";

        [MenuItem("Vaccine/Apply Complete Visual and Gameplay Polish")]
        public static string ApplyAll()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before running Polish Pass.");
            GenerateProceduralTexturesAndNormals();
            UpgradeMaterials();
            UpgradeInfectedModelsAndPrefabs();
            PolishSurfaceScene();
            PolishLabDistrictScene();
            PolishMainMenuAndCapture();
            EditorSceneManager.OpenScene("Assets/Coop/Scenes/MainMenu.unity");
            AssetDatabase.SaveAssets();
            return "Polished textures, normal maps, object-locked TacticalSurface shader, revamped 3D zombie models & InfectedCharacter shader, revamped 3D Main Menu, and Vaccine missions.";
        }

        [MenuItem("Vaccine/Apply Polish and Build Release EXE")]
        public static void ApplyAllAndBuildRelease()
        {
            ApplyAll();
            CoopBuild.BuildRelease();
        }

        static float Frac(float x) => x - Mathf.Floor(x);
        static float Hash2(int x, int y, int seed)
        {
            int n = (x & 1023) * 374761393 + (y & 1023) * 668265263 + seed * 1442695041;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }
        static float SmoothNoise(float u, float v, int freq, int seed)
        {
            float x = u * freq, y = v * freq;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash2(ix % freq, iy % freq, seed);
            float b = Hash2((ix + 1) % freq, iy % freq, seed);
            float c = Hash2(ix % freq, (iy + 1) % freq, seed);
            float d = Hash2((ix + 1) % freq, (iy + 1) % freq, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        static float Fbm(float u, float v, int baseFreq, int seed)
        {
            return SmoothNoise(u, v, baseFreq, seed) * 0.55f
                 + SmoothNoise(u, v, baseFreq * 2, seed + 17) * 0.30f
                 + SmoothNoise(u, v, baseFreq * 4, seed + 43) * 0.15f;
        }

        static void GenerateProceduralTexturesAndNormals()
        {
            Directory.CreateDirectory(Root + "/Textures/Subway");
            var texPaths = new[]
            {
                Root + "/Textures/Asphalt.png",
                Root + "/Textures/Concrete.png",
                Root + "/Textures/StationFloor.png",
                Root + "/Textures/StationWall.png",
                Root + "/Textures/SheetMetal.png",
                Root + "/Textures/Aircraft.png",
                Root + "/Textures/Helicopter.png",
                Root + "/Textures/Subway/TrainEnamel.png",
                Root + "/Textures/Subway/CarriageRubber.png"
            };

            bool allReady = texPaths.All(p =>
            {
                string np = p.Substring(0, p.Length - 4) + "_Normal.png";
                var ni = AssetImporter.GetAtPath(np) as TextureImporter;
                return ni != null && ni.textureType == TextureImporterType.NormalMap;
            });
            if (allReady) return;

            // 1. Generate high-detail procedural Albedo textures for Helicopter, TrainEnamel, and CarriageRubber
            WriteHelicopterAlbedo(Root + "/Textures/Helicopter.png", 512);
            WriteTrainEnamelAlbedo(Root + "/Textures/Subway/TrainEnamel.png", 512);
            WriteCarriageRubberAlbedo(Root + "/Textures/Subway/CarriageRubber.png", 512);
            AssetDatabase.Refresh();

            // Configure Albedo importers (readable temporarily so we can derive exact matching Sobel normal maps)
            foreach (string path in texPaths)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (!imp) continue;
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.isReadable = true;
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 4;
                imp.maxTextureSize = 1024;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }

            // Generate matching Normal Maps via Sobel height gradient
            foreach (string path in texPaths)
            {
                var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!src) continue;
                string normalPath = path.Substring(0, path.Length - 4) + "_Normal.png";
                float strength = path.Contains("Asphalt") || path.Contains("SheetMetal") ? 3.2f :
                                 path.Contains("StationWall") || path.Contains("StationFloor") ? 2.6f : 2.1f;
                BuildNormalMapFromAlbedo(src, normalPath, strength);

                var srcImp = (TextureImporter)AssetImporter.GetAtPath(path);
                srcImp.isReadable = false;
                srcImp.SaveAndReimport();
            }

            AssetDatabase.Refresh();
            foreach (string path in texPaths)
            {
                string normalPath = path.Substring(0, path.Length - 4) + "_Normal.png";
                var nImp = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                if (!nImp) continue;
                nImp.textureType = TextureImporterType.NormalMap;
                nImp.sRGBTexture = false;
                nImp.mipmapEnabled = true;
                nImp.wrapMode = TextureWrapMode.Repeat;
                nImp.filterMode = FilterMode.Trilinear;
                nImp.anisoLevel = 4;
                nImp.maxTextureSize = 1024;
                nImp.textureCompression = TextureImporterCompression.CompressedHQ;
                nImp.SaveAndReimport();
            }
        }

        static void WriteHelicopterAlbedo(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float n = Fbm(u, v, 8, 101);
                    float fine = SmoothNoise(u, v, 64, 202);
                    Color baseCol = Color.Lerp(new Color(0.22f, 0.26f, 0.21f), new Color(0.31f, 0.35f, 0.28f), n * 0.7f + fine * 0.3f);
                    // Panel seams at 2x2 grid
                    float su = Mathf.Min(Frac(u * 2f), 1f - Frac(u * 2f));
                    float sv = Mathf.Min(Frac(v * 2f), 1f - Frac(v * 2f));
                    float seam = Mathf.Min(su, sv);
                    if (seam < 0.008f) baseCol *= 0.48f;
                    else if (seam < 0.022f) baseCol = Color.Lerp(baseCol, new Color(0.52f, 0.55f, 0.53f), 0.35f);
                    // Rivets along seams
                    float ru = Frac(u * 32f) - 0.5f, rv = Frac(v * 32f) - 0.5f;
                    if ((su < 0.03f || sv < 0.03f) && (ru * ru + rv * rv) < 0.045f)
                        baseCol = new Color(0.45f, 0.48f, 0.46f);
                    pixels[y * size + x] = baseCol;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void WriteTrainEnamelAlbedo(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                float fluting = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 28f);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float n = Fbm(u, v, 6, 303);
                    float streak = SmoothNoise(u * 2f, v * 0.5f, 12, 404);
                    Color c = Color.Lerp(new Color(0.76f, 0.80f, 0.82f), new Color(0.89f, 0.92f, 0.93f), n * 0.6f + fluting * 0.25f + streak * 0.15f);
                    float seamU = Mathf.Min(Frac(u * 2f), 1f - Frac(u * 2f));
                    float seamV = Mathf.Min(Frac(v * 2f), 1f - Frac(v * 2f));
                    if (Mathf.Min(seamU, seamV) < 0.007f) c *= 0.55f;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void WriteCarriageRubberAlbedo(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float n = Fbm(u, v, 10, 505);
                    float cu = Frac(u * 16f) - 0.5f, cv = Frac(v * 16f) - 0.5f;
                    float dist = Mathf.Sqrt(cu * cu + cv * cv);
                    float stud = dist < 0.28f ? 1.12f : (dist < 0.33f ? 0.78f : 0.94f);
                    float baseVal = (0.36f + n * 0.12f) * stud;
                    pixels[y * size + x] = new Color(baseVal * 0.95f, baseVal * 0.98f, baseVal, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void BuildNormalMapFromAlbedo(Texture2D src, string outPath, float strength)
        {
            int w = src.width, h = src.height;
            var cols = src.GetPixels();
            var heights = new float[w * h];
            for (int i = 0; i < cols.Length; i++)
                heights[i] = cols[i].r * 0.299f + cols[i].g * 0.587f + cols[i].b * 0.114f;

            var normals = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                int yUp = (y + 1) % h, yDn = (y - 1 + h) % h;
                for (int x = 0; x < w; x++)
                {
                    int xRt = (x + 1) % w, xLt = (x - 1 + w) % w;
                    float tl = heights[yUp * w + xLt], t = heights[yUp * w + x], tr = heights[yUp * w + xRt];
                    float l  = heights[y * w + xLt],                             r  = heights[y * w + xRt];
                    float bl = heights[yDn * w + xLt], b = heights[yDn * w + x], br = heights[yDn * w + xRt];

                    float dX = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dY = (tl + 2f * t + tr) - (bl + 2f * b + br);
                    Vector3 n = new Vector3(-dX * strength, -dY * strength, 1f).normalized;
                    normals[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            var normalTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            normalTex.SetPixels(normals);
            normalTex.Apply();
            File.WriteAllBytes(outPath, normalTex.EncodeToPNG());
            Object.DestroyImmediate(normalTex);
        }

        static void UpgradeMaterials()
        {
            var surfaceShader = Shader.Find("Vaccine/TacticalSurface");
            var interactShader = Shader.Find("Vaccine/TacticalInteractable");
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");

            void ConfigureSurfaceMat(string matPath, string texRel, Color tint, float smooth, float metal, float bumpScale, float grime, float wetness, float triBlend = 1.0f, Color? emission = null)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!m) return;
                m.shader = surfaceShader;
                m.enableInstancing = true;
                m.SetColor("_BaseColor", tint);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                m.SetFloat("_BumpScale", bumpScale);
                m.SetFloat("_GrimeStrength", grime);
                m.SetFloat("_Wetness", wetness);
                m.SetFloat("_UseTriplanar", string.IsNullOrEmpty(texRel) ? 0f : 1.0f);
                m.SetFloat("_TriplanarScale", 0.42f);
                m.SetColor("_SpecColor", new Color(0.24f, 0.27f, 0.30f, 1f));
                if (!string.IsNullOrEmpty(texRel))
                {
                    var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + texRel + ".png");
                    var normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + texRel + "_Normal.png");
                    if (baseTex) m.SetTexture("_BaseMap", baseTex);
                    if (normTex) m.SetTexture("_BumpMap", normTex);
                }
                m.SetColor("_EmissionColor", emission ?? Color.black);
                EditorUtility.SetDirty(m);
            }

            string artDir = Root + "/Materials/Art/";
            ConfigureSurfaceMat(artDir + "Asphalt.mat",      "Asphalt",      new Color(0.86f, 0.89f, 0.93f), 0.32f, 0.04f, 1.35f, 0.28f, 0.62f, 0.45f);
            ConfigureSurfaceMat(artDir + "Concrete.mat",     "Concrete",     new Color(0.82f, 0.84f, 0.85f), 0.22f, 0.03f, 1.15f, 0.32f, 0.15f, 0.40f);
            ConfigureSurfaceMat(artDir + "StationFloor.mat", "StationFloor", new Color(0.84f, 0.88f, 0.90f), 0.48f, 0.06f, 1.10f, 0.22f, 0.38f, 0.15f);
            ConfigureSurfaceMat(artDir + "StationWall.mat",  "StationWall",  new Color(0.94f, 0.96f, 0.97f), 0.56f, 0.04f, 1.20f, 0.20f, 0.00f, 0.10f);
            ConfigureSurfaceMat(artDir + "SheetMetal.mat",   "SheetMetal",   new Color(0.76f, 0.81f, 0.84f), 0.46f, 0.55f, 1.40f, 0.30f, 0.00f, 0.20f);
            ConfigureSurfaceMat(artDir + "Container.mat",    "SheetMetal",   new Color(0.32f, 0.47f, 0.44f), 0.38f, 0.42f, 1.25f, 0.34f, 0.00f, 0.20f);
            ConfigureSurfaceMat(artDir + "Aircraft.mat",     "Aircraft",     new Color(0.90f, 0.92f, 0.90f), 0.52f, 0.48f, 1.10f, 0.25f, 0.00f, 0.15f);
            ConfigureSurfaceMat(artDir + "Helicopter.mat",   "Helicopter",   new Color(0.88f, 0.93f, 0.87f), 0.42f, 0.45f, 1.15f, 0.24f, 0.00f, 0.20f);
            ConfigureSurfaceMat(artDir + "Steel.mat",        "Aircraft",     new Color(0.28f, 0.33f, 0.36f), 0.62f, 0.72f, 0.85f, 0.22f, 0.00f, 0.35f);
            ConfigureSurfaceMat(artDir + "Rubber.mat",       "Subway/CarriageRubber", new Color(0.08f, 0.09f, 0.10f), 0.18f, 0.02f, 0.75f, 0.15f, 0.00f, 0.40f);
            ConfigureSurfaceMat(artDir + "Teal.mat",         "Aircraft",     new Color(0.14f, 0.42f, 0.43f), 0.50f, 0.30f, 0.75f, 0.18f, 0.00f, 0.25f);
            ConfigureSurfaceMat(artDir + "PaintBlue.mat",    "Aircraft",     new Color(0.24f, 0.43f, 0.54f), 0.48f, 0.32f, 0.80f, 0.20f, 0.00f, 0.25f);
            ConfigureSurfaceMat(artDir + "PaintRed.mat",     "Aircraft",     new Color(0.46f, 0.14f, 0.10f), 0.44f, 0.25f, 0.80f, 0.22f, 0.00f, 0.25f);
            ConfigureSurfaceMat(artDir + "Cream.mat",        "Aircraft",     new Color(0.89f, 0.85f, 0.73f), 0.42f, 0.12f, 0.65f, 0.18f, 0.00f, 0.20f);
            ConfigureSurfaceMat(artDir + "Amber.mat",        "Aircraft",     new Color(0.85f, 0.52f, 0.10f), 0.45f, 0.20f, 0.60f, 0.10f, 0.00f, 0.25f, new Color(0.35f, 0.16f, 0.02f));
            ConfigureSurfaceMat(artDir + "Markings.mat",     "Concrete",     new Color(0.82f, 0.79f, 0.64f), 0.20f, 0.02f, 0.85f, 0.25f, 0.00f, 0.30f);
            ConfigureSurfaceMat(artDir + "Rust.mat",         "SheetMetal",   new Color(0.38f, 0.22f, 0.13f), 0.18f, 0.35f, 1.45f, 0.42f, 0.00f, 0.35f);
            ConfigureSurfaceMat(artDir + "Scorch.mat",       "Concrete",     new Color(0.14f, 0.13f, 0.12f), 0.12f, 0.05f, 1.30f, 0.48f, 0.00f, 0.40f);
            ConfigureSurfaceMat(artDir + "LampCool.mat",     null,           new Color(0.82f, 0.94f, 1.00f), 0.60f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, new Color(1.35f, 1.85f, 2.25f));
            ConfigureSurfaceMat(artDir + "LampWarm.mat",     null,           new Color(1.00f, 0.76f, 0.38f), 0.60f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, new Color(2.35f, 1.40f, 0.48f));
            ConfigureSurfaceMat(artDir + "Emergency.mat",    null,           new Color(0.22f, 0.85f, 0.62f), 0.55f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f, new Color(0.18f, 1.55f, 0.85f));

            string subDir = Root + "/Materials/SubwayArt/";
            ConfigureSurfaceMat(subDir + "Train enamel.mat",       "Subway/TrainEnamel",    new Color(0.88f, 0.91f, 0.92f), 0.58f, 0.45f, 1.05f, 0.18f, 0.00f, 0.15f);
            ConfigureSurfaceMat(subDir + "Train rubber floor.mat", "Subway/CarriageRubber", new Color(0.68f, 0.74f, 0.78f), 0.28f, 0.05f, 1.30f, 0.22f, 0.12f, 0.20f);
            ConfigureSurfaceMat(subDir + "Train blue stripe.mat",  "Subway/TrainEnamel",    new Color(0.14f, 0.38f, 0.46f), 0.54f, 0.32f, 0.95f, 0.14f, 0.00f, 0.15f);
            ConfigureSurfaceMat(subDir + "Train seat vinyl.mat",   "Subway/CarriageRubber", new Color(0.14f, 0.28f, 0.34f), 0.46f, 0.05f, 0.65f, 0.15f, 0.00f, 0.35f);
            ConfigureSurfaceMat(subDir + "Track ballast.mat",      "Asphalt",               new Color(0.32f, 0.30f, 0.28f), 0.12f, 0.02f, 1.65f, 0.45f, 0.20f, 0.50f);
            ConfigureSurfaceMat(subDir + "Tactile yellow.mat",     "Subway/CarriageRubber", new Color(1.00f, 0.76f, 0.20f), 0.32f, 0.05f, 1.45f, 0.18f, 0.00f, 0.25f, new Color(0.12f, 0.08f, 0.01f));

            // Create dedicated high-visibility TacticalInteractable materials for C4, Generator, Supplies, Vaccine, and Medkits
            CreateInteractableMat(Root + "/Materials/Art/InteractC4.mat",       new Color(0.92f, 0.48f, 0.08f), new Color(0.55f, 0.20f, 0.02f), new Color(2.4f, 1.15f, 0.18f), 3.4f);
            CreateInteractableMat(Root + "/Materials/Art/InteractTech.mat",     new Color(0.14f, 0.52f, 0.54f), new Color(0.06f, 0.24f, 0.26f), new Color(0.25f, 1.65f, 1.55f), 2.6f);
            CreateInteractableMat(Root + "/Materials/Art/InteractVaccine.mat",  new Color(0.85f, 0.92f, 0.95f), new Color(0.12f, 0.38f, 0.42f), new Color(0.35f, 1.95f, 1.65f), 3.0f);
            CreateInteractableMat(Root + "/Materials/Medical case green.mat",   new Color(0.06f, 0.48f, 0.28f), new Color(0.04f, 0.24f, 0.12f), new Color(0.25f, 1.85f, 0.85f), 2.5f);

            Material CreateInteractableMat(string path, Color baseCol, Color emissive, Color pulse, float speed)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m)
                {
                    m = new Material(interactShader);
                    AssetDatabase.CreateAsset(m, path);
                }
                else m.shader = interactShader;
                m.enableInstancing = true;
                m.SetColor("_BaseColor", baseCol);
                m.SetColor("_EmissionColor", emissive);
                m.SetColor("_PulseColor", pulse);
                m.SetFloat("_Smoothness", 0.62f);
                m.SetFloat("_RimPower", 2.2f);
                m.SetFloat("_PulseSpeed", speed);
                var acTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Aircraft.png");
                if (acTex) m.SetTexture("_BaseMap", acTex);
                EditorUtility.SetDirty(m);
                return m;
            }
        }

        static void PolishSurfaceScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Vaccine/Scenes/Vaccine_Blockout.unity");
            while (Object.FindAnyObjectByType<VaccineStaticArtCache>()) VaccineArtBatcher.Restore();

            var oldPolish = GameObject.Find("19 Vaccine visual and gameplay polish");
            if (oldPolish) Object.DestroyImmediate(oldPolish);
            var polishRoot = new GameObject("19 Vaccine visual and gameplay polish").transform;

            var m = Object.FindAnyObjectByType<VaccineMission>();
            UpgradeSceneInteractables(m, polishRoot);
            UpgradeLightingAndPostProcessing(m.player, polishRoot, false);

            VaccineArtBatcher.Bake();
            var surfaceTrain = m.player.GetComponent<VaccineBoardingTransfer>().train;
            AddTrainGangwayGuards(surfaceTrain);

            // Ensure station power fixtures stay dynamic and properly linked after re-baking
            m.stationFixtures = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include)
                .Where(r => r.sharedMaterial && (r.sharedMaterial.name.Contains("Lamp") || r.sharedMaterial.name.Contains("Emergency")) && ((r.transform.position.x > 0 && r.transform.position.z < 1) || r.transform.position.y < -2))
                .Cast<Renderer>().ToArray();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Vector3 c4Pos = m.tailCharge ? m.tailCharge.transform.position : new Vector3(-28f, 2.2f, 20f);
            CaptureSceneSnapshots("Surface", new[]
            {
                ("helipad_c4", c4Pos + new Vector3(4.2f, 1.4f, -4.8f), c4Pos + new Vector3(-1.5f, 0.4f, 0f)),
                ("station_medkit", new Vector3(12.5f, 2.5f, -5.2f), new Vector3(18.0f, 1.2f, 2.5f))
            });
            VerifyPolishedSetup(m);
        }

        static void PolishLabDistrictScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Vaccine/Scenes/Vaccine_LabDistrict.unity");
            LabDistrictBuilder.RestoreLabBatches();

            // Replace any corrupted Surface-* meshes with Unity's clean unit Cube mesh
            var tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var unitCubeMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tempCube);
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                if (mf.sharedMesh && mf.sharedMesh.name.StartsWith("Surface-"))
                    mf.sharedMesh = unitCubeMesh;
            }
            var room2Bounds = new Bounds(new Vector3(113f, 3.2f, 95f), new Vector3(16f, 5f, 16f));
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (r.enabled && r.bounds.Intersects(room2Bounds))
                    Debug.Log($"[Room2Diag] {r.name} pos={r.transform.position} rot={r.transform.eulerAngles} scale={r.transform.lossyScale} mesh={(r.GetComponent<MeshFilter>()?.sharedMesh?.name)} mat={r.sharedMaterial?.name}");
            }

            var oldPolish = GameObject.Find("19 Lab visual and gameplay polish");
            if (oldPolish) Object.DestroyImmediate(oldPolish);
            var polishRoot = new GameObject("19 Lab visual and gameplay polish").transform;

            var m = Object.FindAnyObjectByType<VaccineMission>();
            UpgradeSceneInteractables(m, polishRoot);
            UpgradeLightingAndPostProcessing(m.player, polishRoot, true);

            LabDistrictBuilder.Bake();
            AddTrainGangwayGuards(m.train.train);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Vector3 trainCam = m.train.train.TransformPoint(new Vector3(123.5f, -8.8f, 21.6f));
            Vector3 trainLook = m.train.train.TransformPoint(new Vector3(116.0f, -9.2f, 21.6f));
            var vacObj = Object.FindObjectsByType<VaccineInteractable>(FindObjectsInactive.Include).FirstOrDefault(v => v.action == VaccineUse.Vaccine);
            Vector3 vacPos = vacObj ? vacObj.transform.position : new Vector3(115f, 1.2f, 95f);
            CaptureSceneSnapshots("Lab", new[]
            {
                ("train_gangway", trainCam, trainLook),
                ("lab_facade", new Vector3(55f, 3.2f, 42f), new Vector3(55f, 2.6f, 60f)),
                ("lab_vaccine", vacPos + new Vector3(-3.2f, 1.4f, -3.2f), vacPos)
            });
            VerifyPolishedSetup(m);
        }

        static void UpgradeSceneInteractables(VaccineMission m, Transform polishRoot)
        {
            var c4Mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Art/InteractC4.mat");
            var techMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Art/InteractTech.mat");
            var vacMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Art/InteractVaccine.mat");
            var medMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Medical case green.mat");

            foreach (var vi in Object.FindObjectsByType<VaccineInteractable>(FindObjectsInactive.Include))
            {
                vi.gameObject.isStatic = false;
                for (int i = vi.transform.childCount - 1; i >= 0; i--)
                {
                    var ch = vi.transform.GetChild(i);
                    if (ch.name.Contains("beacon light")) Object.DestroyImmediate(ch.gameObject);
                }

                var box = vi.GetComponent<BoxCollider>();
                if (!box) box = vi.gameObject.AddComponent<BoxCollider>();

                if (vi.action == VaccineUse.TailCharge)
                {
                    // Make C4 placement target unmistakable and very easy to interact with
                    vi.transform.localScale = new Vector3(0.85f, 0.85f, 0.38f);
                    box.size = new Vector3(2.8f, 2.4f, 3.2f);
                    var r = vi.GetComponent<Renderer>();
                    if (r && c4Mat) r.sharedMaterial = c4Mat;

                    // Add C4 satchel & beacon frame details around the placement point
                    var rig = new GameObject("C4 demolition beacon frame").transform;
                    rig.SetParent(polishRoot, false);
                    rig.position = vi.transform.position;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        bar.name = "C4 frame bracket";
                        bar.transform.SetParent(rig, false);
                        bar.transform.localPosition = new Vector3(s * 0.55f, 0, -0.05f);
                        bar.transform.localScale = new Vector3(0.08f, 0.95f, 0.14f);
                        Object.DestroyImmediate(bar.GetComponent<Collider>());
                        bar.GetComponent<Renderer>().sharedMaterial = c4Mat;
                    }
                    var lightGo = new GameObject("C4 tactical beacon light").AddComponent<Light>();
                    lightGo.transform.SetParent(rig, false);
                    lightGo.transform.localPosition = new Vector3(0, 0.35f, -0.65f);
                    lightGo.type = LightType.Point;
                    lightGo.color = new Color(1.0f, 0.58f, 0.16f);
                    lightGo.intensity = 8.5f;
                    lightGo.range = 7.5f;
                    lightGo.shadows = LightShadows.None;
                }
                else if (vi.medical)
                {
                    // Generous collider on medkits so clicking F never misses
                    box.size = new Vector3(1.55f, 1.35f, 1.55f);
                    foreach (var r in vi.GetComponentsInChildren<Renderer>(true))
                    {
                        r.gameObject.isStatic = false;
                        if (r.name == "Medical case" && medMat) r.sharedMaterial = medMat;
                    }
                    var medLight = new GameObject("Medkit beacon light").AddComponent<Light>();
                    medLight.transform.SetParent(vi.transform, false);
                    medLight.transform.localPosition = new Vector3(0, 0.55f, 0);
                    medLight.type = LightType.Point;
                    medLight.color = new Color(0.25f, 0.95f, 0.55f);
                    medLight.intensity = 4.5f;
                    medLight.range = 5.0f;
                }
                else if (vi.action == VaccineUse.Vaccine)
                {
                    box.size = new Vector3(1.8f, 1.8f, 1.8f);
                    var r = vi.GetComponent<Renderer>();
                    if (r && vacMat) r.sharedMaterial = vacMat;
                    var vLight = new GameObject("Vaccine beacon light").AddComponent<Light>();
                    vLight.transform.SetParent(vi.transform, false);
                    vLight.transform.localPosition = new Vector3(0, 0.55f, 0);
                    vLight.type = LightType.Point;
                    vLight.color = new Color(0.30f, 0.95f, 0.88f);
                    vLight.intensity = 6.0f;
                    vLight.range = 6.0f;
                }
                else
                {
                    // Power generator and supply caches
                    box.size = new Vector3(1.65f, 1.65f, 1.85f);
                    var r = vi.GetComponent<Renderer>();
                    if (r && techMat) r.sharedMaterial = techMat;
                    var tLight = new GameObject("Objective beacon light").AddComponent<Light>();
                    tLight.transform.SetParent(vi.transform, false);
                    tLight.transform.localPosition = new Vector3(0, 0.65f, -0.35f);
                    tLight.type = LightType.Point;
                    tLight.color = new Color(0.22f, 0.88f, 0.85f);
                    tLight.intensity = 5.0f;
                    tLight.range = 5.5f;
                }
            }

            foreach (var panel in Object.FindObjectsByType<LabSecurityPanel>(FindObjectsInactive.Include))
            {
                var box = panel.GetComponent<BoxCollider>();
                if (!box) box = panel.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 1.1f, 0);
                box.size = new Vector3(2.1f, 2.2f, 1.8f);
                foreach (var r in panel.GetComponentsInChildren<Renderer>(true))
                {
                    r.gameObject.isStatic = false;
                    if (r.name.Contains("screen") && techMat) r.sharedMaterial = techMat;
                }
            }
        }

        static void AddTrainGangwayGuards(Transform train)
        {
            if (!train) return;
            for (int i = train.childCount - 1; i >= 0; i--)
            {
                var ch = train.GetChild(i);
                if (ch.name == "Gangway exterior notch guard") Object.DestroyImmediate(ch.gameObject);
            }
            // Seal the 4 exterior inter-car notches at train-local x in [117..118.3] and [135.3..136.6], z outside [20.2..23.0]
            foreach (float gx in new[] { 117.65f, 135.95f })
            {
                foreach (float gz in new[] { 18.8f, 24.4f })
                {
                    var guard = new GameObject("Gangway exterior notch guard");
                    guard.transform.SetParent(train, false);
                    guard.transform.localPosition = new Vector3(gx, -10.3f, gz);
                    var col = guard.AddComponent<BoxCollider>();
                    col.size = new Vector3(1.75f, 3.6f, 2.85f);
                }
            }
        }

        static void UpgradeLightingAndPostProcessing(VaccineWalkthrough player, Transform polishRoot, bool labDistrict)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.22f, 0.32f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.14f, 0.19f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.07f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = labDistrict ? 0.0032f : 0.0035f;
            RenderSettings.fogColor = new Color(0.045f, 0.068f, 0.098f);
            RenderSettings.reflectionIntensity = 0.45f;

            // Enhance directional moonlight
            var dirLight = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (dirLight)
            {
                dirLight.color = new Color(0.62f, 0.76f, 0.95f);
                dirLight.intensity = 0.92f;
                dirLight.shadows = LightShadows.Soft;
                dirLight.shadowStrength = 0.78f;
                dirLight.shadowBias = 0.04f;
            }

            // Enable Post-Processing on cameras
            foreach (var cam in new[] { player.eye, player.overview })
            {
                if (!cam) continue;
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (!data) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = cam == player.eye;
            }

            // Create URP Global Volume Profile for Bloom, ACES Tonemapping, Color Adjustments, and Vignette
            Directory.CreateDirectory(Root + "/Settings");
            string profilePath = Root + "/Settings/VaccinePolishedVolume_" + (labDistrict ? "Lab" : "Surface") + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.components.Clear();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.92f);
            bloom.intensity.Override(0.65f);
            bloom.scatter.Override(0.68f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.28f);
            colorAdj.contrast.Override(12f);
            colorAdj.saturation.Override(8f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.42f);

            EditorUtility.SetDirty(profile);

            var volGo = new GameObject("Tactical Post-Processing Volume");
            volGo.transform.SetParent(polishRoot, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.sharedProfile = profile;
        }

        static void CaptureSceneSnapshots(string prefix, (string tag, Vector3 pos, Vector3 lookAt)[] views)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("Logs/PolishScreenshots");
            var camGo = new GameObject("SnapshotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            var uCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            uCam.renderPostProcessing = true;

            bool prevAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            foreach (var v in views)
            {
                camGo.transform.position = v.pos;
                camGo.transform.LookAt(v.lookAt);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes($"Logs/PolishScreenshots/{prefix}_{v.tag}.png", tex.EncodeToPNG());
            }
            ShaderUtil.allowAsyncCompilation = prevAsync;
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
        }

        static void VerifyPolishedSetup(VaccineMission m)
        {
            // Verify gangway clamping rescues exterior notch positions
            Vector3 inNotch = new Vector3(117.6f, -9.0f, 18.9f);
            Vector3 clamped = LabDistrictReview.ClampTrainLocal(inNotch, false);
            Debug.Log($"[PolishVerify] Gangway notch clamp test: {inNotch} -> {clamped} (z in [20.55, 22.65]: {clamped.z >= 20.54f && clamped.z <= 22.66f})");
            if (m && m.tailCharge)
                Debug.Log($"[PolishVerify] TailCharge BoxCollider size: {m.tailCharge.GetComponent<BoxCollider>().size}");
        }

        static void UpgradeInfectedModelsAndPrefabs()
        {
            const string EnemyRoot = "Assets/Enemies/";
            string atlasPath = EnemyRoot + "Textures/EnemySurfaceAtlas.png";
            string normalPath = EnemyRoot + "Textures/EnemySurfaceAtlas_Normal.png";

            // 1. Generate normal map for EnemySurfaceAtlas.png if needed
            var atlasImp = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            if (atlasImp)
            {
                atlasImp.isReadable = true;
                atlasImp.maxTextureSize = 1024;
                atlasImp.filterMode = FilterMode.Trilinear;
                atlasImp.anisoLevel = 4;
                atlasImp.SaveAndReimport();

                var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
                if (atlasTex)
                    BuildNormalMapFromAlbedo(atlasTex, normalPath, 3.4f);

                atlasImp.isReadable = false;
                atlasImp.SaveAndReimport();
            }
            AssetDatabase.Refresh();

            var nImp = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (nImp)
            {
                nImp.textureType = TextureImporterType.NormalMap;
                nImp.sRGBTexture = false;
                nImp.mipmapEnabled = true;
                nImp.wrapMode = TextureWrapMode.Clamp;
                nImp.filterMode = FilterMode.Trilinear;
                nImp.anisoLevel = 4;
                nImp.maxTextureSize = 1024;
                nImp.textureCompression = TextureImporterCompression.CompressedHQ;
                nImp.SaveAndReimport();
            }

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            var atlasNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var infShader = Shader.Find("Universal Render Pipeline/InfectedCharacter") ?? Shader.Find("Universal Render Pipeline/Lit");

            var mats = new Dictionary<string, Material>();
            void ConfigEnemyMat(string name, Color baseCol, float smooth, float metal, float bump, Color sssCol, float vein, Color? emission = null, float pulseStr = 0f, float pulseSpd = 2.8f, bool useAtlas = true)
            {
                string path = EnemyRoot + "Materials/" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m)
                {
                    m = new Material(infShader);
                    AssetDatabase.CreateAsset(m, path);
                }
                else m.shader = infShader;

                m.enableInstancing = true;
                m.SetColor("_BaseColor", baseCol);
                m.SetTexture("_BaseMap", useAtlas ? atlas : null);
                m.SetTexture("_BumpMap", useAtlas ? atlasNormal : null);
                m.SetFloat("_BumpScale", bump);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                m.SetColor("_SubsurfaceColor", sssCol);
                m.SetFloat("_VeinStrength", vein);
                m.SetFloat("_PulseStrength", pulseStr);
                m.SetFloat("_PulseSpeed", pulseSpd);
                if (emission.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission.Value);
                }
                else
                {
                    m.SetColor("_EmissionColor", Color.black);
                }
                EditorUtility.SetDirty(m);
                mats[name] = m;
            }

            ConfigEnemyMat("Skin",   new Color(0.92f, 0.95f, 0.86f), 0.36f, 0.00f, 1.35f, new Color(0.45f, 0.08f, 0.05f), 0.45f);
            ConfigEnemyMat("Muscle", new Color(1.00f, 0.80f, 0.78f), 0.64f, 0.02f, 1.55f, new Color(0.78f, 0.06f, 0.04f), 0.55f);
            ConfigEnemyMat("Mutant", new Color(0.84f, 0.98f, 0.90f), 0.48f, 0.04f, 1.45f, new Color(0.16f, 0.58f, 0.32f), 0.50f, new Color(0.03f, 0.10f, 0.06f), 0.35f, 2.4f);
            ConfigEnemyMat("Cloth",  new Color(0.86f, 0.88f, 0.92f), 0.16f, 0.02f, 1.25f, new Color(0.06f, 0.08f, 0.10f), 0.10f);
            ConfigEnemyMat("Bone",   new Color(0.84f, 0.78f, 0.58f), 0.42f, 0.05f, 0.95f, new Color(0.38f, 0.22f, 0.10f), 0.22f, null, 0f, 2.8f, true);
            ConfigEnemyMat("Acid",   new Color(0.58f, 0.92f, 0.18f), 0.74f, 0.05f, 1.15f, new Color(0.35f, 0.82f, 0.10f), 0.35f, new Color(0.26f, 0.68f, 0.06f), 0.55f, 3.4f, true);
            ConfigEnemyMat("Metal",  new Color(0.48f, 0.52f, 0.55f), 0.58f, 0.78f, 1.10f, new Color(0.10f, 0.12f, 0.14f), 0.12f, null, 0f, 2.8f, true);
            ConfigEnemyMat("Dark",   new Color(0.03f, 0.04f, 0.04f), 0.55f, 0.10f, 0.50f, new Color(0.18f, 0.02f, 0.02f), 0.00f, null, 0f, 2.8f, false);

            string[] enemyNames = { "Shambler", "Sprinter", "Spitter", "Bloater", "Regurgitator", "Worm", "PukeWorm" };
            string[] prefabFolders = { "Assets/Enemies/Prefabs/", "Assets/Mission/Prefabs/", "Assets/Vaccine/Combat/Prefabs/" };

            foreach (string name in enemyNames)
            {
                string fbxPath = EnemyRoot + "Models/" + name + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
                if (!importer) continue;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.isReadable = false;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                foreach (var kv in mats)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);

                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.name = clip.takeName.Substring(clip.takeName.LastIndexOf('_') + 1);
                    clip.loopTime = clip.name == "Idle" || clip.name == "Move";
                    clip.lockRootRotation = true;
                    clip.lockRootPositionXZ = true;
                    clip.lockRootHeightY = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();

                // Update AnimatorController states to point to the freshly imported FBX animation clips
                string controllerPath = EnemyRoot + "Animations/" + name + ".controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller)
                {
                    var machine = controller.layers[0].stateMachine;
                    foreach (var old in machine.states.ToArray()) machine.RemoveState(old.state);
                    foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                    {
                        var state = machine.AddState(clip.name);
                        state.motion = clip;
                        state.writeDefaultValues = true;
                        if (clip.name == "Idle") machine.defaultState = state;
                    }
                    EditorUtility.SetDirty(controller);
                }

                var fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                var fbxSmr = fbxGo ? fbxGo.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                if (!fbxSmr) continue;
                var resolvedMats = fbxSmr.sharedMaterials.Select(m => m && mats.TryGetValue(m.name, out var targetMat) ? targetMat : mats["Skin"]).ToArray();

                foreach (string folder in prefabFolders)
                {
                    string pPath = folder + name + ".prefab";
                    if (!AssetDatabase.LoadAssetAtPath<GameObject>(pPath)) continue;
                    var rootGo = PrefabUtility.LoadPrefabContents(pPath);
                    foreach (var smr in rootGo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        smr.sharedMesh = fbxSmr.sharedMesh;
                        smr.sharedMaterials = resolvedMats;
                        smr.shadowCastingMode = ShadowCastingMode.On;
                        smr.receiveShadows = true;
                        smr.quality = SkinQuality.Bone2;
                        smr.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(6, 7, 8));
                    }
                    PrefabUtility.SaveAsPrefabAsset(rootGo, pPath);
                    PrefabUtility.UnloadPrefabContents(rootGo);
                }
            }

            // Open EnemyPreview.unity, refresh Display instances, sample Idle animation, and capture verification screenshots
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Enemies/Scenes/EnemyPreview.unity"))
            {
                var prevScene = EditorSceneManager.OpenScene("Assets/Enemies/Scenes/EnemyPreview.unity");
                foreach (var l in Object.FindObjectsByType<Light>())
                    l.shadows = LightShadows.Soft;
                foreach (string name in enemyNames)
                {
                    var disp = GameObject.Find("Display " + name);
                    var fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRoot + "Models/" + name + ".fbx");
                    var fbxSmr = fbxGo ? fbxGo.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                    if (!disp || !fbxSmr) continue;
                    var resolvedMats = fbxSmr.sharedMaterials.Select(m => m && mats.TryGetValue(m.name, out var targetMat) ? targetMat : mats["Skin"]).ToArray();
                    foreach (var smr in disp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        PrefabUtility.RevertObjectOverride(smr, InteractionMode.AutomatedAction);
                        smr.sharedMesh = fbxSmr.sharedMesh;
                        smr.sharedMaterials = resolvedMats;
                        smr.shadowCastingMode = ShadowCastingMode.On;
                        smr.receiveShadows = true;
                    }
                    var anim = disp.GetComponentInChildren<Animator>();
                    if (anim && anim.runtimeAnimatorController)
                    {
                        var idleClip = anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Idle");
                        if (idleClip) idleClip.SampleAnimation(anim.gameObject, 0.35f);
                    }
                }
                EditorSceneManager.MarkSceneDirty(prevScene);
                EditorSceneManager.SaveScene(prevScene);

                CaptureSceneSnapshots("Enemies", new[]
                {
                    ("lineup",     new Vector3(0.5f, 3.0f, 8.8f),  new Vector3(0.5f, 1.45f, 0f)),
                    ("boss_heavy", new Vector3(-3.6f, 2.4f, 5.2f), new Vector3(-3.8f, 1.65f, 0f)),
                    ("specials",   new Vector3(5.6f, 2.0f, 4.5f),  new Vector3(5.8f, 1.35f, 0f))
                });
            }
        }

        static void PolishMainMenuAndCapture()
        {
            MenuPolish.Apply();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("Logs/PolishScreenshots");

            // 1. Render SquadPortrait camera so the RawImage in MainMenu has the fresh 3D dropship squad render
            var diorama = Object.FindAnyObjectByType<SASCoop.SquadDiorama>();
            if (diorama && diorama.portraitCamera)
            {
                var pCam = diorama.portraitCamera.GetComponent<Camera>();
                if (pCam && pCam.targetTexture)
                {
                    pCam.Render();
                    var prevActive = RenderTexture.active;
                    RenderTexture.active = pCam.targetTexture;
                    var pTex = new Texture2D(pCam.targetTexture.width, pCam.targetTexture.height, TextureFormat.RGB24, false);
                    pTex.ReadPixels(new Rect(0, 0, pTex.width, pTex.height), 0, 0);
                    pTex.Apply();
                    File.WriteAllBytes("Logs/PolishScreenshots/Menu_squad_diorama.png", pTex.EncodeToPNG());
                    Object.DestroyImmediate(pTex);
                    RenderTexture.active = prevActive;
                }
            }

            // 2. Render full composited MainMenu UI + 3D Warzone Backdrop
            var menu = Object.FindAnyObjectByType<SASCoop.MainMenu>();
            var menuCamObj = GameObject.Find("Menu camera");
            var menuCam = menuCamObj ? menuCamObj.GetComponent<Camera>() : null;
            var canvas = menu ? menu.GetComponent<Canvas>() : null;
            if (menu && menuCam && canvas)
            {
                // Populate default preview strings on menu labels for the screenshot
                if (menu.missionLabel)
                    menu.missionLabel.text = "02   VACCINE\nRestore subway power. Survive the train journey, recover the vaccine and extract.";
                if (menu.roleDetail)
                    menu.roleDetail.text = SASCoop.RoleLoadout.Descriptions[0];
                if (menu.squad)
                    menu.squad.text = "SQUAD 1 / 4\nSOLO OPERATION\nChoose a role. Every role can plant C4 when playing with fewer than four people.";
                if (menu.status)
                    menu.status.text = "READY FOR DEPLOYMENT  //  SELECT OPERATION & SPECIALIST";
                if (menu.deployLabel)
                    menu.deployLabel.text = "DEPLOY SOLO";

                var origMode = canvas.renderMode;
                var origCam = canvas.worldCamera;
                var origDist = canvas.planeDistance;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = menuCam;
                canvas.planeDistance = 0.85f;
                Canvas.ForceUpdateCanvases();

                var rt = new RenderTexture(1366, 768, 24, RenderTextureFormat.ARGB32);
                var tex = new Texture2D(1366, 768, TextureFormat.RGB24, false);
                menuCam.targetTexture = rt;
                menuCam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1366, 768), 0, 0);
                tex.Apply();
                File.WriteAllBytes("Logs/PolishScreenshots/Menu_full_ui.png", tex.EncodeToPNG());

                menuCam.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);

                canvas.renderMode = origMode;
                canvas.worldCamera = origCam;
                canvas.planeDistance = origDist;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
