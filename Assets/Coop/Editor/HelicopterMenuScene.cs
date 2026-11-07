using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShipEnemies;

public static class HelicopterMenuScene
{
    static Transform root;
    static Material metal, cloth, armor, black, skin, glass, holoMat, redBeaconMat, cyanGlowMat;

    static Material Mat(string n, Color c, float smooth = .35f, float metallic = .15f, string texName = null, Color? emission = null)
    {
        string path = "Assets/Coop/Art/" + n + ".mat";
        var shader = Shader.Find("Vaccine/TacticalSurface") ?? Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader)
        {
            m.shader = shader;
        }
        m.SetColor("_BaseColor", c);
        m.color = c;
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_UseTriplanar")) m.SetFloat("_UseTriplanar", 0.35f);
        if (m.HasProperty("_TriplanarScale")) m.SetFloat("_TriplanarScale", 0.65f);
        if (m.HasProperty("_GrimeStrength")) m.SetFloat("_GrimeStrength", 0.14f);

        if (!string.IsNullOrEmpty(texName))
        {
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Vaccine/Textures/" + texName + ".png");
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Vaccine/Textures/" + texName + "_Normal.png");
            if (alb) m.SetTexture("_BaseMap", alb);
            if (nrm && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", nrm);
                m.SetFloat("_BumpScale", 1.1f);
            }
        }
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
        return m;
    }

    static Transform Part(string n, Transform parent, Vector3 p, Vector3 s, Material m, PrimitiveType type = PrimitiveType.Cube, Vector3? rot = null)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = n;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = p;
        if (rot.HasValue) g.transform.localRotation = Quaternion.Euler(rot.Value);
        g.transform.localScale = s;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.On;
        r.receiveShadows = true;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        return g.transform;
    }

    static void Limb(string n, Transform t, Vector3 a, Vector3 b, float r, Material m)
    {
        var p = Part(n, t, (a + b) / 2, new Vector3(r, (b - a).magnitude / 2, r), m, PrimitiveType.Capsule);
        p.rotation = t.rotation * Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
    }

    public static RenderTexture Build()
    {
        System.IO.Directory.CreateDirectory("Assets/Coop/Art");
        AssetDatabase.Refresh();

        var old = GameObject.Find("Helicopter briefing diorama");
        if (old) Object.DestroyImmediate(old);
        root = new GameObject("Helicopter briefing diorama").transform;
        root.position = new Vector3(0, 100, 0);
        var motion = root.gameObject.AddComponent<SASCoop.SquadDiorama>();

        metal = Mat("Cabin gunmetal", new Color(.42f, .48f, .52f), .54f, .55f, "Aircraft");
        cloth = Mat("Operator fatigues", new Color(.30f, .38f, .34f), .26f, .05f, "Subway/CarriageRubber");
        armor = Mat("Plate carrier", new Color(.22f, .27f, .31f), .48f, .35f, "SheetMetal");
        black = Mat("Rubber and gun finish", new Color(.14f, .17f, .20f), .58f, .65f, "SheetMetal");
        skin = Mat("Skin", new Color(.64f, .45f, .34f), .32f, .02f);
        glass = Mat("Visor", new Color(.14f, .48f, .56f), .90f, .20f, null, new Color(.08f, .32f, .40f));
        holoMat = Mat("Holo projection", new Color(.16f, .72f, .86f), .92f, .1f, null, new Color(.14f, .68f, .88f));
        redBeaconMat = Mat("Cabin red beacon", new Color(.95f, .22f, .14f), .8f, .1f, null, new Color(1.45f, .20f, .10f));
        cyanGlowMat = Mat("Cabin cyan strip", new Color(.45f, .88f, .98f), .85f, .1f, null, new Color(.38f, .95f, 1.15f));

        // Dropship Cabin Deck & Bulkhead
        Part("Deck", root, new Vector3(0, -.12f, 0), new Vector3(8.6f, .22f, 5.4f), metal);
        Part("Rear bulkhead", root, new Vector3(0, 1.65f, .92f), new Vector3(8.6f, 3.6f, .18f), metal);
        for (int i = 0; i < 9; i++)
        {
            Part("Bulkhead rib", root, new Vector3(-4 + i, 1.65f, .76f), new Vector3(.08f, 3.5f, .16f), armor);
            Part("Deck anti slip rail", root, new Vector3(-4 + i, 0, -1), new Vector3(.05f, .028f, 4.2f), black);
        }
        Part("Bench", root, new Vector3(0, .70f, .14f), new Vector3(7.6f, .20f, .74f), black);
        Part("Bench front trim", root, new Vector3(0, .70f, -.23f), new Vector3(7.6f, .06f, .04f), metal);

        var night = Mat("Night beyond cabin", new Color(.05f, .12f, .18f), .85f, .1f, null, new Color(.05f, .14f, .24f));
        for (int i = 0; i < 4; i++)
        {
            float x = -2.7f + i * 1.8f;
            Part("Recessed window frame", root, new Vector3(x, 2.08f, .78f), new Vector3(1.48f, 1.08f, .20f), black);
            Part("Night window", root, new Vector3(x, 2.08f, .66f), new Vector3(1.28f, .88f, .035f), night);
            Part("Window horizon glow", root, new Vector3(x, 1.78f, .64f), new Vector3(1.24f, .04f, .02f), i % 2 == 0 ? cyanGlowMat : redBeaconMat);
            Part("Overhead strip light", root, new Vector3(x, 2.82f, .08f), new Vector3(.95f, .045f, .14f), i == 0 || i == 3 ? redBeaconMat : cyanGlowMat);
            Part("Bench support", root, new Vector3(x, .34f, .1f), new Vector3(.08f, .6f, .52f), metal);
            Part("Ceiling rib", root, new Vector3(x, 2.96f, -.5f), new Vector3(.10f, .14f, 3.2f), black);
        }
        Part("Overhead panel", root, new Vector3(0, 3.12f, -.3f), new Vector3(8.6f, .16f, 3.2f), metal);
        for (int s = -1; s <= 1; s += 2)
        {
            Part("Cabin side frame", root, new Vector3(s * 4.05f, 1.45f, -.2f), new Vector3(.18f, 3.1f, 2.5f), metal);
            Part("Cabin grab rail", root, new Vector3(s * 3.8f, 1.55f, -.8f), new Vector3(.06f, 2.4f, .06f), black);
        }

        // Foreground Tactical Holo-Projector Table (low profile in front of squad boots)
        Part("Holo table base", root, new Vector3(0, .10f, -2.05f), new Vector3(1.8f, .18f, .62f), armor);
        Part("Holo emitter ring", root, new Vector3(0, .20f, -2.05f), new Vector3(1.55f, .025f, .44f), cyanGlowMat);
        var holoRoot = new GameObject("Holo projection root").transform;
        holoRoot.SetParent(root, false);
        holoRoot.localPosition = new Vector3(0, .30f, -2.05f);
        motion.holoDisplay = holoRoot;
        Part("Holo sector building A", holoRoot, new Vector3(-.35f, .05f, 0), new Vector3(.16f, .14f, .16f), holoMat);
        Part("Holo sector building B", holoRoot, new Vector3(.05f, .08f, .05f), new Vector3(.20f, .20f, .14f), holoMat);
        Part("Holo sector building C", holoRoot, new Vector3(.38f, .04f, -.04f), new Vector3(.22f, .12f, .18f), holoMat);
        Part("Holo transit line", holoRoot, new Vector3(0, -.02f, 0), new Vector3(1.20f, .02f, .06f), holoMat);

        // 4 Specialist Operators (non-emissive cloth/armor trim + subtle emissive visors so armor/weapon details read clearly)
        Color[] roleColors =
        {
            new Color(.90f, .42f, .16f), // 0: Demo / Medic accent
            new Color(.18f, .72f, .78f), // 1: Captain / Engineer accent
            new Color(.88f, .68f, .24f), // 2: Breacher accent
            new Color(.84f, .26f, .22f)  // 3: Heavy / Gunner accent
        };

        for (int i = 0; i < 4; i++)
        {
            var t = new GameObject(SASCoop.RoleLoadout.Names[i] + " seated operator").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(-2.7f + i * 1.8f, 0, 0);
            float bulk = i == 3 ? 1.16f : (i == 2 ? 1.08f : 1f);
            var accent = Mat("Role " + i, roleColors[i], .42f, .18f, "SheetMetal", null);
            var visorMat = Mat("Visor Role " + i, roleColors[i], .92f, .25f, null, roleColors[i] * .65f);

            Part("Seat back", t, new Vector3(0, 1.28f, .55f), new Vector3(1.18f, 1.16f, .16f), black);
            Part("Seat headrest", t, new Vector3(0, 1.98f, .52f), new Vector3(.68f, .28f, .18f), armor);
            motion.operators[i] = t;

            Part("Torso", t, new Vector3(0, 1.42f, 0), new Vector3(.64f * bulk, .39f, .40f), cloth, PrimitiveType.Capsule);
            Part("Ballistic chest plate", t, new Vector3(0, 1.43f, -.22f), new Vector3(.62f * bulk, .57f, .16f), armor);
            Part("Chest trauma trim", t, new Vector3(0, 1.56f, -.305f), new Vector3(.36f * bulk, .07f, .025f), accent);
            Part("Pelvis", t, new Vector3(0, .87f, -.03f), new Vector3(.62f * bulk, .29f, .45f), cloth);
            Part("Tactical belt", t, new Vector3(0, 1.04f, -.04f), new Vector3(.65f * bulk, .10f, .47f), black);
            Part("Neck", t, new Vector3(0, 1.91f, 0), new Vector3(.19f, .20f, .20f), skin, PrimitiveType.Cylinder);
            Part("Head", t, new Vector3(0, 2.1f, -.01f), new Vector3(.37f, .44f, .36f), skin, PrimitiveType.Sphere);
            Part("Helmet", t, new Vector3(0, 2.23f, .01f), new Vector3(.47f, .31f, .46f), armor, PrimitiveType.Sphere);
            Part("Helmet lip", t, new Vector3(0, 2.17f, -.08f), new Vector3(.47f, .06f, .46f), armor);
            Part("Goggles", t, new Vector3(0, 2.12f, -.192f), new Vector3(.33f, .095f, .055f), visorMat);
            Part("Face respirator", t, new Vector3(0, 2.0f, -.175f), new Vector3(.24f, .15f, .14f), black);

            if (i == 0)
            {
                Part("Helmet badge", t, new Vector3(0, 2.29f, -.21f), new Vector3(.12f, .05f, .02f), accent);
                Part("C4 satchel pack", t, new Vector3(-.22f, 1.46f, -.31f), new Vector3(.12f, .18f, .07f), accent);
            }
            else if (i == 1)
            {
                for (int nvg = -1; nvg <= 1; nvg += 2)
                {
                    Part("NVG tube inner", t, new Vector3(nvg * .06f, 2.14f, -.24f), new Vector3(.045f, .045f, .11f), black, PrimitiveType.Cylinder, new Vector3(90, 0, 0));
                    Part("NVG lens inner", t, new Vector3(nvg * .06f, 2.14f, -.29f), new Vector3(.038f, .038f, .02f), visorMat, PrimitiveType.Sphere);
                }
                Part("Comms antenna", t, new Vector3(.32f, 2.05f, .22f), new Vector3(.02f, .55f, .02f), metal);
            }
            else if (i == 2)
            {
                Part("Breacher face shield", t, new Vector3(0, 2.08f, -.24f), new Vector3(.40f, .28f, .035f), glass);
                Part("Ballistic throat guard", t, new Vector3(0, 1.82f, -.18f), new Vector3(.46f, .16f, .24f), armor);
            }
            else if (i == 3)
            {
                Part("Heavy mandible guard", t, new Vector3(0, 1.98f, -.20f), new Vector3(.38f, .18f, .16f), armor);
                Part("Targeting eye optic", t, new Vector3(.11f, 2.13f, -.22f), new Vector3(.07f, .05f, .04f), redBeaconMat);
                Part("Heavy shoulder yoke", t, new Vector3(0, 1.72f, -.05f), new Vector3(.84f, .14f, .44f), armor);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                Limb("Upper arm", t, new Vector3(s * .38f * bulk, 1.7f, 0), new Vector3(s * .49f * bulk, 1.2f, -.14f), .23f, cloth);
                Part("Shoulder plate", t, new Vector3(s * .39f * bulk, 1.65f, -.02f), new Vector3(.25f, .28f, .37f), armor);
                Part("Unit patch", t, new Vector3(s * .41f * bulk, 1.65f, -.21f), new Vector3(.14f, .09f, .03f), accent);
                Limb("Forearm", t, new Vector3(s * .49f * bulk, 1.2f, -.14f), new Vector3(s * .23f, 1.05f, -.53f), .19f, cloth);
                Part("Glove", t, new Vector3(s * .23f, 1.05f, -.53f), new Vector3(.18f, .14f, .2f), black, PrimitiveType.Sphere);
                Limb("Thigh", t, new Vector3(s * .19f * bulk, .83f, -.05f), new Vector3(s * .26f, .75f, -.65f), .28f, cloth);
                Part("Knee protection", t, new Vector3(s * .26f, .76f, -.79f), new Vector3(.24f, .22f, .09f), armor);
                Limb("Shin", t, new Vector3(s * .26f, .68f, -.65f), new Vector3(s * .28f, .22f, -.67f), .22f, cloth);
                Part("Boot", t, new Vector3(s * .28f, .13f, -.78f), new Vector3(.26f, .24f, .44f), black);
            }
            for (int j = 0; j < 3; j++)
                Part("Magazine pouch", t, new Vector3(-.2f + j * .2f, 1.32f, -.34f), new Vector3(.15f, .22f, .11f), j == 1 ? accent : armor);
            for (int s = -1; s <= 1; s += 2)
            {
                Part("Harness webbing", t, new Vector3(s * .22f, 1.5f, -.325f), new Vector3(.065f, .56f, .025f), black);
                Part("Harness buckle", t, new Vector3(s * .22f, 1.56f, -.35f), new Vector3(.09f, .09f, .03f), metal);
            }

            // Detailed Class Weapon
            var gun = new GameObject("Held weapon").transform;
            gun.SetParent(t, false);
            gun.localPosition = new Vector3(-.45f, 1.05f, -.62f);
            gun.localRotation = Quaternion.Euler(0, 0, -8);
            float length = i == 0 ? .68f : i == 2 ? 1.08f : i == 3 ? 1.18f : .92f;
            Part("Receiver", gun, new Vector3(.35f, 0, 0), new Vector3(.54f, .13f, .11f), metal);
            Part("Upper rail", gun, new Vector3(.38f, .08f, 0), new Vector3(.42f, .03f, .06f), black);
            Part("Optic sight", gun, new Vector3(.32f, .12f, 0), new Vector3(.14f, .07f, .06f), armor);
            Part("Optic lens", gun, new Vector3(.39f, .12f, 0), new Vector3(.02f, .05f, .05f), visorMat);
            Part("Stock", gun, new Vector3(.02f, -.02f, 0), new Vector3(.26f, .16f, .1f), black);
            Part("Barrel", gun, new Vector3(.52f + length / 2, 0, 0), new Vector3(length, .048f, .048f), metal);
            Part("Muzzle brake", gun, new Vector3(.52f + length, 0, 0), new Vector3(.09f, .065f, .065f), black);
            Part("Laser module", gun, new Vector3(.68f, .05f, -.05f), new Vector3(.12f, .04f, .04f), accent);
            Part("Magazine", gun, new Vector3(.36f, -.13f, 0), new Vector3(i == 3 ? .22f : .11f, i == 3 ? .32f : .20f, i == 3 ? .22f : .09f), black);
            if (i == 2) Part("Pump grip", gun, new Vector3(.72f, -.04f, 0), new Vector3(.26f, .12f, .12f), accent);
            Part("Overhead harness", t, new Vector3(0, 2.75f, .65f), new Vector3(.9f, .06f, .1f), black);
        }

        // Well-balanced Cabin Lighting + Front Fill Key + Dynamic Selection Spotlight
        for (int i = 0; i < 4; i++)
        {
            var l = new GameObject("Cabin practical light " + i).AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = new Vector3(-2.7f + i * 1.8f, 2.45f, -2.1f);
            l.type = LightType.Point;
            l.range = 12f;
            l.intensity = 6.8f;
            l.color = i == 0 ? new Color(1f, .72f, .55f) : i == 3 ? new Color(.65f, .86f, 1f) : new Color(.92f, .96f, 1f);
            l.shadows = LightShadows.None;
        }
        var frontFill = new GameObject("Cabin front key fill").AddComponent<Light>();
        frontFill.transform.SetParent(root, false);
        frontFill.transform.localPosition = new Vector3(0f, 1.55f, -3.6f);
        frontFill.type = LightType.Point;
        frontFill.range = 16f;
        frontFill.intensity = 7.5f;
        frontFill.color = new Color(.85f, .92f, 1f);
        frontFill.shadows = LightShadows.None;

        var selLight = new GameObject("Operator selection spotlight").AddComponent<Light>();
        selLight.transform.SetParent(root, false);
        selLight.transform.localPosition = new Vector3(-2.7f, 2.75f, -.85f);
        selLight.transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
        selLight.type = LightType.Spot;
        selLight.spotAngle = 44f;
        selLight.innerSpotAngle = 24f;
        selLight.range = 7.5f;
        selLight.intensity = 11f;
        selLight.color = new Color(1f, .90f, .70f);
        selLight.shadows = LightShadows.Soft;
        motion.selectionLight = selLight;

        var cam = new GameObject("Squad portrait camera").AddComponent<Camera>();
        cam.transform.SetParent(root, false);
        cam.transform.localPosition = new Vector3(0, 1.52f, -5.75f);
        cam.transform.LookAt(root.position + new Vector3(0, 1.36f, 0));
        cam.fieldOfView = 26.5f;
        cam.nearClipPlane = .1f;
        cam.farClipPlane = 15f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.025f, .042f, .058f);
        cam.allowHDR = true;
        cam.allowMSAA = true;
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData) camData.renderPostProcessing = true;
        motion.portraitCamera = cam.transform;

        string rtPath = "Assets/Coop/Art/SquadPortrait.renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if (!rt)
        {
            rt = new RenderTexture(1416, 420, 24);
            rt.antiAliasing = 4;
            AssetDatabase.CreateAsset(rt, rtPath);
        }
        else
        {
            rt.Release();
            rt.width = 1416;
            rt.height = 420;
            rt.antiAliasing = 4;
            EditorUtility.SetDirty(rt);
        }
        cam.targetTexture = rt;

        BuildMenuWarzoneBackdrop(motion);

        AssetDatabase.SaveAssets();
        return rt;
    }

    static void BuildMenuWarzoneBackdrop(SASCoop.SquadDiorama motion)
    {
        var oldStage = GameObject.Find("Menu warzone backdrop");
        if (oldStage) Object.DestroyImmediate(oldStage);
        var stage = new GameObject("Menu warzone backdrop").transform;
        stage.position = Vector3.zero;

        // Skybox & Night Fog
        var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Vaccine/Materials/Art/NightSky.mat");
        if (skyMat) RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.28f, 0.36f, 0.46f);
        RenderSettings.ambientEquatorColor = new Color(0.20f, 0.26f, 0.32f);
        RenderSettings.ambientGroundColor = new Color(0.10f, 0.14f, 0.18f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.035f, 0.060f, 0.085f);
        RenderSettings.fogDensity = 0.014f;

        var asphaltMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Vaccine/Materials/Art/Asphalt.mat") ?? metal;
        var concreteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Vaccine/Materials/Art/Concrete.mat") ?? armor;
        var hazardMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Vaccine/Materials/Art/Amber.mat") ?? redBeaconMat;
        var vaccineGlow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Vaccine/Materials/Art/InteractVaccine.mat") ?? cyanGlowMat;
        var c4Glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Vaccine/Materials/Art/InteractC4.mat") ?? redBeaconMat;

        // Helipad & Containment Outpost Ground
        Part("Outpost tarmac", stage, new Vector3(0, -0.25f, 12f), new Vector3(60f, 0.5f, 60f), asphaltMat);
        Part("Containment blast wall", stage, new Vector3(0, 2.4f, 22f), new Vector3(44f, 5.2f, 1.2f), concreteMat);
        for (int i = -3; i <= 3; i++)
        {
            Part("Wall buttress " + i, stage, new Vector3(i * 6f, 2.6f, 21.2f), new Vector3(0.9f, 5.6f, 1.4f), black);
            Part("Perimeter beacon " + i, stage, new Vector3(i * 6f, 5.2f, 20.5f), new Vector3(0.45f, 0.22f, 0.22f), i % 2 == 0 ? redBeaconMat : cyanGlowMat);
        }

        // Foreground Tactical Barricades & Bio-Containment Crate
        Part("Barricade left", stage, new Vector3(-4.2f, 0.55f, 9.5f), new Vector3(3.2f, 1.1f, 0.7f), concreteMat, PrimitiveType.Cube, new Vector3(0, 18f, 0));
        Part("Barricade right", stage, new Vector3(4.6f, 0.55f, 10.2f), new Vector3(3.2f, 1.1f, 0.7f), concreteMat, PrimitiveType.Cube, new Vector3(0, -22f, 0));
        Part("Hazard stripe L", stage, new Vector3(-4.1f, 0.95f, 9.1f), new Vector3(3.0f, 0.16f, 0.1f), hazardMat, PrimitiveType.Cube, new Vector3(0, 18f, 0));
        Part("Hazard stripe R", stage, new Vector3(4.5f, 0.95f, 9.8f), new Vector3(3.0f, 0.16f, 0.1f), hazardMat, PrimitiveType.Cube, new Vector3(0, -22f, 0));

        // Glowing Vaccine Cryo-Case & C4 Pack on Supply Crate
        Part("Biohazard crate", stage, new Vector3(-1.8f, 0.42f, 7.6f), new Vector3(1.2f, 0.84f, 0.85f), armor, PrimitiveType.Cube, new Vector3(0, 14f, 0));
        Part("Vaccine canister showcase", stage, new Vector3(-1.8f, 0.96f, 7.6f), new Vector3(0.55f, 0.26f, 0.38f), vaccineGlow, PrimitiveType.Cube, new Vector3(0, 14f, 0));
        Part("C4 charge showcase", stage, new Vector3(-1.35f, 0.92f, 7.45f), new Vector3(0.28f, 0.16f, 0.22f), c4Glow, PrimitiveType.Cube, new Vector3(0, -10f, 0));

        // Spawn Live 3D Infected Horde Specimens in the Containment Zone
        var enemySpecs = new (string name, Vector3 pos, float yaw)[]
        {
            ("Regurgitator", new Vector3(0.3f, 0f, 11.2f), 186f),
            ("Bloater",      new Vector3(-2.6f, 0f, 10.8f), 162f),
            ("Spitter",      new Vector3(3.1f, 0f, 10.2f), 204f),
            ("Sprinter",     new Vector3(-0.9f, 0f, 8.8f), 175f),
            ("Shambler",     new Vector3(1.9f, 0f, 8.9f), 195f),
            ("Worm",         new Vector3(0.6f, 0f, 7.6f), 190f),
            ("PukeWorm",     new Vector3(-0.4f, 0f, 7.4f), 172f)
        };
        foreach (var spec in enemySpecs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Prefabs/" + spec.name + ".prefab");
            if (!prefab) continue;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "Menu Infected " + spec.name;
            inst.transform.SetParent(stage, false);
            inst.transform.localPosition = spec.pos;
            inst.transform.localRotation = Quaternion.Euler(0f, spec.yaw, 0f);
            var brain = inst.GetComponent<EnemyBrain>();
            if (brain) brain.displayOnly = true;
            var agent = inst.GetComponent<NavMeshAgent>();
            if (agent) agent.enabled = false;
            var col = inst.GetComponent<Collider>();
            if (col) col.enabled = false;
            var anim = inst.GetComponentInChildren<Animator>();
            if (anim)
            {
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var idleClip = anim.runtimeAnimatorController ? anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Idle") : null;
                if (idleClip) idleClip.SampleAnimation(anim.gameObject, 0.35f);
            }
        }

        // Dramatic Warzone Lighting
        var moon = new GameObject("Warzone moonlight").AddComponent<Light>();
        moon.transform.SetParent(stage, false);
        moon.transform.rotation = Quaternion.Euler(38f, -25f, 0f);
        moon.type = LightType.Directional;
        moon.intensity = 1.65f;
        moon.color = new Color(0.75f, 0.88f, 1.0f);
        moon.shadows = LightShadows.Soft;

        var keySpot = new GameObject("Warzone cyan searchlight").AddComponent<Light>();
        keySpot.transform.SetParent(stage, false);
        keySpot.transform.localPosition = new Vector3(-4.5f, 6.5f, 4.0f);
        keySpot.transform.LookAt(new Vector3(0.2f, 1.6f, 10.5f));
        keySpot.type = LightType.Spot;
        keySpot.spotAngle = 72f;
        keySpot.range = 28f;
        keySpot.intensity = 24f;
        keySpot.color = new Color(0.42f, 0.86f, 1.0f);
        keySpot.shadows = LightShadows.Soft;

        var rimSpot = new GameObject("Warzone crimson alert light").AddComponent<Light>();
        rimSpot.transform.SetParent(stage, false);
        rimSpot.transform.localPosition = new Vector3(5.5f, 5.5f, 14.5f);
        rimSpot.transform.LookAt(new Vector3(0f, 1.5f, 10.0f));
        rimSpot.type = LightType.Spot;
        rimSpot.spotAngle = 78f;
        rimSpot.range = 26f;
        rimSpot.intensity = 22f;
        rimSpot.color = new Color(1.0f, 0.28f, 0.18f);
        rimSpot.shadows = LightShadows.Soft;

        // Configure Menu Camera to frame the live 3D warzone stage with Post-Processing
        var menuCamObj = GameObject.Find("Menu camera");
        if (menuCamObj)
        {
            var menuCam = menuCamObj.GetComponent<Camera>();
            menuCamObj.transform.position = new Vector3(0f, 2.05f, 3.4f);
            menuCamObj.transform.LookAt(new Vector3(0.1f, 1.60f, 10.5f));
            if (menuCam)
            {
                menuCam.clearFlags = CameraClearFlags.Skybox;
                menuCam.fieldOfView = 48f;
                menuCam.allowHDR = true;
                menuCam.allowMSAA = true;
                var uData = menuCam.GetUniversalAdditionalCameraData();
                if (uData) uData.renderPostProcessing = true;
            }
            motion.menuCamera = menuCamObj.transform;
        }

        // Global Post-Processing Volume for MainMenu
        var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Vaccine/Settings/VaccinePolishedVolume_Surface.asset");
        if (volumeProfile)
        {
            var volGo = new GameObject("Menu Post Volume");
            volGo.transform.SetParent(stage, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.sharedProfile = volumeProfile;
        }
    }
}
