using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vaccine.Editor
{
    public static class VaccineSubwayArtRevision
    {
        const string Dir="Assets/Vaccine/Geometry/SubwayArt", Mats="Assets/Vaccine/Materials/SubwayArt";
        const float Y=-12;
        static Transform revision, subway, ceilings, stairs;
        static VaccineWalkthrough player;
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static int meshId;
        static GameObject Find(string n)=>GameObject.Find(n)??Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t=>t.name==n)?.gameObject??throw new InvalidOperationException("Missing "+n);
        static Transform Group(string n,Transform parent=null){var t=new GameObject(n).transform;t.SetParent(parent?parent:revision,false);return t;}
        static void UV(MeshFilter f,float metres)
        {
            Vector3 s=f.transform.lossyScale;
            string key=s.ToString("F3")+"/"+metres;
            if(!meshes.TryGetValue(key,out var m))
            {
                m=Object.Instantiate(f.sharedMesh);var v=m.vertices;var n=m.normals;var uv=new Vector2[v.Length];
                for(int i=0;i<v.Length;i++){var p=Vector3.Scale(v[i],s);uv[i]=(Mathf.Abs(n[i].y)>.65f?new Vector2(p.x,p.z):Mathf.Abs(n[i].x)>.65f?new Vector2(p.z,p.y):new Vector2(p.x,p.y))/metres;}
                m.uv=uv;m.name="Subway scaled surface";AssetDatabase.CreateAsset(m,Dir+"/Surface-"+(++meshId).ToString("D3")+".asset");meshes[key]=m;
            }
            f.sharedMesh=m;
        }
        static GameObject Box(string n,Vector3 p,Vector3 s,string material,Transform parent=null,bool solid=true,bool moving=false,float tile=2)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(parent?parent:revision,false);g.transform.position=p;g.transform.localScale=s;g.GetComponent<MeshRenderer>().sharedMaterial=materials[material];g.isStatic=!moving;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());if(materials[material].mainTexture)UV(g.GetComponent<MeshFilter>(),tile);return g;
        }
        static GameObject LocalBox(string n,Transform parent,Vector3 p,Vector3 s,string mat,bool solid=true)
        {var b=Box(n,parent.TransformPoint(p),s,mat,parent,solid,true);b.transform.localRotation=Quaternion.identity;return b;}
        static void Surface(GameObject g,string mat,float tile=2)
        {g.GetComponent<MeshRenderer>().sharedMaterial=materials[mat];if(materials[mat].mainTexture)UV(g.GetComponent<MeshFilter>(),tile);}
        static void Floor(string n,float x0,float x1,float z0,float z1,float y,string mat,Transform parent=null)
        {Box(n,new Vector3((x0+x1)/2,y-.2f,(z0+z1)/2),new Vector3(x1-x0,.4f,z1-z0),mat,parent);}
        static Material Material(string name,string texture,Color tint,float smooth=.2f)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name=name,enableInstancing=true};m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",smooth);m.SetColor("_SpecColor",new Color(.16f,.17f,.18f));
            if(texture!=null)m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));AssetDatabase.CreateAsset(m,Mats+"/"+name+".mat");materials[name]=m;return m;
        }
        static void Text(string value,Transform parent,Vector3 local,float width,float height,float size=3)
        {
            var t=new GameObject(value).AddComponent<TextMeshPro>();t.transform.SetParent(parent,false);t.transform.localPosition=local;t.rectTransform.sizeDelta=new Vector2(width,height);t.font=TMP_Settings.defaultFontAsset;t.text=value;t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;t.textWrappingMode=TextWrappingModes.NoWrap;
        }
        static void Sign(string value,Vector3 p,float width,float yaw=0,Transform parent=null)
        {
            var g=Group(value,parent);g.position=p;g.rotation=Quaternion.Euler(0,yaw,0);
            LocalBox("Enamel sign backing",g,Vector3.zero,new Vector3(width,.7f,.12f),"Teal",false);
            Text(value,g,new Vector3(0,0,-.071f),width-.1f,.65f,2.8f);
        }
        [MenuItem("Vaccine/Deepen and Texture Subway")]
        public static string Apply()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=VaccineBlockoutBuilder.ScenePath)throw new InvalidOperationException("Open Vaccine outside Play.");
            if(GameObject.Find("15 Deep subway and connected trains"))throw new InvalidOperationException("Revision already applied.");
            VaccineArtBatcher.Restore();Directory.CreateDirectory(Dir);Directory.CreateDirectory(Mats);AssetDatabase.Refresh();
            foreach(var file in Directory.GetFiles("Assets/Vaccine/Textures/Subway","*.png"))
            {var i=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));i.textureType=TextureImporterType.Default;i.sRGBTexture=true;i.wrapMode=TextureWrapMode.Repeat;i.mipmapEnabled=true;i.filterMode=FilterMode.Trilinear;i.anisoLevel=2;i.maxTextureSize=1024;i.textureCompression=TextureImporterCompression.Compressed;i.SaveAndReimport();}
            materials.Clear();meshes.Clear();meshId=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Vaccine/Materials/Art"})){var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));materials[m.name]=m;}
            Material("Train enamel","Assets/Vaccine/Textures/Subway/TrainEnamel.png",new Color(.85f,.87f,.85f),.35f);
            Material("Train rubber floor","Assets/Vaccine/Textures/Subway/CarriageRubber.png",new Color(.72f,.77f,.8f),.1f);
            Material("Train blue stripe","Assets/Vaccine/Textures/Subway/TrainEnamel.png",new Color(.12f,.29f,.34f),.3f);
            Material("Train seat vinyl",null,new Color(.12f,.23f,.27f),.35f);
            Material("Track ballast","Assets/Vaccine/Textures/Concrete.png",new Color(.27f,.25f,.23f),.05f);
            Material("Tactile yellow","Assets/Vaccine/Textures/Subway/CarriageRubber.png",new Color(1,.74f,.22f),.15f);
            var glass=Object.Instantiate(materials["Station panoramic glass"]);glass.name="Train glazing";glass.SetColor("_BaseColor",new Color(.12f,.22f,.25f,.28f));AssetDatabase.CreateAsset(glass,Mats+"/Train glazing.mat");materials[glass.name]=glass;
            revision=new GameObject("15 Deep subway and connected trains").transform;player=Object.FindAnyObjectByType<VaccineWalkthrough>();
            subway=Find("14 Underground subway blockout").transform;ceilings=Find("Subway ceiling and train roofs").transform;
            AssetDatabase.StartAssetEditing();
            try { SurfaceFixes(); LowerSubway(); StairFlights(); PlatformMaterials(); TrackClearance(); RebuildTrains(); DressStation(); Routes(); }
            finally {AssetDatabase.StopAssetEditing();}
            Physics.SyncTransforms();AssetDatabase.SaveAssets();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "Saved three flights to -12m, textured subway, 8.4m wide connected trains with 36 automatic doors, clear departure track and four surface fixes.";
        }
        static void SurfaceFixes()
        {
            // This old strip was not parented to the temporary landing wall.
            foreach(string name in new[]{"Landing emergency strip","02  /  EASTBOUND","Stairwell safety light"}){var g=GameObject.Find(name);if(g)Object.DestroyImmediate(g);}
            var wall=Find("Entrance wall south");wall.transform.position=new Vector3(33,2.5f,-21.7f);wall.transform.localScale=new Vector3(.35f,5,15.4f);Surface(wall,"StationWall",2.4f);
            var sign=Find("CENTRAL TRANSIT  /  M2");sign.transform.position=new Vector3(16.5f,3.6f,-1.2f);
        }
        static void LowerSubway()
        {
            subway.position+=Vector3.down*8;ceilings.position+=Vector3.down*8;
            // Roofs remain a separate group for overhead review.
            foreach(var t in ceilings.Cast<Transform>().Where(t=>t.name=="Carriage roof").ToArray())Object.DestroyImmediate(t.gameObject);
            foreach(string name in new[]{"Platform 1 boarding train","Platform 2 damaged train","Subway entrance lintel"})Object.DestroyImmediate(Find(name));
        }
        static void StairFlights()
        {
            stairs=Find("07 Only open descent - platform layout pending").transform;
            foreach(var child in stairs.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            stairs.position=Vector3.zero;
            Floor("Upper stair landing",60.8f,63.8f,6.3f,10.3f,0,"StationFloor",stairs);
            Floor("Stair landing one",76.8f,81.3f,6.3f,15,-4,"StationFloor",stairs);
            Floor("Stair landing two",60.8f,63.8f,6.3f,15,-8,"StationFloor",stairs);
            Floor("Lower platform access landing",76.8f,88.8f,6.3f,15,-12,"StationFloor",stairs);
            for(int flight=0;flight<3;flight++)for(int i=0;i<20;i++)
            {
                bool reverse=flight==1;float x=reverse?76.8f-(i+.5f)*.65f:63.8f+(i+.5f)*.65f;
                float top=-flight*4-(i+1)*.2f,z=reverse?13f:8.3f;
                Box("Flight "+(flight+1)+" step "+(i+1),new Vector3(x,top-.18f,z),new Vector3(.65f,.36f,4),"StationFloor",stairs);
                Box("Stair nosing",new Vector3(x+(reverse?.29f:-.29f),top+.013f,z),new Vector3(.055f,.02f,4),"Markings",stairs,false);
            }
            foreach(float z in new[]{6,15.3f})Box("Enclosed three-flight stair wall",new Vector3(74.8f,-3.85f,z),new Vector3(28,18.3f,.35f),"StationWall",stairs,true,false,2.4f);
            Box("Stair central divider",new Vector3(70.3f,-4.8f,10.65f),new Vector3(13,15.4f,.4f),"StationWall",stairs,true,false,2.4f);
            Box("Upper landing north guard wall",new Vector3(62.3f,1.4f,10.5f),new Vector3(3,2.8f,.35f),"StationWall",stairs);
            Box("Middle landing enclosed end",new Vector3(81.3f,.55f,10.65f),new Vector3(.4f,9.1f,9.3f),"StationWall",stairs);
            Box("Lower stair west closure",new Vector3(60.6f,-6.5f,10.65f),new Vector3(.4f,13,9.3f),"StationWall",stairs);
            Box("Upper stair west closure",new Vector3(60.6f,2.5f,12.8f),new Vector3(.4f,5,4.8f),"StationWall",stairs);
            Box("Stair to subway upper closure",new Vector3(88.8f,-.75f,10.65f),new Vector3(.5f,12.1f,9.3f),"StationWall",stairs);
            // The ceiling meets the lintel; there is no open sky above the platform entrance.
            var roof=Find("Enclosed subway descent roof");roof.transform.position=new Vector3(74.8f,5.18f,10.65f);roof.transform.localScale=new Vector3(28,.3f,9.7f);Surface(roof,"Concrete");
            Sign("PLATFORMS  /  DOWN",new Vector3(80.99f,-1.2f,10.65f),6,90,stairs);
            Sign("PLATFORMS  /  DOWN",new Vector3(60.85f,-5.2f,10.65f),6,-90,stairs);
            foreach(var p in new[]{new Vector3(70,-.2f,8.3f),new Vector3(72,-4.2f,13),new Vector3(71,-8.2f,8.3f),new Vector3(84,-8.6f,10)})Light(p,30,13,stairs);
        }
        static void PlatformMaterials()
        {
            foreach(var r in subway.GetComponentsInChildren<MeshRenderer>(true).Concat(ceilings.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(r.GetComponent<TMP_Text>()||!r.sharedMaterial)continue;string name=r.sharedMaterial.name,mat=null;float tile=2;
                if(name=="Platform grey") {mat="StationFloor";tile=1.8f;}
                if(name=="Underground concrete grey") {mat=r.name.Contains("roof")?"Concrete":"StationWall";tile=2.4f;}
                if(name=="Track and frames charcoal")mat=r.name.Contains("bed")?"Track ballast":"Steel";
                if(name=="Window recess charcoal")mat="Rubber";
                if(name=="Collapse grey")mat="Concrete";
                if(name=="Platform edge pale grey"){mat="Tactile yellow";tile=1;}
                if(name=="Ceiling light white")mat="LampCool";
                if(mat!=null)Surface(r.gameObject,mat,tile);
            }
        }
        static void ResizeBox(string n,Vector3 p,Vector3 size,string mat)
        {var g=Find(n);g.transform.position=p;g.transform.localScale=size;Surface(g,mat);}
        static void TrackClearance()
        {
            ResizeBox("North track bed",new Vector3(136.4f,Y-1.4f,23.25f),new Vector3(95.2f,.5f,12.5f),"Track ballast");
            ResizeBox("South track bed",new Vector3(136.4f,Y-1.4f,-36.5f),new Vector3(95.2f,.5f,13),"Track ballast");
            ResizeBox("Subway north retaining wall",new Vector3(136.4f,Y+2.3f,29.5f),new Vector3(95.2f,7,.5f),"StationWall");
            ResizeBox("Subway south retaining wall",new Vector3(136.4f,Y+2.3f,-43),new Vector3(95.2f,7,.5f),"StationWall");
            ResizeBox("Subway east retaining wall",new Vector3(184,Y+2.3f,-13),new Vector3(.6f,7,60),"StationWall");
            ResizeBox("West wall below stair mouth",new Vector3(88.8f,Y+2.3f,-18.5f),new Vector3(.5f,7,49),"StationWall");
            ResizeBox("West wall above stair mouth",new Vector3(88.8f,Y+2.3f,22.25f),new Vector3(.5f,7,14.5f),"StationWall");
            ResizeBox("Underground roof slab",new Vector3(136.4f,Y+5.05f,-6.75f),new Vector3(95.2f,.5f,72.5f),"Concrete");
            foreach(var b in ceilings.Cast<Transform>().Where(t=>t.name=="Transverse ceiling beam")){var s=b.localScale;s.z=72.5f;b.localScale=s;var p=b.position;p.z=-6.75f;b.position=p;Surface(b.gameObject,"Steel");}
            var collapse=Find("Far end collapse - half of train 2 buried");
            foreach(var r in collapse.GetComponentsInChildren<MeshRenderer>().Where(r=>r.bounds.max.z>2).ToArray())Object.DestroyImmediate(r.gameObject);
            var damageCore=Box("Wide damaged train collapse core",new Vector3(169,Y+2.5f,-29.5f),new Vector3(30,7,27),"Concrete");damageCore.GetComponent<MeshRenderer>().enabled=false;
            foreach(var t in collapse.GetComponentsInChildren<Transform>().Where(t=>t.name=="Roof crushing damaged train")){var p=t.position;p.z=-34.6f;t.position=p;var s=t.localScale;s.z=11;t.localScale=s;Surface(t.gameObject,"Concrete");}
            for(int i=0;i<5;i++){var b=Box("Wreckage behind widened train",new Vector3(156+i*5,Y+1.8f,-40),new Vector3(6,4.5f,5),"Concrete");b.transform.rotation=Quaternion.Euler(10,i*27,-14);}
            foreach(var track in new[]{Find("North track").transform,Find("South track").transform})
            {
                bool north=track.name=="North track";float z=north?21.6f:-34.6f;
                foreach(var t in track.Cast<Transform>().Where(t=>t.name=="Rail"||t.name=="Track sleeper"||t.name=="Closed tunnel end").ToArray())Object.DestroyImmediate(t.gameObject);
                for(int i=0;i<2;i++)Box("Train running rail",new Vector3(north?153.4f:136.4f,Y-.97f,z+(i==0?-2.5f:2.5f)),new Vector3(north?129.2f:95.2f,.2f,.14f),"Steel",track);
                for(float x=89.5f;x<(north?218:184);x+=2)Box("Rail sleeper",new Vector3(x,Y-1.05f,z),new Vector3(.3f,.17f,6.6f),"Rust",track);
                Box("Rear tunnel wall",new Vector3(89.05f,Y+2.1f,z),new Vector3(.4f,6.5f,11),"Concrete",track);
            }
            Floor("Clear departure tunnel track bed",184,218,17,29.5f,Y-1.15f,"Track ballast");
            foreach(float z in new[]{17,29.5f})Box("Departure tunnel wall",new Vector3(201,Y+1.9f,z),new Vector3(34,6.1f,.5f),"Concrete");
            Floor("Departure tunnel ceiling",184,218,17,29.5f,Y+5,"Concrete",ceilings);
            Box("Departure portal upper lintel",new Vector3(184,Y+4.8f,23.25f),new Vector3(.6f,.6f,12.5f),"Concrete");
            Box("Distant tunnel darkness",new Vector3(218,Y+1.9f,23.25f),new Vector3(.3f,6.1f,12.5f),"Rubber");
            Floor("North service ledge",96,158,26,28.8f,Y,"Concrete");Floor("South service ledge",122,151,-42,-39,Y,"Concrete");
            Floor("Train one rear access deck",96,100,17,28.8f,Y,"Steel");Floor("Train two rear access deck",122,126.4f,-42,-30,Y,"Steel");
            Light(new Vector3(191,Y+2,23),25,14);Light(new Vector3(210,Y+2,23),14,12);
        }
        static void RebuildTrains(){Train("Platform 1 wide connected train",100,21.6f,false);Train("Platform 2 wide damaged train",126.4f,-34.6f,true);}
        static void Train(string name,float start,float z,bool damaged)
        {
            var root=Group(name);
            for(int i=0;i<3;i++)
            {
                float x=start+i*18.3f;var car=Group("Carriage "+(i+1)+" - six doors",root);
                Floor("Carriage rubber floor",x,x+17,z-4.2f,z+4.2f,Y,"Train rubber floor",car);
                Box("Underframe",new Vector3(x+8.5f,Y-.5f,z),new Vector3(16.8f,.6f,7.5f),"Steel",car);
                foreach(float side in new[]{-1f,1f})
                {
                    float wallZ=z+side*4.2f;
                    foreach(var v in new[]{new Vector2(0,3.3f),new Vector2(5.7f,11.3f),new Vector2(13.7f,17)})
                    {
                        float cx=x+(v.x+v.y)/2,len=v.y-v.x;
                        Box("Lower enamel body",new Vector3(cx,Y+.63f,wallZ),new Vector3(len,1.26f,.2f),"Train enamel",car);
                        Box("Upper enamel body",new Vector3(cx,Y+3.05f,wallZ),new Vector3(len,.7f,.2f),"Train enamel",car);
                        Box("Glazed carriage window",new Vector3(cx,Y+1.98f,wallZ),new Vector3(len-.36f,1.44f,.06f),"Train glazing",car);
                        foreach(float px in new[]{cx-len/2+.09f,cx+len/2-.09f})Box("Window mullion",new Vector3(px,Y+1.98f,wallZ),new Vector3(.18f,1.44f,.21f),"Train enamel",car);
                        Box("Blue carriage waist stripe",new Vector3(cx,Y+.93f,wallZ+side*.108f),new Vector3(len,.32f,.025f),"Train blue stripe",car,false);
                    }
                    foreach(float dx in new[]{4.5f,12.5f})
                    {
                        Box("Side doorway header",new Vector3(x+dx,Y+3.05f,wallZ),new Vector3(2.4f,.7f,.25f),"Train enamel",car);
                        Door(new Vector3(x+dx,Y+1.35f,wallZ+side*.16f),0,car,"Side automatic door");
                        Floor("Door threshold",x+dx-1.3f,x+dx+1.3f,wallZ-.48f,wallZ+.48f,Y,"Steel",car);
                    }
                    if(!damaged||i==0)
                    foreach(float sx in new[]{x+1.7f,x+8.5f,x+15.3f})
                    {
                        Box("Vinyl bench cushion",new Vector3(sx,Y+.48f,z+side*3.55f),new Vector3(2.1f,.22f,.85f),"Train seat vinyl",car);
                        Box("Vinyl bench back",new Vector3(sx,Y+.93f,z+side*3.9f),new Vector3(2.1f,.8f,.18f),"Train seat vinyl",car);
                        Box("Seat pedestal",new Vector3(sx,Y+.22f,z+side*3.55f),new Vector3(1.6f,.44f,.6f),"Steel",car);
                    }
                }
                foreach(float ex in new[]{x+.1f,x+16.9f})
                {
                    foreach(float side in new[]{-1f,1f})Box("End bulkhead beside gangway",new Vector3(ex,Y+1.7f,z+side*2.7f),new Vector3(.2f,3.4f,3),"Train enamel",car);
                    Box("End doorway header",new Vector3(ex,Y+3.05f,z),new Vector3(.25f,.7f,2.4f),"Train enamel",car);
                    Door(new Vector3(ex,Y+1.35f,z),90,car,"End automatic door");
                }
                var roof=Box("Wide carriage roof",new Vector3(x+8.5f,Y+3.55f,z),new Vector3(17,.3f,8.55f),"Train enamel",ceilings);
                if(damaged&&i>0){roof.transform.position+=Vector3.down*(i==1?.35f:1.4f);roof.transform.rotation=Quaternion.Euler(i==1?7:13,0,i==1?-5:4);}
                foreach(float sx in new[]{x+3,x+14})
                {
                    Box("Bogie",new Vector3(sx,Y-.75f,z),new Vector3(2.8f,.5f,6.2f),"Steel",car);
                    foreach(float sz in new[]{z-2.65f,z+2.65f}){var w=GameObject.CreatePrimitive(PrimitiveType.Cylinder);w.name="Steel wheel";w.transform.SetParent(car,false);w.transform.position=new Vector3(sx,Y-.7f,sz);w.transform.rotation=Quaternion.Euler(90,0,0);w.transform.localScale=new Vector3(.9f,.18f,.9f);w.GetComponent<MeshRenderer>().sharedMaterial=materials["Steel"];w.isStatic=true;Object.DestroyImmediate(w.GetComponent<Collider>());}
                }
                if(!damaged||i<2)
                {
                    var downlight=Light(new Vector3(x+8.5f,Y+2.8f,z),35,12,car);
                    downlight.type=LightType.Spot;downlight.transform.rotation=Quaternion.Euler(90,0,0);downlight.spotAngle=145;downlight.innerSpotAngle=95;
                    Light(new Vector3(x+8.5f,Y+1.9f,z),5,10,car).name="Carriage soft fill";
                    foreach(float dx in new[]{4f,13f})Box("Carriage ceiling strip",new Vector3(x+dx,Y+3.36f,z),new Vector3(4,.06f,.35f),"LampWarm",ceilings,false);
                }
                if(i<2)
                {
                    Floor("Walkable inter-car gangway",x+16.9f,x+18.4f,z-1.4f,z+1.4f,Y,"Train rubber floor",root);
                    foreach(float side in new[]{-1f,1f})
                    {
                        Box("Enclosed gangway side",new Vector3(x+17.65f,Y+1.5f,z+side*1.4f),new Vector3(1.5f,3,.16f),"Rubber",root);
                        for(int k=0;k<7;k++)Box("Gangway bellows rib",new Vector3(x+17.05f+k*.2f,Y+1.5f,z+side*1.5f),new Vector3(.07f,3,.12f),"Steel",root);
                    }
                    Box("Gangway roof",new Vector3(x+17.65f,Y+3.15f,z),new Vector3(1.5f,.3f,3),"Rubber",ceilings);
                }
            }
            Floor("Train front inspection deck",start+53.6f,start+56,z-1.4f,z+1.4f,Y,"Steel",root);
        }
        static void Door(Vector3 p,float yaw,Transform car,string name)
        {
            var t=Group(name,car);t.position=p;t.rotation=Quaternion.Euler(0,yaw,0);
            var d=t.gameObject.AddComponent<SubwayTrainDoor>();d.actor=player.transform;d.sensor=new Vector3(2.5f,2.2f,3.5f);
            foreach(float side in new[]{-1f,1f})
            {
                var leaf=Group("Moving leaf",t);leaf.localPosition=new Vector3(side*.6f,0,0);
                LocalBox("Door lower panel",leaf,new Vector3(0,-.7f,0),new Vector3(1.18f,1.3f,.16f),"Train enamel");
                LocalBox("Door top panel",leaf,new Vector3(0,1.12f,0),new Vector3(1.18f,.46f,.16f),"Train enamel");
                LocalBox("Door safety glass",leaf,new Vector3(0,.42f,0),new Vector3(.92f,.94f,.05f),"Train glazing");
                foreach(float edge in new[]{-.525f,.525f})LocalBox("Door window frame",leaf,new Vector3(edge,.42f,0),new Vector3(.13f,.94f,.16f),"Train enamel");
                if(side<0)d.leftPanel=leaf;else d.rightPanel=leaf;
            }
        }
        static Light Light(Vector3 p,float intensity,float range,Transform parent=null)
        {var l=new GameObject("Subway practical light").AddComponent<Light>();l.transform.SetParent(parent?parent:revision,false);l.transform.position=p;l.type=LightType.Point;l.intensity=intensity;l.range=range;l.color=new Color(.85f,.91f,1);l.shadows=LightShadows.None;return l;}
        static void DressStation()
        {
            foreach(float z in new[]{29.18f,-42.68f})
            {
                Box("Fixed metro wall stripe",new Vector3(136,Y+1.55f,z),new Vector3(94,.55f,.035f),"Teal");
                Box("Wall skirting",new Vector3(136,Y+.16f,z),new Vector3(94,.32f,.06f),"Steel");
                for(int x=94;x<180;x+=12)Box("Wall support pier",new Vector3(x,Y+2.4f,z),new Vector3(.5f,4.8f,.5f),"Concrete");
            }
            foreach(float z in new[]{1.1f,-15.1f})for(int x=116;x<153;x+=12)
            {
                Box("Platform bench seat",new Vector3(x,Y+.5f,z),new Vector3(3,.2f,.75f),"Train seat vinyl");
                foreach(float dx in new[]{-1.1f,1.1f})Box("Bench leg",new Vector3(x+dx,Y+.2f,z),new Vector3(.12f,.4f,.6f),"Steel");
            }
            foreach(float z in new[]{3.2f,-17.2f})Box("Overhead service trunk",new Vector3(133,Y+4.6f,z),new Vector3(84,.22f,.3f),"Rust",ceilings);
            Sign("01  /  EVACUATION TRAIN",new Vector3(119,Y+3.3f,16.1f),14);
            Sign("02  /  SERVICE SUSPENDED",new Vector3(130,Y+3.3f,-16.5f),15);
            Sign("EXIT TUNNEL",new Vector3(183.6f,Y+3.8f,23.2f),7,90);
        }
        static void Routes()
        {
            var route=Find("Review route checkpoints").transform;
            route.GetChild(20).position=new Vector3(78.5f,-4,8);route.GetChild(21).position=new Vector3(78.5f,-4,13);
            foreach(var p in new[]{new Vector3(62.3f,-8,13),new Vector3(62.3f,-8,8),new Vector3(78.5f,-12,8),new Vector3(92,-12,8)})
            {var t=Group("Route "+route.childCount.ToString("D2"),route);t.position=p;}
            var labels=Find("Subway overhead labels");foreach(var t in labels.GetComponentsInChildren<TextMeshPro>(true))
            {
                if(t.text=="BOARDING TRAIN")t.transform.position=new Vector3(125,8,22);
                if(t.text=="DAMAGED TRAIN")t.transform.position=new Vector3(151,8,-35);
                if(t.text=="STAIRS UP")t.transform.position=new Vector3(73,8,11);
            }
        }
    }
}
