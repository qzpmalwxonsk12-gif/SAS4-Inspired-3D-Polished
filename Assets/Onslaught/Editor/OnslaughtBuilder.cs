using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Onslaught.Editor
{
    public static class OnslaughtBuilder
    {
        public const string Root = "Assets/Onslaught";
        public const string ScenePath = Root + "/Scenes/Onslaught_Blockout.unity";
        // Image coordinates are the layout source: 1008 x 567 pixels, origin at top left.
        // Scale and heights are estimates for a first-person walkthrough, not measured game dimensions.
        public const float MetresPerPixel = .07f;
        public static Vector3 Point(float x, float y, float height = 0) => new Vector3((x - 445) * MetresPerPixel, height, (286 - y) * MetresPerPixel);
        static Material floor, wall, trim, prop, ramp, marker;
        static Transform hull, partitions, fixtures, ramps;

        [MenuItem("Onslaught/Create Map Blockout")]
        public static void Create()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            if (File.Exists(ScenePath)) throw new System.InvalidOperationException("Blockout already exists; edit its saved objects.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Save the current scene before replacing it.");
            foreach (string folder in new[] { "Scenes", "Materials", "Geometry", "Input", "Settings" }) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            floor = Material("Deck Grey", .46f); wall = Material("Wall Grey", .7f);
            trim = Material("Edge Grey", .30f); prop = Material("Fixture Grey", .51f);
            ramp = Material("Ramp Grey", .56f); marker = Material("Player Marker", .12f);
            hull = new GameObject("01 - Hull and deck").transform;
            partitions = new GameObject("02 - Central room and jumpseats").transform;
            fixtures = new GameObject("03 - Blockout fixtures").transform;
            ramps = new GameObject("04 - Open boarding ramps - map ends here").transform;

            Vector2[] body = Points(165,140, 225,122, 310,116, 405,115, 495,125, 575,145, 645,175,
                650,188, 650,387, 638,404, 575,430, 495,449, 405,458, 310,456, 225,446, 165,425);
            Vector2[] nose = Points(165,180, 165,398, 135,392, 110,365, 92,330, 83,288, 88,244, 105,210, 132,185);
            Slab("Main ship deck", body, hull, floor);
            Slab("Rounded forward jumpseat deck", nose, hull, floor);
            Vector2[] boundary = Points(165,140, 225,122, 310,116, 405,115, 495,125, 575,145, 645,175,
                650,188, 650,387, 638,404, 575,430, 495,449, 405,458, 310,456, 225,446, 165,425,
                165,398, 135,392, 110,365, 92,330, 83,288, 88,244, 105,210, 132,185, 165,180);
            for (int i = 0; i < boundary.Length; i++)
            {
                Vector2 a = boundary[i], b = boundary[(i + 1) % boundary.Length];
                if (a.x == 650 && b.x == 650) continue; // Entire ramp approach stays open; no central divider.
                Segment("Hull wall " + i, a, b, 3.3f, .35f, hull, wall);
            }

            // One central cargo room, open east into the broad approach. North/south doors connect the side passages.
            Segment("Jumpseat upper partition", new Vector2(245,182), new Vector2(245,265), 3.3f, .42f, partitions, wall);
            Segment("Jumpseat lower partition", new Vector2(245,310), new Vector2(245,393), 3.3f, .42f, partitions, wall);
            Segment("Jumpseat north bulkhead", new Vector2(140,182), new Vector2(245,182), 3.3f, .42f, partitions, wall);
            Segment("Jumpseat south bulkhead", new Vector2(140,393), new Vector2(245,393), 3.3f, .42f, partitions, wall);
            foreach (float y in new[] { 178f, 394f })
            {
                Segment("Cargo room wall west " + y, new Vector2(245,y), new Vector2(310,y), 3.3f, .42f, partitions, wall);
                Segment("Cargo room wall east " + y, new Vector2(370,y), new Vector2(448,y), 3.3f, .42f, partitions, wall);
                Header("Open side passage doorway " + y, 340, y, 60, false);
            }
            Segment("Cargo room northeast wall", new Vector2(448,178), new Vector2(448,234), 3.3f, .42f, partitions, wall);
            Segment("Cargo room southeast wall", new Vector2(448,341), new Vector2(448,394), 3.3f, .42f, partitions, wall);
            Rect("Northeast projecting bulkhead", 442,234,476,265,3.3f,partitions,wall);
            Rect("Southeast projecting bulkhead", 442,310,476,341,3.3f,partitions,wall);
            Header("Open forward jumpseat doorway",245,287.5f,45,true);
            Header("Open central room mouth",462,287.5f,45,true);

            // Only large visible fixtures from the supplied image; no invented cargo maze.
            Rect("Upper jumpseat bank",183,194,231,222,.65f,fixtures,prop);
            Rect("Upper seat back",183,194,231,199,1.15f,fixtures,trim);
            Rect("Lower jumpseat bank",190,348,231,382,.65f,fixtures,prop);
            Rect("Lower seat back",190,377,231,382,1.15f,fixtures,trim);
            Rect("Forward low console",128,278,180,294,.85f,fixtures,prop);

            // Two adjoining slope lanes, with a walkable strip between them. User explicitly confirmed no solid divider.
            Slope("Upper boarding ramp",650,800,188,272);
            Slope("Open strip between ramps",650,800,272,303);
            GameObject.Find("Open strip between ramps").GetComponent<Renderer>().sharedMaterial = floor;
            Slope("Lower boarding ramp",650,800,303,387);
            SlopeRail("Upper outside ramp edge",188);
            SlopeRail("Lower outside ramp edge",387);
            var end = Segment("End of map low boundary",new Vector2(800,188),new Vector2(800,387),1.05f,.25f,ramps,trim);
            end.transform.position += Vector3.down * .9f;
            foreach (float y in new[] { 188f, 387f }) Rect("Ramp opening jamb",643,y-3,650,y+3,3.3f,ramps,wall);
            // Simplified engine silhouettes outside the playable hull; no walkable exterior extension.
            Rect("Upper engine housing",493,77,690,112,1.9f,fixtures,trim,false);
            Rect("Lower engine housing",493,461,690,498,1.9f,fixtures,trim,false);

            var light = new GameObject("Single soft blockout light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(55,-35,0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f,.6f,.6f);
            RenderSettings.skybox = null; RenderSettings.fog = false;
            CreateExplorer();
            PlayerSettings.productName = "Onslaught - Map Blockout";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath,true) };
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            AssetDatabase.SaveAssets();
            SceneView.lastActiveSceneView?.LookAt(Point(445,286),Quaternion.Euler(60,0,0),38);
            Debug.Log("Onslaught blockout saved. Trace scale 0.07 m/pixel; no solid divider between ramps; all exploration openings clear.");
        }
        static void CreateExplorer()
        {
            var go = new GameObject("Explorer"); go.layer = 2; go.tag = "Player";
            go.transform.SetPositionAndRotation(Point(540,286,.08f),Quaternion.Euler(0,-90,0));
            var body = go.AddComponent<CharacterController>(); body.height = 1.8f; body.center = new Vector3(0,.9f,0);
            body.radius = .32f; body.skinWidth = .03f; body.stepOffset = .25f; body.slopeLimit = 45;
            var player = go.AddComponent<ExplorerController>(); player.input = Input();
            var eye = new GameObject("First person camera").AddComponent<Camera>(); eye.transform.SetParent(go.transform,false);
            eye.transform.localPosition = Vector3.up * 1.62f; eye.fieldOfView = 75; eye.nearClipPlane = .05f; eye.farClipPlane = 100;
            eye.tag = "MainCamera"; SetupCamera(eye); eye.gameObject.AddComponent<AudioListener>(); player.eye = eye;
            var overview = new GameObject("Reference aligned overhead camera").AddComponent<Camera>();
            overview.transform.SetPositionAndRotation(Point(445,286,65),Quaternion.Euler(90,0,0));
            overview.orthographic = true; overview.orthographicSize = 19; overview.nearClipPlane = .1f; overview.farClipPlane = 120;
            SetupCamera(overview); overview.enabled = false; player.overview = overview;
            var spot = Box("Your position in overhead view",go.transform,Vector3.zero,new Vector3(.65f,.05f,.65f),marker,false);
            spot.transform.localPosition = new Vector3(0,.05f,0); spot.layer = 2; spot.isStatic = false; player.mapMarker = spot; spot.SetActive(false);
            var canvas = new GameObject("Walkthrough instructions",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
            Label(canvas.transform,"ONSLAUGHT  /  MAP BLOCKOUT",new Vector2(.5f,1),new Vector2(0,-18),22);
            Label(canvas.transform,"WASD  move     SHIFT  sprint     TAB  overhead     ENTER  reset     ESC  cursor / click to resume",new Vector2(.5f,0),new Vector2(0,20),16);
            Selection.activeGameObject = go;
        }
        static void SetupCamera(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.11f,.12f,.14f);
            camera.allowHDR = false; camera.allowMSAA = false; camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }
        static InputActionAsset Input()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>(); var map = asset.AddActionMap("Explore");
            map.AddAction("Move",InputActionType.Value,expectedControlLayout:"Vector2").AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            map.AddAction("Look",InputActionType.Value,"<Mouse>/delta",expectedControlLayout:"Vector2");
            map.AddAction("Sprint",InputActionType.Button,"<Keyboard>/leftShift");
            map.AddAction("Release",InputActionType.Button,"<Keyboard>/escape");
            map.AddAction("Focus",InputActionType.Button,"<Mouse>/leftButton");
            map.AddAction("Reset",InputActionType.Button,"<Keyboard>/enter");
            map.AddAction("Map",InputActionType.Button,"<Keyboard>/tab");
            string path = Root + "/Input/Explore.inputactions"; File.WriteAllText(path,asset.ToJson()); Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }
        static void Label(Transform parent,string text,Vector2 anchor,Vector2 offset,int fontSize)
        {
            var label = new GameObject(text,typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); label.transform.SetParent(parent,false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = anchor;
            label.rectTransform.anchoredPosition = offset; label.rectTransform.sizeDelta = new Vector2(1230,40);
            label.text = text; label.font = TMP_Settings.defaultFontAsset; label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false;
        }
        static Material Material(string name,float grey)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); m.name = name;
            m.SetColor("_BaseColor",new Color(grey,grey,grey)); m.SetFloat("_Smoothness",0);
            AssetDatabase.CreateAsset(m,Root+"/Materials/"+name+".mat"); return m;
        }
        static Vector2[] Points(params float[] xy)
        {
            var points = new Vector2[xy.Length/2]; for(int i=0;i<points.Length;i++)points[i]=new Vector2(xy[i*2],xy[i*2+1]); return points;
        }
        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
            go.transform.position=position; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material;
            go.isStatic=true; if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static GameObject Rect(string name,float x1,float y1,float x2,float y2,float height,Transform parent,Material material,bool solid=true)
            => Box(name,parent,Point((x1+x2)/2,(y1+y2)/2,height/2),new Vector3((x2-x1)*MetresPerPixel,height,(y2-y1)*MetresPerPixel),material,solid);
        static GameObject Segment(string name,Vector2 a,Vector2 b,float height,float width,Transform parent,Material material)
        {
            Vector3 p=Point(a.x,a.y),q=Point(b.x,b.y);
            var go=Box(name,parent,(p+q)/2+Vector3.up*height/2,new Vector3(width,height,Vector3.Distance(p,q)+width*.4f),material);
            go.transform.rotation=Quaternion.LookRotation(q-p); return go;
        }
        static void Header(string name,float x,float y,float pixels,bool alongY)
        {
            Vector3 size=alongY?new Vector3(.42f,.5f,pixels*MetresPerPixel):new Vector3(pixels*MetresPerPixel,.5f,.42f);
            Box(name,partitions,Point(x,y,3.05f),size,trim);
        }
        static void Slab(string name,Vector2[] polygon,Transform parent,Material material)
        {
            // Each deck piece is convex. A centre fan supplies upward-facing triangles and a solid underside.
            var vertices=new List<Vector3>(); Vector2 centre=Vector2.zero; foreach(var p in polygon)centre+=p; centre/=polygon.Length;
            vertices.Add(Point(centre.x,centre.y)); vertices.Add(Point(centre.x,centre.y,-.3f));
            foreach(var p in polygon){vertices.Add(Point(p.x,p.y));vertices.Add(Point(p.x,p.y,-.3f));}
            var triangles=new List<int>();
            for(int i=0;i<polygon.Length;i++)
            {
                int a=2+i*2,b=2+((i+1)%polygon.Length)*2;
                triangles.AddRange(new[]{0,a,b,1,b+1,a+1,a,a+1,b+1,a,b+1,b});
            }
            var mesh=new Mesh{name=name}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            PhysicsEditorMeshExtensions.SetPreBakeCollisionMesh(mesh,false,true);
            AssetDatabase.CreateAsset(mesh,Root+"/Geometry/"+name+".asset");
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)); go.transform.SetParent(parent,false); go.isStatic=true;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshCollider>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=material;
        }
        static void Slope(string name,float x1,float x2,float y1,float y2)
        {
            float run=(x2-x1)*MetresPerPixel,drop=.9f;
            var go=Box(name,ramps,Point((x1+x2)/2,(y1+y2)/2,-drop/2-.15f),new Vector3(Mathf.Sqrt(run*run+drop*drop),.3f,(y2-y1)*MetresPerPixel),ramp);
            go.transform.rotation=Quaternion.Euler(0,0,-Mathf.Atan2(drop,run)*Mathf.Rad2Deg);
        }
        static void SlopeRail(string name,float y)
        {
            float run=150*MetresPerPixel;
            var go=Box(name,ramps,Point(725,y,.1f),new Vector3(Mathf.Sqrt(run*run+.81f),1.1f,.2f),trim);
            go.transform.rotation=Quaternion.Euler(0,0,-Mathf.Atan2(.9f,run)*Mathf.Rad2Deg);
        }
    }
}
