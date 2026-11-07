using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Vaccine.Editor
{
    public static partial class LabDistrictBuilder
    {
        public const string ScenePath="Assets/Vaccine/Scenes/Vaccine_LabDistrict.unity";
        const string MeshDir="Assets/Vaccine/Geometry/LabDistrict";
        static Transform district, roofs, labels, parent;
        static VaccineWalkthrough player;
        static Dictionary<string,Material> mats;
        static readonly Dictionary<string,Mesh> uvMeshes=new Dictionary<string,Mesh>();
        static int meshId;
        static readonly System.Random rng=new System.Random(4417);
        static Transform Group(string n,Transform p=null){var t=new GameObject(n).transform;if(p)t.SetParent(p,false);return t;}
        static GameObject Box(string n,Vector3 p,Vector3 s,string mat="Concrete",bool solid=true,Transform into=null,bool dynamic=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(into?into:parent,false);g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=mats[mat];g.isStatic=!dynamic;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());
            if(mats[mat].mainTexture)
            {
                float tile=mat=="StationWall"?2.4f:mat=="StationFloor"?1.8f:3;string key=s.ToString("F3")+"/"+tile;
                if(!uvMeshes.TryGetValue(key,out var m)){m=Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);var v=m.vertices;var nrm=m.normals;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++){var v3=Vector3.Scale(v[i],s);uv[i]=(Mathf.Abs(nrm[i].y)>.6f?new Vector2(v3.x,v3.z):Mathf.Abs(nrm[i].x)>.6f?new Vector2(v3.z,v3.y):new Vector2(v3.x,v3.y))/tile;}m.uv=uv;AssetDatabase.CreateAsset(m,MeshDir+"/Surface-"+(++meshId).ToString("D3")+".asset");uvMeshes[key]=m;}g.GetComponent<MeshFilter>().sharedMesh=m;
            }
            return g;
        }
        static void Floor(string n,float x0,float x1,float z0,float z1,float y=0,string m="StationFloor",Transform p=null)=>Box(n,new Vector3((x0+x1)/2,y-.2f,(z0+z1)/2),new Vector3(x1-x0,.4f,z1-z0),m,true,p);
        static void Wall(string n,float x0,float z0,float x1,float z1,float h=5,float y=0,string m="StationWall",Transform p=null)
        {var a=new Vector3(x0,y,z0);var b=new Vector3(x1,y,z1);var g=Box(n,(a+b)/2+Vector3.up*h/2,new Vector3(.35f,h,Vector3.Distance(a,b)),m,true,p);g.transform.rotation=Quaternion.LookRotation(b-a);}
        static void Sign(string text,Vector3 p,float width,float yaw=0)
        {var t=Group(text,parent);t.position=p;t.rotation=Quaternion.Euler(0,yaw,0);var b=Box("Sign backing",p,new Vector3(width,.9f,.1f),"Teal",false,t);b.transform.localRotation=Quaternion.identity;var tm=new GameObject("Lettering").AddComponent<TextMeshPro>();tm.transform.SetParent(t,false);tm.transform.localPosition=new Vector3(0,0,-.065f);tm.rectTransform.sizeDelta=new Vector2(width-.2f,.8f);tm.font=TMP_Settings.defaultFontAsset;tm.text=text;tm.fontSize=3.6f;tm.alignment=TextAlignmentOptions.Center;tm.color=Color.white;tm.textWrappingMode=TextWrappingModes.NoWrap;}
        static void Plan(string text,float x,float z,float width=24)
        {var t=new GameObject(text).AddComponent<TextMeshPro>();t.transform.SetParent(labels,false);t.transform.position=new Vector3(x,22,z);t.transform.rotation=Quaternion.Euler(90,0,0);t.font=TMP_Settings.defaultFontAsset;t.fontSize=14;t.text=text;t.rectTransform.sizeDelta=new Vector2(width,8);t.alignment=TextAlignmentOptions.Center;t.color=Color.white;}
        static void Light(Vector3 p,float intensity=24,float range=16,bool warm=false)
        {var l=new GameObject("Local practical light").AddComponent<Light>();l.transform.SetParent(parent,false);l.transform.position=p;l.type=LightType.Point;l.intensity=intensity;l.range=range;l.color=warm?new Color(1,.73f,.42f):new Color(.64f,.83f,1);l.shadows=LightShadows.None;}
        static void Door(string name,float x,float z,float width=3,float yaw=0)
        {var root=Group(name,parent);root.position=new Vector3(x-width/2,0,z);root.rotation=Quaternion.Euler(0,yaw,0);var d=root.gameObject.AddComponent<ReviewDoor>();d.roomName=name;d.leaf=Group("Hinged door",root);var b=Box("Door panel",root.TransformPoint(new Vector3(width/2,1.35f,0)),new Vector3(width,2.7f,.15f),"Steel",true,d.leaf,true);b.transform.localRotation=Quaternion.identity;}
        static void DoorWall(string name,float x0,float x1,float z,float centre)
        {Wall(name+" left",x0,z,centre-1.5f,z);Wall(name+" right",centre+1.5f,z,x1,z);Box("Door lintel",new Vector3(centre,3.85f,z),new Vector3(3,2.3f,.35f),"StationWall");Door(name,centre,z);Sign(name.ToUpper(),new Vector3(centre,3.2f,z-.22f),Mathf.Min(10,x1-x0-1));}
        static void Window(float x,float z,float length,bool alongX=true,float y=0)
        {
            Vector3 size=alongX?new Vector3(length,3.6f,.12f):new Vector3(.12f,3.6f,length);Box("Armoured glazing",new Vector3(x,y+2.5f,z),size,"Station panoramic glass");
            for(int i=0;i<=Mathf.CeilToInt(length/4);i++){float k=-length/2+i*length/Mathf.CeilToInt(length/4);Box("Window mullion",new Vector3(x+(alongX?k:0),y+2.5f,z+(alongX?0:k)),new Vector3(.15f,3.7f,.15f),"Steel");}
            foreach(float h in new[]{.6f,4.4f})Box("Window sill and header",new Vector3(x,y+h,z),alongX?new Vector3(length,.3f,.3f):new Vector3(.3f,.3f,length),"Steel");
        }
        static void Rubble(string name,float x,float z,float width,float depth,int count)
        {
            var root=Group(name,parent);Box("Solid collapse core",new Vector3(x,1,z),new Vector3(width,2,depth),"Concrete",true,root).GetComponent<MeshRenderer>().enabled=false;
            for(int i=0;i<count;i++){float px=x+(float)(rng.NextDouble()-.5)*width,pz=z+(float)(rng.NextDouble()-.5)*depth;var g=Box("Broken concrete slab",new Vector3(px,.5f+(float)rng.NextDouble()*1.4f,pz),new Vector3(2+(float)rng.NextDouble()*3,.6f+(float)rng.NextDouble()*1.8f,2+(float)rng.NextDouble()*3),i%4==0?"Rust":"Concrete",true,root);g.transform.rotation=Quaternion.Euler(rng.Next(15,65),rng.Next(360),rng.Next(-25,25));}
        }
        [MenuItem("Vaccine/Create Lab District and Train Route")]
        public static string Create()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Stop Play and save first.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("Lab exists; edit it instead of replacing it.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=VaccineBlockoutBuilder.ScenePath)EditorSceneManager.OpenScene(VaccineBlockoutBuilder.ScenePath);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath,true);EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(MeshDir);AssetDatabase.Refresh();uvMeshes.Clear();meshId=0;
            mats=new Dictionary<string,Material>();foreach(string dir in new[]{"Assets/Vaccine/Materials/Art","Assets/Vaccine/Materials/SubwayArt"})foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{dir})){var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));mats[m.name]=m;}
            player=Object.FindAnyObjectByType<VaccineWalkthrough>();var ui=GameObject.Find("Vaccine walkthrough controls");var overview=player.overview.gameObject;
            var train=Object.Instantiate(GameObject.Find("Platform 1 wide connected train"));train.name="Route boarding train";
            foreach(var r in train.GetComponentsInChildren<MeshRenderer>(true)){r.enabled=true;r.gameObject.isStatic=false;}
            var trainRoof=Group("Train roofs",train.transform);
            foreach(var r in GameObject.Find("Subway ceiling and train roofs").GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.bounds.center.z>17&&r.bounds.center.x>=100&&r.bounds.center.x<155&&r.bounds.size.x<18))
            {var g=Object.Instantiate(r.gameObject,trainRoof,true);g.GetComponent<Renderer>().enabled=true;g.isStatic=false;}
            var moon=Object.FindObjectsByType<Light>().First(l=>l.type==LightType.Directional);var moonCopy=Object.Instantiate(moon.gameObject);moonCopy.name="District moonlight";
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())if(root!=player.gameObject&&root!=ui&&root!=overview&&root!=train&&root!=moonCopy)Object.DestroyImmediate(root);
            var oldMap=player.GetComponent<SubwayReviewMap>();if(oldMap)Object.DestroyImmediate(oldMap);
            foreach(var old in player.GetComponents<MonoBehaviour>().Where(c=>c&&c!=player&&c.GetType().Name.Contains("Checks")).ToArray())Object.DestroyImmediate(old);
            district=Group("Lab district / user sketch");roofs=Group("Lab roofs / hidden on map");labels=Group("Lab plan labels");parent=district;
            player.roofs=roofs.gameObject;player.mapLabels=labels.gameObject;labels.gameObject.SetActive(false);
            player.transform.SetPositionAndRotation(new Vector3(9,.08f,52),Quaternion.Euler(0,60,0));player.overview.transform.position=new Vector3(75,160,54);player.overview.orthographicSize=58;
            player.eye.farClipPlane=240;player.overview.farClipPlane=230;
            var review=player.gameObject.AddComponent<LabDistrictReview>();review.player=player;review.train=train.transform;review.trainDoors=train.GetComponentsInChildren<SubwayTrainDoor>();foreach(var d in review.trainDoors)d.actor=player.transform;
            foreach(var t in ui.GetComponentsInChildren<TextMeshProUGUI>())if(t.text.StartsWith("WASD"))t.text="WASD Walk   SHIFT Sprint   E Doors   TAB Map   T Train preview   ENTER Reset   ESC Cursor";
            AssetDatabase.StartAssetEditing();
            try{GroundAndStreet();Building();StreetProps();StationRoute(review);Plan("LAB COMPLEX",90,82,42);Plan("PARKING / SHIP 2",74,17,38);Plan("SHIP 1",132,18);Plan("SHIP 3",141,88);Plan("SUBWAY EXIT",14,46);}
            finally{AssetDatabase.StopAssetEditing();}
            train.transform.position=new Vector3(-120,0,0);Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return "Created textured lab district, four research rooms, glass stairwell, security, lounge, street/landing areas and four-stop train preview.";
        }
        static void GroundAndStreet()
        {
            Floor("West pavement",0,6,0,105,0,"Concrete");Floor("District road",10.2f,156,0,105,0,"Asphalt");Floor("Pavement south of stair",6,10.2f,0,34,0,"Concrete");Floor("Pavement north of stair",6,10.2f,47,105,0,"Concrete");
            Floor("Lab frontage pavement",40,125,57,60,.06f,"Concrete");Floor("Lab west pavement",37,40,60,105,.06f,"Concrete");
            for(int z=6;z<97;z+=11)Box("North-south road dash",new Vector3(22,.015f,z),new Vector3(.2f,.02f,6),"Markings",false);
            for(int x=42;x<150;x+=14)Box("East-west road dash",new Vector3(x,.015f,46),new Vector3(8,.02f,.2f),"Markings",false);
            for(int z=64;z<98;z+=11)Box("East road dash",new Vector3(139,.015f,z),new Vector3(.2f,.02f,6),"Markings",false);
            Floor("Parking apron",40,106,0,34,.04f,"Asphalt");Wall("Parking west boundary",40,0,40,34,2.3f,0,"Concrete");Wall("Parking front west",40,34,65,34,1.5f);Wall("Parking front east",85,34,106,34,1.5f);Wall("Parking east barrier",106,0,106,34,2.3f,0,"Concrete");
            for(int x=45;x<102;x+=7)Box("Parking bay marking",new Vector3(x,.07f,15),new Vector3(.12f,.02f,12),"Markings",false);
            foreach(float x in new[]{0,156})Wall("District perimeter",x,0,x,105,9,0,"Concrete");Wall("Southern perimeter",0,0,156,0,9,0,"Concrete");Wall("Northern perimeter",0,105,156,105,9,0,"Concrete");
            Rubble("North road collapse / future approach",20,101,36,7,18);Rubble("South road collapse / future approach",20,4,36,7,18);Rubble("Ship 3 outer collapse",140,101,27,8,18);
            foreach(var v in new[]{new Vector3(75,0,26),new Vector3(132,0,17),new Vector3(140,0,87)})
            {
                for(int i=0;i<4;i++){float a=i*90*Mathf.Deg2Rad;Box("Landing corner marking",v+new Vector3(Mathf.Cos(a)*8,.07f,Mathf.Sin(a)*8),new Vector3(i%2==0?.2f:5,.03f,i%2==0?5:.2f),"Markings",false);}
                var anchor=Group("Future ship landing anchor",parent);anchor.position=v;
            }
            Rubble("Rubble along ship 1 perimeter",151,18,6,28,15);Rubble("Ship 1 southern wreckage",131,3,31,5,12);
            foreach(float z in new[]{18,72,94}){Box("Neighbouring west facade",new Vector3(-3,9,z),new Vector3(6,18,22),"Concrete");for(int y=3;y<17;y+=4)for(int k=0;k<4;k++)Box("Dark abandoned window",new Vector3(.1f,y,z-8+k*5),new Vector3(.1f,2.2f,2.8f),"Glass",false);}
        }
        static void Building()
        {
            parent=Group("Research complex",district);Floor("Laboratory slab",40,125,60,105,.08f);Floor("Main lab roof",59,125,60,105,5.4f,"Concrete",roofs);
            Wall("West stairwell lower return",40,60,40,77);Window(40,91,28,false);Wall("West glass kick wall",40,77,40,105,.6f);Wall("Lab north wall",40,105,125,105);Wall("Lab east wall",125,60,125,105);
            Wall("Front stair block",40,60,51,60);Window(55,60,8); // Entrance glazing has a real door opening beneath its header.
            var frontGlass=parent.GetComponentsInChildren<Transform>().Where(t=>t.name=="Armoured glazing"&&Mathf.Abs(t.position.z-60)<.01f&&t.position.x==55).First();Object.DestroyImmediate(frontGlass.gameObject);
            foreach(var post in parent.GetComponentsInChildren<Transform>().Where(t=>t.name=="Window mullion"&&Mathf.Abs(t.position.z-60)<.01f&&t.position.x>53&&t.position.x<57).ToArray())Object.DestroyImmediate(post.gameObject);
            Door("Laboratory entrance",55,60,4);Sign("KESTREL / VACCINE RESEARCH",new Vector3(55,4.9f,59.7f),23);
            Wall("Front security wall",59,60,79,60);Window(92,60,26);Window(113.5f,60,23);Wall("Glazed frontage kick wall",79,60,125,60,.6f);
            Wall("Security side south",59,60,59,65.5f);Wall("Security side north",59,70.5f,59,75);Box("Security opening header",new Vector3(59,4,68),new Vector3(.35f,2,5),"StationWall");Wall("Security north screen",59,75,79,75);Window(69,75,12,true);Wall("Room 1 west",79,60,79,75);Wall("Room 1 east",102,60,102,75);DoorWall("Room 1 / Analysis",79,102,75,91);
            foreach(float x in new[]{59,78,96,106})Wall("Research room partition",x,85,x,105);DoorWall("Room 4 / Cold storage",59,78,85,68);DoorWall("Room 3 / Biology",78,96,85,87);DoorWall("Room 2 / Development",106,125,85,115);
            Floor("Glass stairwell high roof",40,59,60,105,9,"Concrete",roofs);
            for(int i=0;i<24;i++)Box("Lab stair step",new Vector3(44.5f,(i+1)*.2f-.16f,66+(i+.5f)*.7f),new Vector3(4,.32f,.7f),"StationFloor");
            Floor("Glass stairwell mezzanine",40,59,83,105,4.8f);Wall("Mezzanine guard",48,66,48,83,1.1f,4.8f,"Steel");Wall("Upper observation guard",48,83,59,83,1.1f,4.8f,"Steel");
            Sign("RESEARCH / AUTHORIZED STAFF",new Vector3(59.3f,3.2f,80),13,90);
            // Security desk, monitors and equipment.
            Box("Security counter",new Vector3(68, .65f,67),new Vector3(10,1.3f,1.8f),"Steel");for(int i=0;i<3;i++){Box("Security monitor",new Vector3(64+i*3,1.85f,67.2f),new Vector3(1.5f,.85f,.12f),"Glass");Box("Monitor pedestal",new Vector3(64+i*3,1.4f,67.2f),new Vector3(.2f,.4f,.2f),"Steel");}
            for(int i=0;i<3;i++){Box("Security locker",new Vector3(75.6f,1.3f,63+i*3),new Vector3(1.5f,2.6f,1.2f),"SheetMetal");}
            foreach(var c in new[]{new Vector3(112,0,67),new Vector3(120,0,77)}){Box("Lounge sofa",c+Vector3.up*.5f,new Vector3(6,.8f,1.4f),"Train seat vinyl");Box("Sofa back",c+new Vector3(0,1.1f,.6f),new Vector3(6,.9f,.25f),"Train seat vinyl");Box("Low lounge table",c+new Vector3(0,.4f,-2),new Vector3(3,.8f,1.3f),"Cream");}
            Box("Water cooler",new Vector3(123, .7f,82),new Vector3(.9f,1.4f,.8f),"Cream");Box("Information terminal",new Vector3(104,1,79),new Vector3(1,2,.7f),"Steel");Sign("RECEPTION / LOUNGE",new Vector3(124.7f,3.7f,71),13,90);
            LabFurniture(90,67,1);LabFurniture(116,95,2);LabFurniture(87,95,3);LabFurniture(68,95,4);
            foreach(var p in new[]{new Vector3(55,4,70),new Vector3(54,7.5f,94),new Vector3(69,4,68),new Vector3(90,4,67),new Vector3(113,4,72),new Vector3(70,4,80),new Vector3(92,4,80),new Vector3(114,4,95),new Vector3(87,4,95),new Vector3(68,4,95)}){Light(p,42,18);Box("Ceiling fluorescent",p+Vector3.up*.65f,new Vector3(3,.12f,.45f),"LampCool",false,roofs);}
            foreach(var p in new[]{new Vector3(68,0,93),new Vector3(88,0,93),new Vector3(116,0,93),new Vector3(91,0,67)}){var a=Group("Future lab objective / encounter",parent);a.position=p;}
            parent=district;
        }
        static void LabFurniture(float x,float z,int type)
        {
            foreach(float side in new[]{-1f,1f}){Box("Laboratory workbench",new Vector3(x+side*5, .6f,z),new Vector3(2.4f,1.2f,9),"Cream");Box("Stainless worktop",new Vector3(x+side*5,1.27f,z),new Vector3(2.6f,.14f,9.2f),"Steel");for(int j=0;j<3;j++){Box("Instrument case",new Vector3(x+side*5,1.65f,z-3+j*3),new Vector3(1.1f,.65f,1.2f),j%2==0?"PaintBlue":"SheetMetal");Box("Instrument screen",new Vector3(x+side*4.4f,1.8f,z-3+j*3),new Vector3(.05f,.35f,.6f),"LampCool",false);}}
            if(type==4){for(int j=0;j<4;j++){Box("Vaccine cold cabinet",new Vector3(x-6+j*4,1.6f,102.5f),new Vector3(2.5f,3.2f,2),"Steel");Box("Cold storage glass door",new Vector3(x-6+j*4,1.7f,101.45f),new Vector3(2.1f,2.4f,.08f),"Glass");}}
            else if(type==3){for(int j=0;j<3;j++){var t=GameObject.CreatePrimitive(PrimitiveType.Cylinder);t.name="Containment specimen tank";t.transform.SetParent(parent,false);t.transform.position=new Vector3(x-4+j*4,1.5f,102);t.transform.localScale=new Vector3(2,1.5f,2);t.GetComponent<Renderer>().sharedMaterial=mats["Station panoramic glass"];t.isStatic=true;Box("Containment plinth",new Vector3(x-4+j*4,.3f,102),new Vector3(2.4f,.6f,2.4f),"Steel");}}
            else{Box("Analysis workstation",new Vector3(x, .75f,z+3),new Vector3(2,1.5f,2),"SheetMetal");Box("Analysis screen",new Vector3(x,1.8f,z+3),new Vector3(1.4f,.7f,.12f),"LampCool",false);}
        }
        static void StreetProps()
        {
            parent=Group("Street dressing and boundaries",district);
            foreach(var p in new[]{new Vector3(13,0,20),new Vector3(13,0,69),new Vector3(35,0,92),new Vector3(45,0,53),new Vector3(90,0,53),new Vector3(126,0,53),new Vector3(152,0,80),new Vector3(103,0,29)}){Box("Street lamp mast",p+Vector3.up*3.8f,new Vector3(.18f,7.6f,.18f),"Steel");Box("Street lamp head",p+Vector3.up*7.4f,new Vector3(1.6f,.18f,.5f),"LampWarm",false);Light(p+Vector3.up*6.8f,95,25,true);}
            Vehicle(new Vector3(30,0,65),false);Vehicle(new Vector3(74,0,55),true);Vehicle(new Vector3(48,0,13),false);Vehicle(new Vector3(98,0,14),false);
            for(int x=43;x<126;x+=7)if(x<51||x>59)Box("Frontage bollard",new Vector3(x,.65f,57),new Vector3(.25f,1.3f,.25f),"Steel");
            foreach(float z in new[]{65,90}){Box("Street utility cabinet",new Vector3(36,1,z),new Vector3(1.2f,2,2),"SheetMetal");}
            for(int i=0;i<14;i++)Box("Scattered street debris",new Vector3(27+(float)rng.NextDouble()*8,.1f,3+(float)rng.NextDouble()*93),new Vector3(.4f,.2f,.7f),"Concrete").transform.Rotate(0,i*37,0);
            parent=district;
        }
        static void Vehicle(Vector3 p,bool truck)
        {
            var g=Group(truck?"Abandoned delivery truck":"Abandoned car",parent);float length=truck?11:5.2f;
            Box("Vehicle chassis",p+Vector3.up*.8f,new Vector3(length,1.1f,2.8f),"PaintBlue",true,g);
            Box("Cabin",p+new Vector3(truck?4:0,1.7f,0),new Vector3(truck?2.5f:2.9f,1.1f,2.5f),"SheetMetal",true,g);
            Box("Windscreen",p+new Vector3(truck?5.28f:1.48f,1.85f,0),new Vector3(.08f,.7f,2.15f),"Glass",true,g);
            if(truck)Box("Cargo body",p+new Vector3(-1.2f,2,0),new Vector3(7,2.5f,2.8f),"SheetMetal",true,g);
            foreach(float x in new[]{-length*.32f,length*.32f})foreach(float z in new[]{-1.4f,1.4f}){var w=GameObject.CreatePrimitive(PrimitiveType.Cylinder);w.name="Vehicle tyre";w.transform.SetParent(g,false);w.transform.position=p+new Vector3(x,.55f,z);w.transform.rotation=Quaternion.Euler(90,0,0);w.transform.localScale=new Vector3(1.15f,.22f,1.15f);w.GetComponent<Renderer>().sharedMaterial=mats["Rubber"];w.isStatic=true;}
        }
        static void StationRoute(LabDistrictReview review)
        {
            review.stations=new GameObject[4];float[] starts={-500,-350,-200,-20};string[] names={"01 / FOUNDRY","02 / CIVIC EXCHANGE","03 / QUARANTINE","04 / RESEARCH DISTRICT"};
            for(int i=0;i<4;i++)
            {
                float x=starts[i];parent=Group(names[i]);review.stations[i]=parent.gameObject;Floor("Station platform",x-5,x+68,26,34,-12);Floor("Station track bed",x-8,x+70,14,26,-13.2f,"Track ballast");Floor("Station roof",x-8,x+70,14,34,-6.5f,"Concrete",roofs);
                Wall("Station rear wall",x-8,14,x+70,14,7,-13.2f,"Concrete");Wall("Station far platform wall",x-5,34,x+68,34,5,-12);
                foreach(float z in new[]{19.1f,24.1f})Box("Running rail",new Vector3(x+31,-12.97f,z),new Vector3(78,.2f,.14f),"Steel");
                Box("Platform tactile edge",new Vector3(x+31,-11.97f,26.4f),new Vector3(73,.06f,.65f),"Tactile yellow");
                for(int k=0;k<7;k++){float sx=x+k*10;Box("Platform support",new Vector3(sx,-9.5f,32.8f),new Vector3(.5f,5,.5f),"Concrete");Box("Station bench",new Vector3(sx,-11.5f,31.5f),new Vector3(4,.6f,.8f),"Train seat vinyl");Light(new Vector3(sx,-8,29),30,15);Box("Strip light",new Vector3(sx,-7.3f,29),new Vector3(4,.12f,.4f),"LampCool",false,roofs);}
                Sign(names[i],new Vector3(x+29,-8.5f,33.65f),25);foreach(float dx in new[]{10,48}){Box("Closed station exit",new Vector3(x+dx,-10.5f,33.7f),new Vector3(3,3,.15f),"Steel");var a=Group("Future zombie boarding approach",parent);a.position=new Vector3(x+dx,-12,29);}
                if(i<3){Sign("EVACUATION SERVICE / REMAIN ABOARD",new Vector3(x+29,-9.9f,33.65f),30);parent.gameObject.SetActive(false);}
            }
            // Physical tunnel between the stations, visible through the train windows while travelling.
            parent=Group("Transit tunnel scenery");Floor("Continuous tunnel track",-510,50,14,26,-13.5f,"Track ballast");foreach(float z in new[]{14,36})Wall("Transit tunnel side",-510,z,50,z,7,-13.5f,"Concrete");Floor("Transit tunnel roof",-510,50,14,36,-6.4f,"Concrete",roofs);
            parent=review.stations[3].transform;
            // Replace a section of the fourth station wall with the stair opening.
            var rear=parent.Find("Station far platform wall");Object.DestroyImmediate(rear.gameObject);Wall("Final station wall west",-25,34,5.8f,34,5,-12);Wall("Final station wall east",10.2f,34,48,34,5,-12);
            for(int flight=0;flight<3;flight++)for(int k=0;k<20;k++){bool reverse=flight==1;float z=reverse?47-(k+.5f)*.65f:34+(k+.5f)*.65f;float top=-12+flight*4+(k+1)*.2f;Box("Exit flight "+(flight+1)+" step "+(k+1),new Vector3(reverse?15:8,top-.18f,z),new Vector3(4,.36f,.65f),"StationFloor");}
            Floor("Exit landing one",6,17,47,51,-8);Floor("Exit landing two",6,17,30,34,-4);Floor("Exit top pavement",6,10.2f,47,52,0,"Concrete");
            Wall("Stair east enclosure",17.3f,30,17.3f,51,13,-12.5f);Wall("Stair west enclosure",5.7f,30,5.7f,51,13,-12.5f);Wall("Stair north enclosure",5.7f,51,17.3f,51,11,-12.5f);Wall("Stair divider",11.5f,34,11.5f,47,12,-12.5f);
            // Close ground-level sides of the opening with low headhouse walls and railings.
            Wall("Exit headhouse west rail",5.7f,34,5.7f,47,1.1f,0,"Steel");Wall("Exit headhouse east rail",10.3f,34,10.3f,47,1.1f,0,"Steel");Wall("Exit headhouse south rail",5.7f,34,10.3f,34,1.1f,0,"Steel");
            foreach(var p in new[]{new Vector3(8,-8,40),new Vector3(15,-4,40),new Vector3(8,-.5f,40)})Light(p,30,14);
            Sign("EXIT / RESEARCH DISTRICT",new Vector3(8,-9,33.7f),12);Sign("METRO / RESEARCH",new Vector3(8,3,47.4f),10,180);
            parent=district;
        }
        [MenuItem("Vaccine/Finish Lab District Details")]
        public static string Finish()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open Lab outside Play.");
            if(GameObject.Find("Lab finishing details"))throw new InvalidOperationException("Already finished.");
            district=GameObject.Find("Lab district / user sketch").transform;roofs=GameObject.Find("Lab roofs / hidden on map").transform;labels=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).First(t=>t.name=="Lab plan labels");parent=Group("Lab finishing details",district);
            meshId=Directory.GetFiles(MeshDir,"Surface-*.asset").Length;uvMeshes.Clear();mats=new Dictionary<string,Material>();foreach(string dir in new[]{"Assets/Vaccine/Materials/Art","Assets/Vaccine/Materials/SubwayArt"})foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{dir})){var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));mats[m.name]=m;}
            AssetDatabase.StartAssetEditing();try
            {
                foreach(var sill in Object.FindObjectsByType<Transform>().Where(t=>t.name=="Window sill and header"&&Mathf.Abs(t.position.x-55)<.01f&&Mathf.Abs(t.position.z-60)<.01f&&t.position.y<1).ToArray())Object.DestroyImmediate(sill.gameObject);
                foreach(float x in new[]{52f,58f})Box("Entrance side glazing",new Vector3(x,2.5f,60),new Vector3(2,3.6f,.12f),"Station panoramic glass");
                var roomSign=Object.FindObjectsByType<Transform>().First(t=>t.name=="ROOM 1 / ANALYSIS");roomSign.position=new Vector3(91,3.2f,75.22f);roomSign.rotation=Quaternion.Euler(0,180,0);
                // Clear furniture from the fourth station's actual stair approach.
                var exitBench=Object.FindAnyObjectByType<LabDistrictReview>().stations[3].GetComponentsInChildren<Transform>().First(t=>t.name=="Station bench"&&Mathf.Abs(t.position.x-10)<.01f);exitBench.position+=Vector3.right*4;
                // Close the tall glass stairwell all the way to its roof.
                Wall("Upper stairwell front masonry",40,60,51,60,4,5);Window(55,60,8,true,4.3f);Window(40,91,28,false,4.3f);Wall("Upper stairwell west return",40,60,40,77,4,5);Wall("Upper stairwell north wall",40,105,59,105,4,5);Wall("Upper stairwell east wall",59,60,59,105,4,5);
                // Provide a real view through the security-room window.
                Object.DestroyImmediate(GameObject.Find("Security north screen"));Wall("Security window left pier",59,75,63,75);Wall("Security window right pier",75,75,79,75);Wall("Security window lower wall",63,75,75,75,.6f);Wall("Security window upper wall",63,75,75,75,.6f,4.4f);
                foreach(var w in Object.FindObjectsByType<Transform>().Where(t=>t.name=="Transit tunnel side"&&t.position.z>35).ToArray())Object.DestroyImmediate(w.gameObject);
                Wall("Tunnel wall before exit",-510,36,5.7f,36,7,-13.5f,"Concrete");Wall("Tunnel wall beyond exit",17.3f,36,50,36,7,-13.5f,"Concrete");Wall("Lab station terminus",50,14,50,36,7,-13.5f,"Concrete");
                // User clarification: all four red rectangles are rubble; parking uses sheet walls.
                Object.DestroyImmediate(GameObject.Find("Parking west boundary"));Object.DestroyImmediate(GameObject.Find("Parking front west"));Object.DestroyImmediate(GameObject.Find("Parking front east"));Object.DestroyImmediate(GameObject.Find("Parking east barrier"));
                SheetRun(40,0,40,24);SheetRun(40,29,40,34);SheetRun(40,34,65,34);SheetRun(85,34,106,34);SheetRun(106,0,106,34);SheetRun(40,1,106,1);
                Rubble("Red rectangle / parking entrance collapse",75,34,20,8,22);Sign("PARKING / SIDE ACCESS",new Vector3(39.7f,3.7f,26.5f),10,90);
                // Reuse authored Blender vehicle and debris meshes rather than primitive silhouettes.
                foreach(var car in Object.FindObjectsByType<Transform>().Where(t=>t.name=="Abandoned car").ToArray())
                {var p=car.Find("Vehicle chassis").position-Vector3.up*.8f;foreach(Transform c in car.Cast<Transform>().ToArray())Object.DestroyImmediate(c.gameObject);var model=Model("AbandonedSedan",car);model.transform.position=p;model.transform.rotation=Quaternion.Euler(0,90,0);var bounds=model.GetComponentInChildren<Renderer>().bounds;var col=car.gameObject.AddComponent<BoxCollider>();col.center=bounds.center;col.size=bounds.size;}
                int index=0;foreach(var slab in Object.FindObjectsByType<MeshRenderer>().Where(r=>r.name=="Broken concrete slab").ToArray())
                {var model=Model("ConcreteDebris"+(index++%3),slab.transform.parent);var b=model.GetComponentInChildren<Renderer>().bounds;model.transform.position=slab.transform.position;model.transform.localScale=new Vector3(slab.transform.localScale.x/b.size.x,slab.transform.localScale.y/b.size.y,slab.transform.localScale.z/b.size.z);model.transform.rotation=slab.transform.rotation;slab.enabled=false;}
                foreach(var item in new[]{new Vector3(68,0,95),new Vector3(87,0,95),new Vector3(115,0,95),new Vector3(90,0,67)})Plan(item.z<80?"ROOM 1":item.x<75?"ROOM 4":item.x<100?"ROOM 3":"ROOM 2",item.x,item.z,17);
                Plan("SECURITY",69,68,18);Plan("LOUNGE",115,77,18);
                foreach(float z in new[]{69,81,94})Box("Stairwell facade brace",new Vector3(39.8f,4.5f,z),new Vector3(.35f,9,.35f),"Steel");
                foreach(float x in new[]{65,90,113}){Box("Rooftop vent",new Vector3(x,6,98),new Vector3(4,1.5f,2.4f),"Steel",true,roofs);}
            }finally{AssetDatabase.StopAssetEditing();}
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return "Sheet-walled parking, four rubble blockages, enclosed glass stairwell and authored vehicle/debris meshes saved.";
        }
        static GameObject Model(string asset,Transform into)
        {var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vaccine/Models/"+asset+".fbx"));g.transform.SetParent(into,false);foreach(var r in g.GetComponentsInChildren<Renderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>m&&mats.ContainsKey(m.name)?mats[m.name]:mats["Concrete"]).ToArray();r.gameObject.isStatic=true;}return g;}
        static void SheetRun(float x0,float z0,float x1,float z1)
        {var a=new Vector3(x0,0,z0);var b=new Vector3(x1,0,z1);float length=Vector3.Distance(a,b);int count=Mathf.CeilToInt(length/3);for(int i=0;i<count;i++){var g=Box("Overlapping parking sheet",Vector3.Lerp(a,b,(i+.5f)/count)+Vector3.up*1.8f,new Vector3(.15f,3.6f,length/count+.08f),i%4==0?"Rust":"SheetMetal");g.transform.rotation=Quaternion.LookRotation(b-a);}for(int i=0;i<=count;i++)Box("Sheet barricade support",Vector3.Lerp(a,b,(float)i/count)+Vector3.up*1.7f,new Vector3(.2f,3.4f,.2f),"Steel");}
        [MenuItem("Vaccine/Batch Lab District Scenery")]
        public static string Bake()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open Lab outside Play.");
            if(GameObject.Find("Lab static batches"))throw new InvalidOperationException("Already batched.");
            var rs=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r=>r.enabled&&r.gameObject.isStatic&&r.GetComponent<MeshFilter>()&&!r.GetComponent<TMP_Text>()&&!r.GetComponentInParent<ReviewDoor>()&&r.sharedMaterial&&r.sharedMaterials.All(m=>m&&m.renderQueue<3000)).ToArray();
            var parts=rs.SelectMany(r=>Enumerable.Range(0,Mathf.Min(r.GetComponent<MeshFilter>().sharedMesh.subMeshCount,r.sharedMaterials.Length)).Select(index=>new{Renderer=r,Index=index,Material=r.sharedMaterials[index]}));
            var buckets=parts.GroupBy(p=>new{Root=p.Renderer.transform.root,X=Mathf.FloorToInt(p.Renderer.bounds.center.x/24),Z=Mathf.FloorToInt(p.Renderer.bounds.center.z/24),p.Material});
            var manifest=new GameObject("Lab static batches").AddComponent<VaccineStaticArtCache>();var generated=new List<GameObject>();int i=0;
            AssetDatabase.StartAssetEditing();try{foreach(var group in buckets){var mesh=new Mesh(){indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(p=>new CombineInstance{mesh=p.Renderer.GetComponent<MeshFilter>().sharedMesh,subMeshIndex=p.Index,transform=p.Renderer.transform.localToWorldMatrix}).ToArray(),true,true);AssetDatabase.CreateAsset(mesh,MeshDir+"/Batch-"+(++i).ToString("D3")+".asset");var g=new GameObject("Batched "+group.Key.Material.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(group.Key.Root,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=group.Key.Material;g.isStatic=true;generated.Add(g);}}finally{AssetDatabase.StopAssetEditing();}
            foreach(var r in rs)r.enabled=false;manifest.sources=rs;manifest.generatedGroups=generated.ToArray();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return rs.Length+" static renderers in "+i+" batches";
        }
    }
}
