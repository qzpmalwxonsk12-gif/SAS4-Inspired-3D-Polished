using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Onslaught.Editor
{
    public static class ShipRefinement
    {
        const string Root = "Assets/Onslaught";
        const float Scale = 1.3f;
        static Material steel, dark, deck, amber, cyan, black;
        static Transform details, roofs;
        static readonly Dictionary<string, Mesh> tiled = new Dictionary<string, Mesh>();
        public static Vector3 P(float x, float y, float height = 0) => new Vector3((x - 445) * .091f, height, (286 - y) * .091f);

        [MenuItem("Onslaught/Apply Ship Refinement")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play first");
            if (GameObject.Find("Ship layout - 130 percent") != null) throw new System.InvalidOperationException("Refinement already applied; edit the saved scene");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != OnslaughtBuilder.ScenePath || scene.isDirty) throw new System.InvalidOperationException("Open and save the Onslaught scene first");
            Directory.CreateDirectory(Root + "/Geometry/UV"); AssetDatabase.Refresh();
            var texturePath = Root + "/Textures/WornShipMetal.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.maxTextureSize = 1024; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 2; importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed; importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            steel = Mat("Worn blue steel", new Color(.65f,.72f,.77f),texture);
            deck = Mat("Worn deck plating", new Color(.49f,.54f,.56f),texture);
            dark = Mat("Dark reinforced steel", new Color(.32f,.37f,.4f),texture);
            amber = Mat("Amber practical",new Color(1,.47f,.12f),null,true);
            cyan = Mat("Screen phosphor",new Color(.18f,.68f,.72f),null,true);
            black = Mat("Rubber and recesses",new Color(.035f,.05f,.055f));

            var layout = new GameObject("Ship layout - 130 percent").transform;
            foreach (string name in new[] { "01 - Hull and deck", "02 - Central room and jumpseats", "03 - Blockout fixtures", "04 - Open boarding ramps - map ends here" })
                GameObject.Find(name).transform.SetParent(layout,false);
            layout.localScale = Vector3.one * Scale;
            var explorer = Object.FindAnyObjectByType<ExplorerController>();
            explorer.transform.position *= Scale;
            explorer.overview.transform.position *= Scale; explorer.overview.orthographicSize *= Scale;
            explorer.overview.farClipPlane = 150;
            foreach(var renderer in layout.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = renderer.name.Contains("deck") || renderer.name.Contains("ramp") || renderer.name.Contains("strip") ? deck : renderer.name.Contains("housing") || renderer.name.Contains("back") ? dark : steel;
                Tile(renderer.GetComponent<MeshFilter>());
            }
            details = new GameObject("05 - Ship fittings and doors").transform;
            roofs = new GameObject("06 - Ceilings hidden in overhead view").transform;
            explorer.ceiling = roofs.gameObject;
            foreach(string name in new[]{"Main ship deck","Rounded forward jumpseat deck"})
            {
                var source=GameObject.Find(name).GetComponent<MeshFilter>();
                var go=new GameObject(name+" ceiling",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(roofs,false);
                go.transform.position=Vector3.up*4.68f;go.transform.localScale=Vector3.one*Scale;
                go.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;go.GetComponent<MeshRenderer>().sharedMaterial=dark;go.isStatic=true;
            }

            MakeDoor("Auto door - upper side passage",340,178,0,60*.091f,3.64f,true);
            MakeDoor("Auto door - lower side passage",340,394,0,60*.091f,3.64f,true);
            MakeDoor("Auto door - forward control room",245,287.5f,90,45*.091f,3.64f,true);
            var gates = new[]{MakeDoor("Upper boarding gate",650,228.5f,90,81*.091f,4.05f,false),MakeDoor("Lower boarding gate",650,346,90,82*.091f,4.05f,false)};
            explorer.gates=gates;

            // Booth blocks the middle strip only near the ship, leaving its door on the ramp-facing end.
            Wall("Booth rear wall",635,269,635,305,3.65f);
            Wall("Booth upper side",635,269,705,269,3.65f);
            Wall("Booth lower side",635,305,705,305,3.65f);
            Wall("Booth front left jamb",705,269,705,279,3.65f);
            Wall("Booth front right jamb",705,295,705,305,3.65f);
            Box("Booth doorway header",details,P(705,287,2.9f),new Vector3(.25f,.65f,16*.091f),dark);
            Box("Booth ceiling",roofs,P(670,287,3.6f),new Vector3(70*.091f,.22f,36*.091f),dark,false);
            Console("Ramp operations",P(643,287,-.05f),Vector3.right,1.25f);

            // Follow the curve of the forward hull; console faces point inward toward the room centre.
            foreach(var pixel in new[]{new Vector2(121,222),new Vector2(102,254),new Vector2(99,290),new Vector2(109,332),new Vector2(132,365)})
            {
                Vector3 pos=P(pixel.x,pixel.y),facing=(P(190,287)-pos).normalized;
                Console("Forward systems "+pixel.y,pos+facing*.35f,facing,1.7f);
            }
            // Narrow ribs, base rails and practical lights supply depth without heavy graphics.
            foreach(float y in new[]{214f,254f,326f,365f})
                foreach(float x in new[]{252f,437f}) Box("Cargo vertical rib",details,P(x,y,2.1f),new Vector3(.18f,4.2f,.16f),dark,false);
            foreach(var p in new[]{new Vector2(160,287),new Vector2(340,145),new Vector2(340,425),new Vector2(340,286),new Vector2(550,225),new Vector2(550,350)})
                Lamp(p.x,p.y,p.x<200 ? new Color(.35f,.62f,.72f) : new Color(.9f,.65f,.35f));
            // Ship-side hazard thresholds and floor guide strips remain geometry, not extra texture sets.
            foreach(float y in new[]{228.5f,346f})
                for(int i=0;i<12;i++)Box("Gate hazard sill",details,P(643,y-37+i*6,.025f),new Vector3(.45f,.025f,.3f),i%2==0?amber:black,false);
            foreach(float x in new[]{278f,410f})
                Box("Cargo aisle guide",details,P(x,286,.015f),new Vector3(.06f,.025f,14),amber,false);
            var mainLight=GameObject.Find("Single soft blockout light").GetComponent<Light>();mainLight.intensity=.45f;mainLight.color=new Color(.65f,.75f,.86f);
            RenderSettings.ambientLight=new Color(.28f,.32f,.38f);
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var settings=new SerializedObject(pipeline);settings.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
            settings.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=4;settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);

            var actions=Object.Instantiate(explorer.input);actions.FindActionMap("Explore").AddAction("Interact",InputActionType.Button,"<Keyboard>/e");
            var inputPath=AssetDatabase.GetAssetPath(explorer.input);File.WriteAllText(inputPath,actions.ToJson());Object.DestroyImmediate(actions);AssetDatabase.ImportAsset(inputPath);
            explorer.input=AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
            var canvas=GameObject.Find("Walkthrough instructions").transform;
            foreach(var label in canvas.GetComponentsInChildren<TMP_Text>())
            {
                if(label.text.Contains("WASD")){label.text="WASD move   SHIFT sprint   E gate   TAB overhead   ENTER reset   ESC cursor";label.fontSize=17;}
                else label.text="ONSLAUGHT  /  MANTIS SHIP";
            }
            var prompt=new GameObject("Gate interaction",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();prompt.transform.SetParent(canvas,false);
            prompt.rectTransform.anchorMin=prompt.rectTransform.anchorMax=prompt.rectTransform.pivot=new Vector2(.5f,.35f);prompt.rectTransform.sizeDelta=new Vector2(700,45);
            prompt.font=TMP_Settings.defaultFontAsset;prompt.fontSize=23;prompt.alignment=TextAlignmentOptions.Center;prompt.color=new Color(1,.73f,.38f);prompt.raycastTarget=false;prompt.text="";explorer.interactionPrompt=prompt;
            foreach(var r in details.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial==steel||r.sharedMaterial==dark||r.sharedMaterial==deck)Tile(r.GetComponent<MeshFilter>());
            PlayerSettings.productName="Onslaught - Mantis Ship";
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("Ship refined: 1.3x layout, five curved-wall consoles, three automatic doors, two boarding gates and ramp-facing booth.");
        }
        static Material Mat(string name,Color color,Texture texture=null,bool unlit=false)
        {
            var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Simple Lit"));m.name=name;m.SetColor("_BaseColor",color);
            if(texture!=null)m.SetTexture("_BaseMap",texture);if(!unlit)m.SetFloat("_Smoothness",.18f);
            AssetDatabase.CreateAsset(m,Root+"/Materials/"+name+".mat");return m;
        }
        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;go.isStatic=true;if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Wall(string name,float x1,float y1,float x2,float y2,float height)
        {
            Vector3 a=P(x1,y1),b=P(x2,y2);var go=Box(name,details,(a+b)/2+Vector3.up*(height/2-.5f),new Vector3(.25f,height+1,Vector3.Distance(a,b)+.25f),steel);
            go.transform.rotation=Quaternion.LookRotation(b-a);
        }
        static ShipDoor MakeDoor(string name,float x,float y,float yaw,float width,float height,bool automatic)
        {
            var root=new GameObject(name);root.transform.SetParent(details,false);root.transform.SetPositionAndRotation(P(x,y),Quaternion.Euler(0,yaw,0));
            var door=root.AddComponent<ShipDoor>();door.automatic=automatic;door.width=width;door.height=height;
            door.barrier=root.AddComponent<BoxCollider>();door.barrier.center=Vector3.up*height/2;door.barrier.size=new Vector3(width,height,.4f);
            door.obstacle=root.AddComponent<NavMeshObstacle>();door.obstacle.shape=NavMeshObstacleShape.Box;door.obstacle.center=door.barrier.center;door.obstacle.size=door.barrier.size;door.obstacle.carving=true;
            var panel=Box("Reinforced sliding panel",root.transform,Vector3.zero,new Vector3(width-.1f,height,.24f),dark,false);panel.transform.localPosition=Vector3.up*height/2;panel.transform.localRotation=Quaternion.identity;panel.isStatic=false;door.panel=panel.transform;
            // Decorations use a unit-scale child of the door, so the moving sheet does not distort their units.
            var decor=new GameObject("Panel fittings").transform;decor.SetParent(panel.transform,false);decor.localScale=new Vector3(1/(width-.1f),1/height,1/.24f);
            for(int i=0;i<6;i++)LocalBox("Shutter reinforcement",decor,new Vector3(0,-height/2+.3f+i*(height-.6f)/5,.17f),new Vector3(width-.22f,.1f,.08f),steel);
            LocalBox("Door status strip",decor,new Vector3(width/2-.25f,0,.19f),new Vector3(.07f,height*.6f,.04f),automatic?cyan:amber);
            foreach(float side in new[]{-1f,1f})LocalBox("Door frame",root.transform,new Vector3(side*(width/2+.1f),height/2,0),new Vector3(.2f,height+.25f,.5f),dark);
            LocalBox("Door motor casing",root.transform,new Vector3(0,height+.15f,0),new Vector3(width+.4f,.3f,.55f),steel);
            return door;
        }
        static GameObject LocalBox(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var go=Box(name,parent,Vector3.zero,size,material,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;go.isStatic=false;return go;
        }
        static void Console(string name,Vector3 position,Vector3 facing,float width)
        {
            var root=new GameObject(name).transform;root.SetParent(details,false);root.SetPositionAndRotation(position,Quaternion.LookRotation(facing));
            var baseGo=LocalBox("Console plinth",root,new Vector3(0,.55f,0),new Vector3(width,1.1f,.65f),dark);baseGo.AddComponent<BoxCollider>();
            LocalBox("Monitor surround",root,new Vector3(0,1.45f,.04f),new Vector3(width, .68f,.25f),steel);
            LocalBox("Monitor glass",root,new Vector3(0,1.45f,.18f),new Vector3(width-.16f,.49f,.04f),black);
            for(int i=0;i<4;i++)LocalBox("Phosphor readout",root,new Vector3(-width*.13f,1.6f-i*.1f,.21f),new Vector3(width*(.4f-i*.05f),.022f,.015f),cyan);
            for(int i=0;i<3;i++)LocalBox("Switch",root,new Vector3(-.4f+i*.32f,1.03f,.38f),new Vector3(.1f,.045f,.08f),i==0?amber:cyan);
        }
        static void Lamp(float x,float y,Color color)
        {
            Box("Ceiling worklight",roofs,P(x,y,4.22f),new Vector3(1.4f,.08f,.2f),amber,false);
            var light=new GameObject("Local shadowless practical").AddComponent<Light>();light.transform.SetParent(details,false);light.transform.position=P(x,y,3.4f);
            light.type=LightType.Point;light.intensity=2.2f;light.range=10;light.color=color;light.shadows=LightShadows.None;
        }
        static void Tile(MeshFilter filter)
        {
            if(filter==null||filter.sharedMesh==null)return;
            var original=filter.sharedMesh;var scale=filter.transform.lossyScale;
            if(original.name.Contains("deck"))
            {
                var uv=new Vector2[original.vertexCount];var verts=original.vertices;for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(verts[i].x*scale.x,verts[i].z*scale.z)/4;
                original.uv=uv;EditorUtility.SetDirty(original);return;
            }
            if(original.name!="Cube")return;
            string key=$"UV_{scale.x:F2}_{scale.y:F2}_{scale.z:F2}".Replace('.','_');
            if(!tiled.TryGetValue(key,out Mesh mesh))
            {
                mesh=Object.Instantiate(original);mesh.name=key;var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    var v=Vector3.Scale(vertices[i],scale);var n=normals[i];
                    uv[i]=Mathf.Abs(n.y)>.5f?new Vector2(v.x,v.z)/4:Mathf.Abs(n.x)>.5f?new Vector2(v.z,v.y)/3:new Vector2(v.x,v.y)/3;
                }
                mesh.uv=uv;AssetDatabase.CreateAsset(mesh,Root+"/Geometry/UV/"+key+".asset");tiled.Add(key,mesh);
            }
            filter.sharedMesh=mesh;
        }
    }
}
