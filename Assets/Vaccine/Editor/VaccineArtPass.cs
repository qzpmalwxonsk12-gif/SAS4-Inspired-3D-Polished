using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Vaccine.Editor
{
    /// <summary>Applies the original art kit to the approved saved layout through the Editor.</summary>
    public static class VaccineArtPass
    {
        const string Root = "Assets/Vaccine";
        static readonly Dictionary<string, Material> M = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> UVCache = new Dictionary<string, Mesh>();
        static Transform art;
        static int uvIndex;

        [MenuItem("Vaccine/Apply Night Art Pass")]
        public static string Apply()
        {
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != VaccineBlockoutBuilder.ScenePath)
                throw new InvalidOperationException("Open the saved Vaccine scene outside Play.");
            if (GameObject.Find("10 Vaccine night art") != null) throw new InvalidOperationException("Art already applied; edit existing objects instead.");
            foreach (var d in new[] {"Materials/Art", "Geometry/Art"}) Directory.CreateDirectory(Root + "/" + d);
            AssetDatabase.Refresh();
            ImportTextures(); Materials(); UVCache.Clear(); uvIndex=0;
            art = new GameObject("10 Vaccine night art").transform;
            ApplySurfaces(); ReplaceProps(); AircraftDetail(); Architecture(); ElectricalDetail(); Night();
            var p=Object.FindAnyObjectByType<VaccineWalkthrough>(); p.eye.clearFlags=CameraClearFlags.Skybox;
            p.eye.farClipPlane=500;
            foreach (var t in Object.FindObjectsByType<TMP_Text>())
            {
                if (t.text=="VACCINE / BLOCKOUT") t.text="VACCINE / NIGHT REVIEW";
                if (t.text.Contains("untextured")) t.text=t.text.Replace("untextured", "night art");
            }
            Save(); return "Vaccine night art applied; original layout and collision envelopes preserved.";
        }
        static void Save(){ EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); }
        [MenuItem("Vaccine/Finish Night Art Lighting and Set Dressing")]
        public static string Finish()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
            art=GameObject.Find("10 Vaccine night art").transform;
            M.Clear();foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials/Art"}))
            {var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));M[m.name]=m;}
            UVCache.Clear();uvIndex=Directory.GetFiles(Root+"/Geometry/Art","UV-*.asset").Length;
            // Refresh material-slot overrides after re-exporting the Blender models.
            foreach(var r in Object.FindObjectsByType<MeshRenderer>())
            {
                if(!Ancestor(r.transform).Contains("Visual model"))continue;
                var source=PrefabUtility.GetCorrespondingObjectFromSource(r);if(!source)continue;
                bool wreck=Ancestor(r.transform).Contains("wreck");
                r.sharedMaterials=source.sharedMaterials.Select(m=>wreck&&m.name!="Glass"&&m.name!="Rubber"?M["Scorch"]:M.TryGetValue(m.name,out var mat)?mat:M["Steel"]).ToArray();
            }
            M["Glass"].SetFloat("_Cull",0);M["Glass"].SetColor("_BaseColor",new Color(.14f,.27f,.32f));M["Glass"].SetColor("_SpecColor",new Color(.55f,.65f,.7f));EditorUtility.SetDirty(M["Glass"]);
            if(GameObject.Find("11 Final surface details")!=null){Save();return "Model slots refreshed.";}
            var detail=Group("11 Final surface details");
            foreach(var l in Object.FindObjectsByType<Light>())
            {
                if(l.type==LightType.Point&&!l.name.Contains("review"))l.intensity*=12;
                if(l.name=="Inspection torch")l.intensity=30;
            }
            YardLamp(new Vector3(-38,0,-13),true);
            var newLamp=art.GetComponentsInChildren<Light>().Last(l=>l.name=="Yard floodlight");newLamp.intensity=75;
            // Recessed lamps cast downwards in selected interior zones, giving props readable shadows.
            foreach(var l in Object.FindObjectsByType<Light>().Where(l=>l.name=="Practical ceiling light"))
            {
                l.type=LightType.Spot;l.transform.rotation=Quaternion.Euler(90,0,0);l.spotAngle=145;l.innerSpotAngle=90;l.intensity=55;
                l.shadows=(l.transform.position.x>50||l.transform.position.x<0)?LightShadows.Hard:LightShadows.None;
                l.shadowResolution=LightShadowResolution.Low;l.shadowBias=.08f;
            }
            // Ceiling service runs and grilles provide detail without narrowing the walking route.
            foreach(float x in new[]{51.4f,52.1f})
            {
                Rod("Station service pipe",detail,new Vector3(x,4.55f,-27),new Vector3(x,4.55f,11),.09f,"Rust");
                for(int z=-25;z<12;z+=6)Box("Pipe bracket",detail,new Vector3(x,4.7f,z),new Vector3(.35f,.3f,.07f),"Steel");
            }
            foreach(var p in new[]{new Vector3(50.21f,3.2f,-20),new Vector3(50.21f,3.2f,-2)})
            {
                Box("Vent recess",detail,p,new Vector3(.03f,.9f,2),"Rubber");
                for(int i=0;i<8;i++)Box("Vent louvre",detail,p+new Vector3(.03f,-.35f+i*.1f,0),new Vector3(.06f,.035f,1.9f),"Steel");
            }
            for(int i=0;i<10;i++)
            {
                var p=new Vector3(51+i%5*2.6f,.035f,13+i/5*1.4f);
                var slab=Box("Impact floor tile",detail,p,new Vector3(.7f,.08f,.6f),"StationFloor");slab.transform.Rotate(3,i*31,7);
            }
            foreach(float z in new[]{-32f,-16.5f,-1.5f})
            {
                Sign("CENTRAL  /  M2",detail,new Vector3(86.76f,3.25f,z),Quaternion.Euler(0,90,0),4.8f,.7f,2.3f);
                Box("Room radiator",detail,new Vector3(86.65f,.6f,z+4),new Vector3(.2f,.8f,2.7f),"Cream");
                for(int j=0;j<12;j++)Box("Radiator fins",detail,new Vector3(86.5f,.6f,z+2.8f+j*.22f),new Vector3(.08f,.7f,.04f),"Steel");
            }
            // Parking details follow the existing bays; the helicopter and exit retain their clear area.
            foreach(float z in new[]{-37.8f,-1.2f})for(int x=-62;x<-17;x+=7)
                Box("Concrete wheel stop",detail,new Vector3(x,.09f,z),new Vector3(1.8f,.18f,.35f),"Concrete");
            foreach(var p in new[]{new Vector3(-17,.025f,-31),new Vector3(-55,.025f,-7),new Vector3(14,.025f,28)})
            {
                var oil=Box("Oil-darkened ground patch",detail,p,new Vector3(3.5f,.012f,2),"Scorch");oil.transform.Rotate(0,23,0);
            }
            // Dropped papers and scraps are small, pale points of wear, not another repeated floor overlay.
            var rand=new System.Random(321);
            for(int i=0;i<28;i++)
            {
                bool hall=i<14;float x=hall?8+(float)rand.NextDouble()*21:52+(float)rand.NextDouble()*13;
                float z=hall?-4-(float)rand.NextDouble()*19:-25+(float)rand.NextDouble()*31;
                var paper=Box("Discarded paper",detail,new Vector3(x,.022f,z),new Vector3(.2f,.004f,.29f),"Markings");paper.transform.Rotate(0,rand.Next(180),0);
            }
            foreach(var r in Object.FindObjectsByType<MeshRenderer>().Where(r=>r.name=="Overlapping sheet barricade").Where((r,i)=>i%9==0).ToArray())
                Box("Welded repair patch",r.transform,new Vector3(-.54f,-.1f,.12f),new Vector3(.04f,.23f,.22f),"Rust");
            var pcontrol=Object.FindAnyObjectByType<VaccineWalkthrough>();pcontrol.transform.rotation=Quaternion.Euler(0,-84,0);
            Save();return "Night lighting, vehicle glazing, station fittings and restrained surface wear finished.";
        }
        static void ImportTextures()
        {
            foreach(var file in Directory.GetFiles(Root+"/Textures","*.png"))
            {
                var i=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                i.textureType=TextureImporterType.Default;i.sRGBTexture=true;i.mipmapEnabled=true;i.wrapMode=TextureWrapMode.Repeat;i.filterMode=FilterMode.Trilinear;i.anisoLevel=2;i.maxTextureSize=1024;i.textureCompression=TextureImporterCompression.Compressed;i.SaveAndReimport();
            }
        }
        static Material Mat(string key,string tex,Color color,float smooth=.12f,Color? emission=null)
        {
            string path=Root+"/Materials/Art/"+key+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));AssetDatabase.CreateAsset(m,path);}
            m.name=key;m.enableInstancing=true;m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);
            m.SetColor("_SpecColor",new Color(.13f,.15f,.16f));
            if(tex!=null)m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+tex+".png"));
            if(emission.HasValue){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value);}
            M[key]=m;EditorUtility.SetDirty(m);return m;
        }
        static void Materials()
        {
            M.Clear();Mat("Asphalt","Asphalt",Color.white,.08f);Mat("Concrete","Concrete",new Color(.83f,.84f,.83f));
            Mat("StationFloor","StationFloor",new Color(.82f,.86f,.88f),.22f);Mat("StationWall","StationWall",Color.white,.22f);
            Mat("SheetMetal","SheetMetal",new Color(.72f,.77f,.79f),.2f);Mat("Container","SheetMetal",new Color(.34f,.46f,.42f));
            Mat("Aircraft","Aircraft",new Color(.87f,.87f,.83f),.25f);Mat("Helicopter","Helicopter",new Color(.94f,.98f,.93f),.2f);
            Mat("Steel","Aircraft",new Color(.23f,.28f,.3f),.45f);Mat("Rubber",null,new Color(.035f,.043f,.049f),.08f);
            Mat("Glass",null,new Color(.075f,.18f,.23f),.85f);
            Mat("Teal","Aircraft",new Color(.12f,.37f,.37f),.28f);Mat("PaintBlue","Aircraft",new Color(.25f,.42f,.5f),.3f);
            Mat("PaintRed","Aircraft",new Color(.38f,.12f,.085f));Mat("Cream","Aircraft",new Color(.88f,.83f,.69f),.25f);
            Mat("Amber",null,new Color(.68f,.4f,.08f));Mat("Markings",null,new Color(.72f,.69f,.54f));
            Mat("Rust","SheetMetal",new Color(.32f,.19f,.11f));Mat("Scorch","Concrete",new Color(.14f,.13f,.12f));
            Mat("Flesh","Concrete",new Color(.35f,.34f,.23f));Mat("LampCool",null,new Color(.7f,.85f,.9f),.2f,new Color(.8f,1.25f,1.5f));
            Mat("LampWarm",null,new Color(.9f,.65f,.28f),.2f,new Color(1.8f,1.05f,.35f));
            Mat("Emergency",null,new Color(.2f,.6f,.45f),.15f,new Color(.1f,.9f,.5f));
        }
        static string Ancestor(Transform t){string s=t.name;while(t.parent){t=t.parent;s=t.name+"/"+s;}return s;}
        static void ApplySurfaces()
        {
            foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if(!r.sharedMaterial || r.GetComponent<TMP_Text>())continue;
                string n=r.name.ToLowerInvariant(), a=Ancestor(r.transform);string key="Steel";float tile=2;
                bool station=a.StartsWith("05 ")||a.StartsWith("06 ")||a.StartsWith("07 ");
                string old=r.sharedMaterial.name;
                if(old=="Ground") { key=station?"StationFloor":n.Contains("electrical")?"Concrete":"Asphalt";tile=station?1.8f:3; }
                else if(old=="Concrete") { key=station?(n.Contains("floor")||n.Contains("step")||n.Contains("landing")?"StationFloor":"StationWall"):"Concrete";tile=key=="StationFloor"?1.8f:2.4f; }
                else if(old=="Aircraft skin") {key="Aircraft";tile=4;}
                else if(old=="Rubble")key="Concrete";
                else if(old=="Dark recess")key="Rubber";
                else if(old=="White markings")key="Markings";
                else if(old=="Sheet metal")key="SheetMetal";
                if(n.Contains("sheet barricade"))key="SheetMetal";
                if(n.Contains("container"))key=n.Contains("ribs")?"Rust":"Container";
                if(n.Contains("cabinet")||n.Contains("generator body"))key="Helicopter";
                if(n.Contains("door leaf"))key="Teal";
                if(n.Contains("ticket checking pedestal")||n.Contains("ticket counter"))key="Steel";
                if(n.Contains("glazing")||n.Contains("windshield")||n.Contains("water bottle"))key="Glass";
                if(n.Contains("water dispenser"))key="Cream";
                if(n.Contains("forearm"))key="Flesh";
                if(n.Contains("finger")||n.Contains("palm"))key="Flesh";
                if(n.Contains("crushed engine"))key="Aircraft";
                if(n.Contains("chair seat")||n.Contains("chair back"))key="Teal";
                if(n.Contains("step edge"))key="Amber";
                r.sharedMaterial=M[key];
                if(M[key].mainTexture) UV(r,tile);
            }
        }
        static void UV(MeshRenderer r,float metres)
        {
            var mf=r.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)return;
            var src=mf.sharedMesh;Vector3 s=r.transform.lossyScale;
            string cache=src.name+"/"+s.ToString("F3")+"/"+metres;
            if(!UVCache.TryGetValue(cache,out var mesh))
            {
                mesh=Object.Instantiate(src);mesh.name=src.name+" metre UV";
                var v=src.vertices;var normals=src.normals;var uv=new Vector2[v.Length];
                for(int i=0;i<v.Length;i++)
                {
                    Vector3 p=Vector3.Scale(v[i],s),n=i<normals.Length?normals[i]:Vector3.up;
                    if(Mathf.Abs(n.y)>.65f)uv[i]=new Vector2(p.x,p.z)/metres;
                    else if(Mathf.Abs(n.x)>Mathf.Abs(n.z))uv[i]=new Vector2(p.z,p.y)/metres;
                    else uv[i]=new Vector2(p.x,p.y)/metres;
                }
                mesh.uv=uv;AssetDatabase.CreateAsset(mesh,Root+"/Geometry/Art/UV-"+(++uvIndex).ToString("D3")+".asset");UVCache[cache]=mesh;
            }
            mf.sharedMesh=mesh;
        }
        static Transform Group(string n,Transform parent=null){var t=new GameObject(n).transform;t.SetParent(parent?parent:art,false);return t;}
        static GameObject Box(string n,Transform parent,Vector3 p,Vector3 scale,string key, bool solid=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(parent?parent:art,false);g.transform.localPosition=p;g.transform.localScale=scale;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=M[key];g.isStatic=true;
            if(M[key].mainTexture)UV(g.GetComponent<MeshRenderer>(),2);return g;
        }
        static GameObject Rod(string n,Transform parent,Vector3 a,Vector3 b,float radius,string key)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(parent?parent:art,false);g.transform.localPosition=(a+b)/2;
            g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);g.transform.localScale=new Vector3(radius*2,(b-a).magnitude/2,radius*2);
            Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=M[key];g.isStatic=true;return g;
        }
        static GameObject Model(string asset,Transform parent,Vector3 p)
        {
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+asset+".fbx"));
            g.name="Visual model - "+asset;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localRotation=Quaternion.identity;
            foreach(var r in g.GetComponentsInChildren<Renderer>())
            {r.sharedMaterials=r.sharedMaterials.Select(m=>M.TryGetValue(m.name,out var replacement)?replacement:M["Steel"]).ToArray();r.gameObject.isStatic=true;}
            return g;
        }
        static void ReplaceProps()
        {
            var h=GameObject.Find("Stranded helicopter").transform;
            foreach(var r in h.GetComponentsInChildren<Renderer>())r.enabled=false;
            Model("RescueHelicopter",h,Vector3.zero);
            foreach(string n in new[]{"Abandoned sedan","Abandoned van","Burned wreck","Overturned wreck"})
            {
                var t=GameObject.Find(n).transform;foreach(var r in t.GetComponentsInChildren<Renderer>())r.enabled=false;
                var g=Model(n.Contains("van")?"ServiceVan":"AbandonedSedan",t,Vector3.zero);
                if(n.Contains("wreck"))foreach(var r in g.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>m==M["Glass"]||m==M["Rubber"]?m:M["Scorch"]).ToArray();
                if(n.Contains("Overturned")){g.transform.localPosition=new Vector3(0,1.5f,0);g.transform.localRotation=Quaternion.Euler(0,0,162);}
            }
            var seatRoot=GameObject.Find("06 Three waiting rooms").transform;
            foreach(var r in seatRoot.GetComponentsInChildren<MeshRenderer>().ToArray())
            {
                if(r.name=="Waiting chair seat"){Model("MetroSeat",seatRoot,r.transform.localPosition-Vector3.up*.48f);r.enabled=false;}
                if(r.name=="Waiting chair back"||r.name=="Chair support")r.enabled=false;
            }
            int index=0;
            foreach(var r in Object.FindObjectsByType<MeshRenderer>().ToArray())
            {
                string n=r.name.ToLowerInvariant();
                if(!(n.Contains("rubble")||n=="crash debris"||n=="snapped roof edge"||n=="collapsed stairwell solid core"))continue;
                r.enabled=false;Model("ConcreteDebris"+(index++%3),r.transform,Vector3.zero);
                if(index%3==0)
                {var t=r.transform;Rod("Exposed reinforcing bar",t,new Vector3(-.35f,.5f,0),new Vector3(.65f,.8f,.12f),.013f,"Rust");}
            }
        }
        static void AircraftDetail()
        {
            var plane=GameObject.Find("04 Crashed aircraft - belly on ground - no landing gear").transform;
            var detail=Group("Aircraft skin fittings and impact detail",plane);
            // Window strips follow the original fuselage rings; no colliders span the tail cavity.
            foreach(float s in new[]{-1f,1f})
            {
                for(float z=14;z<49;z+=2.1f)
                {
                    float w=z<=37?3.6f:Mathf.Lerp(3.6f,3.3f,(z-37)/12);
                    Box("Passenger window gasket",detail,new Vector3(s*(w*.958f+.025f),4.27f,z),new Vector3(.075f,.77f,.59f),"Rubber");
                    Box("Dark passenger glass",detail,new Vector3(s*(w*.958f+.07f),4.27f,z),new Vector3(.025f,.61f,.43f),"Glass");
                }
                Box("Airline stripe",detail,new Vector3(s*3.59f,3.45f,25),new Vector3(.04f,.28f,23),"Teal");
                foreach(float lateral in new[]{10f,16f})
                {
                    float z=lateral==10?30:35;
                    Rod("Engine central cone",detail,new Vector3(s*lateral,1.3f,z+2.5f),new Vector3(s*lateral,1.3f,z+2.8f),.22f,"Steel");
                    for(int j=0;j<12;j++)
                    {
                        float a=j*Mathf.PI*2/12;
                        var blade=Box("Turbine blade",detail,new Vector3(s*lateral+Mathf.Cos(a)*.62f,1.3f+Mathf.Sin(a)*.62f,z+2.47f),new Vector3(.65f,.11f,.06f),"Steel");blade.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg+22);
                    }
                    Rod("Engine exhaust ring",detail,new Vector3(s*lateral,1.3f,z-2.4f),new Vector3(s*lateral,1.3f,z-2.49f),.85f,"Steel");
                    Rod("Engine exhaust hollow",detail,new Vector3(s*lateral,1.3f,z-2.5f),new Vector3(s*lateral,1.3f,z-2.51f),.69f,"Rubber");
                }
                for(int i=0;i<7;i++)
                {
                    var panel=Box("Wing skin seam",detail,new Vector3(s*(5+i*1.9f),2.72f,32+i*.68f),new Vector3(.05f,.015f,5),"Steel");
                    panel.transform.localRotation=Quaternion.Euler(0,s*18,0);
                }
            }
            // Torn station impact framing remains above walking headroom.
            for(int i=0;i<8;i++)Rod("Torn reinforcing rod",art,new Vector3(49.7f,3.5f+i*.22f,14.6f),new Vector3(51+i*.24f,3.1f+i*.25f,14.9f+i*.18f),.045f,"Rust");
        }
        static TextMeshPro Sign(string text,Transform parent,Vector3 p,Quaternion q,float width,float height=.6f,float font=3)
        {
            var t=Group(text,parent);t.localPosition=p;t.localRotation=q;
            Box("Enamel sign",t,Vector3.zero,new Vector3(width,height,.06f),"Teal");
            var tm=new GameObject("Lettering").AddComponent<TextMeshPro>();tm.transform.SetParent(t,false);tm.transform.localPosition=new Vector3(0,0,-.04f);
            tm.rectTransform.sizeDelta=new Vector2(width-.12f,height-.04f);tm.font=TMP_Settings.defaultFontAsset;tm.fontSize=font;tm.text=text;tm.alignment=TextAlignmentOptions.Center;tm.color=new Color(.9f,.93f,.87f);tm.textWrappingMode=TextWrappingModes.NoWrap;
            return tm;
        }
        static void Architecture()
        {
            // Wall trims follow each existing segment, including the existing openings.
            foreach(var r in Object.FindObjectsByType<MeshRenderer>().ToArray())
            {
                string a=Ancestor(r.transform);var s=r.transform.localScale;
                if((a.StartsWith("05 ")||a.StartsWith("06 ")||a.StartsWith("07 ")) && r.sharedMaterial==M["StationWall"] && s.x<.8f && s.y>=3 && s.z>=2)
                {
                    Box("Metro teal band",r.transform,new Vector3(0,-.2f,0),new Vector3(1.07f,.075f,1.002f),"Teal");
                    Box("Dark skirting",r.transform,new Vector3(0,-.47f,0),new Vector3(1.1f,.05f,1.002f),"Steel");
                }
            }
            // Industrial garage doors and container hardware are local additions, not new obstructions.
            foreach(var r in Object.FindObjectsByType<MeshRenderer>().Where(x=>x.name=="Cargo container").ToArray())
            {
                var t=Group("Container locking bars");t.position=r.transform.position;
                foreach(float x in new[]{-1.6f,1.6f})Rod("Locking bar",t,new Vector3(x,-1.35f,-4.55f),new Vector3(x,1.35f,-4.55f),.055f,"Steel");
                for(int k=0;k<2;k++)Box("Container hinge",t,new Vector3(-2.8f,-.8f+k*1.6f,-4.56f),new Vector3(.25f,.16f,.1f),"Steel");
                Sign("TRANSIT / CARGO",t,new Vector3(0,.6f,-4.57f),Quaternion.identity,3,.45f,1.5f);
            }
            foreach(var d in Object.FindObjectsByType<ReviewDoor>())
            {
                var leaf=d.leaf.Find("Door leaf - E to open for inspection");float w=leaf.localScale.x;
                Box("Door kick plate",d.leaf,new Vector3(w/2,.32f,-.079f),new Vector3(w-.15f,.45f,.025f),"Steel");
                Box("Door small wired window",d.leaf,new Vector3(w/2,2.1f,-.08f),new Vector3(.42f,.52f,.025f),"Rubber");
                Box("Door glazing",d.leaf,new Vector3(w/2,2.1f,-.095f),new Vector3(.34f,.44f,.018f),"Glass");
                for(int i=0;i<3;i++)Rod("Door hinge barrel",d.leaf,new Vector3(.04f,.4f+i,0),new Vector3(.04f,.58f+i,0),.045f,"Steel");
                foreach(var t in d.GetComponentsInChildren<Transform>())t.gameObject.isStatic=false;
            }
            for(int i=0;i<7;i++)
            {
                float x=8.5f+i*4;Box("Reader status lamp",art,new Vector3(x,1.251f,-22.55f),new Vector3(.18f,.016f,.12f),"Emergency");
                // Retracted gate leaves keep the approved passages clear.
                Box("Retracted turnstile panel",art,new Vector3(x+.35f,.66f,-23),new Vector3(.04f,.7f,1.1f),"Glass");
            }
            for(int i=0;i<3;i++)
            {
                float z=-7-i*5;
                Box("Ticket window upper frame",art,new Vector3(5.02f,2.9f,z),new Vector3(.08f,.08f,4.7f),"Steel");
                Box("Ticket window sill",art,new Vector3(5.03f,1.35f,z),new Vector3(.35f,.1f,4.7f),"Steel");
                Box("Ticket office screen",art,new Vector3(3.5f,1.7f,z),new Vector3(.15f,.55f,.8f),"Glass");
            }
            foreach(var r in Object.FindObjectsByType<MeshRenderer>().Where(x=>x.name=="Water dispenser").ToArray())
            {
                Vector3 p=r.transform.position;
                Box("Dispenser recess",art,p+new Vector3(0,.12f,-.334f),new Vector3(.5f,.35f,.025f),"Rubber");
                Box("Drip tray",art,p+new Vector3(0,-.08f,-.39f),new Vector3(.54f,.045f,.2f),"Steel");
                Box("Cold tap",art,p+new Vector3(-.13f,.18f,-.39f),new Vector3(.06f,.08f,.12f),"PaintBlue");
                Box("Hot tap",art,p+new Vector3(.13f,.18f,-.39f),new Vector3(.06f,.08f,.12f),"PaintRed");
            }
            Sign("CENTRAL TRANSIT  /  M2",art,new Vector3(31.8f,3.6f,-1.7f),Quaternion.Euler(0,180,0),12,.8f,3);
            Sign("EASTBOUND   /   PLATFORM 02  >",art,new Vector3(45,3.5f,-38.8f),Quaternion.Euler(0,180,0),15,.85f,3.2f);
            Sign("02  /  EASTBOUND",art,new Vector3(86,1.2f,14.9f),Quaternion.identity,8,.7f,2.5f);
            // Night city silhouettes sit beyond the sheet enclosure and cannot become routes.
            var city=Group("Distant city silhouettes");var rand=new System.Random(48);
            for(int i=0;i<19;i++)
            {
                float x=-130+i*14, z=i%2==0?83:104, height=12+rand.Next(22);
                Box("Abandoned city block",city,new Vector3(x,height/2,z),new Vector3(10+rand.Next(8),height,12),"Concrete");
                Box("Rooftop service housing",city,new Vector3(x,height+.9f,z),new Vector3(4,1.8f,4),"Steel");
                for(int j=0;j<3;j++)Box("Distant isolated window",city,new Vector3(x-3+j*3,4+rand.Next((int)height-5),z-6.02f),new Vector3(.7f,1.1f,.04f),j%2==0?"LampWarm":"Rubber");
            }
        }
        static void ElectricalDetail()
        {
            for(int i=0;i<5;i++)
            {
                float z=31+i*3;
                Box("Switchboard inset",art,new Vector3(-32.58f,1.5f,z),new Vector3(.04f,1.4f,1.5f),"Steel");
                for(int j=0;j<3;j++)Box("Breaker handle",art,new Vector3(-32.53f,1.6f,z-.48f+j*.48f),new Vector3(.1f,.28f,.1f),"Rubber");
                Box("Warning plate",art,new Vector3(-32.52f,2.1f,z),new Vector3(.015f,.17f,.32f),"Amber");
                Rod("Armoured conduit",art,new Vector3(-33.5f,2.4f,z),new Vector3(-33.5f,4.5f,z),.055f,"Steel");
            }
            for(int j=0;j<13;j++)Box("Generator cooling louvre",art,new Vector3(-26.2f+j*.34f,.85f,39.27f),new Vector3(.18f,.75f,.03f),"Rubber");
            Rod("Exhaust riser",art,new Vector3(-26.5f,2.4f,42),new Vector3(-26.5f,4.4f,42),.16f,"Rust");
            Rod("Exhaust elbow",art,new Vector3(-26.5f,4.4f,42),new Vector3(-27.3f,4.4f,42),.16f,"Rust");
            Box("Generator instrument display",art,new Vector3(-24.6f,1.8f,39.15f),new Vector3(.55f,.3f,.025f),"Emergency");
            for(int i=0;i<3;i++)Rod("Generator gauge bezel",art,new Vector3(-23.8f+i*.32f,2.05f,39.14f),new Vector3(-23.8f+i*.32f,2.05f,39.1f),.1f,"Cream");
            for(int i=0;i<6;i++)Rod("Torn electrical rebar",art,new Vector3(-17,4.8f,28+i*3),new Vector3(-13.5f+i*.25f,4.3f,28+i*3),.04f,"Rust");
        }
        static Light Point(string n,Vector3 p,Color c,float intensity,float range,Transform parent=null)
        {
            var t=Group(n,parent);t.position=p;var l=t.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=c;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;l.renderMode=LightRenderMode.Auto;return l;
        }
        static void Fixture(Vector3 p,bool warm=false)
        {
            Box("Ceiling fixture housing",art,p,new Vector3(1.8f,.13f,.35f),"Steel");
            Box("Ceiling lamp diffuser",art,p-Vector3.up*.075f,new Vector3(1.55f,.025f,.24f),warm?"LampWarm":"LampCool");
            Point("Practical ceiling light",p-Vector3.up*.18f,warm?new Color(1,.7f,.38f):new Color(.62f,.8f,1),3.5f,13);
        }
        static void YardLamp(Vector3 p,bool warm=false)
        {
            Rod("Floodlight pole",art,p,p+Vector3.up*6,.09f,"Steel");
            Box("Floodlight housing",art,p+new Vector3(0,6,.25f),new Vector3(.8f,.24f,.65f),"Steel");
            Box("Floodlight panel",art,p+new Vector3(0,5.86f,.25f),new Vector3(.63f,.025f,.5f),warm?"LampWarm":"LampCool");
            Point("Yard floodlight",p+new Vector3(0,5.6f,.25f),warm?new Color(1,.66f,.3f):new Color(.6f,.78f,1),5,23);
        }
        static void Night()
        {
            var daylight=GameObject.Find("One daylight - blockout visibility");if(daylight)Object.DestroyImmediate(daylight);
            var sky=new Material(Shader.Find("Vaccine/NightSky"));sky.name="Open moonlit sky";AssetDatabase.CreateAsset(sky,Root+"/Materials/Art/NightSky.mat");RenderSettings.skybox=sky;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.12f,.17f,.25f);RenderSettings.ambientEquatorColor=new Color(.07f,.095f,.13f);RenderSettings.ambientGroundColor=new Color(.045f,.055f,.07f);RenderSettings.ambientIntensity=1;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0038f;RenderSettings.fogColor=new Color(.055f,.075f,.105f);
            RenderSettings.reflectionIntensity=.25f;
            var moon=Group("Moonlight - single shadow light");moon.rotation=Quaternion.Euler(38,-38,0);var light=moon.gameObject.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(.55f,.69f,.9f);light.intensity=.72f;light.shadows=LightShadows.Soft;light.shadowStrength=.75f;light.shadowBias=.06f;
            foreach(var p in new[]{new Vector3(-60,0,-32),new Vector3(-22,0,-37),new Vector3(-15,0,-2),new Vector3(-34,0,3),new Vector3(32,0,46)})YardLamp(p);
            YardLamp(new Vector3(45,0,-20),true);Point("Tail crash emergency spill",new Vector3(-8,4.5f,29),new Color(1,.49f,.18f),4,15);
            foreach(var p in new[]{new Vector3(12,4.8f,-8),new Vector3(25,4.8f,-18),new Vector3(17,4.8f,-33),new Vector3(37,4.8f,-33),new Vector3(58,4.8f,-31),new Vector3(59,4.8f,-13),new Vector3(59,4.8f,3),new Vector3(77,4.8f,-32),new Vector3(77,4.8f,-16),new Vector3(77,4.8f,-1)})Fixture(p);
            Fixture(new Vector3(-27,4.75f,34),true);Fixture(new Vector3(-22,4.75f,43),true);
            Point("Stairwell safety light",new Vector3(78,2,10.7f),new Color(.55f,.82f,.72f),3,15);
            Box("Landing emergency strip",art,new Vector3(95.8f,-2.3f,10.6f),new Vector3(.08f,.2f,3),"Emergency");
            var pcontrol=Object.FindAnyObjectByType<VaccineWalkthrough>();
            var torch=new GameObject("Inspection torch").AddComponent<Light>();torch.transform.SetParent(pcontrol.eye.transform,false);torch.transform.localPosition=new Vector3(.15f,-.18f,.1f);torch.type=LightType.Spot;torch.color=new Color(.9f,.94f,1);torch.intensity=3;torch.range=19;torch.spotAngle=68;torch.innerSpotAngle=34;torch.shadows=LightShadows.None;
            // A map-only fill preserves the approved overhead review in the night scene.
            var map=Point("Overhead review fill",new Vector3(10,55,0),Color.white,35,150,pcontrol.mapLabels.transform);map.gameObject.transform.localPosition=new Vector3(10,55,0);
        }
    }
}
