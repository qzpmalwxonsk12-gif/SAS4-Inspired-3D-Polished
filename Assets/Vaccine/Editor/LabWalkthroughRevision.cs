using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Vaccine.Editor
{
    public static partial class LabDistrictBuilder
    {
        const float TerraceY=1.88f;
        static Transform[] All()=>Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        static void RemoveNamed(params string[] names)
        {foreach(var t in All().Where(t=>names.Contains(t.name)).ToArray())if(t)Object.DestroyImmediate(t.gameObject);}
        static void RevisionContext()
        {
            district=GameObject.Find("Lab district / user sketch").transform;
            roofs=GameObject.Find("Lab roofs / hidden on map").transform;
            player=Object.FindAnyObjectByType<VaccineWalkthrough>();
            meshId=Directory.GetFiles(MeshDir,"Surface-*.asset").Select(p=>int.Parse(Path.GetFileNameWithoutExtension(p).Substring(8))).DefaultIfEmpty(0).Max();
            uvMeshes.Clear();mats=new Dictionary<string,Material>();
            foreach(string dir in new[]{"Assets/Vaccine/Materials/Art","Assets/Vaccine/Materials/SubwayArt"})
                foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{dir})){var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));mats[m.name]=m;}
        }
        public static void RestoreLabBatches()
        {
            var root=GameObject.Find("Lab static batches");if(!root)return;
            var c=root.GetComponent<VaccineStaticArtCache>();foreach(var r in c.sources)if(r)r.enabled=true;
            foreach(var g in c.generatedGroups)if(g)Object.DestroyImmediate(g);
            Object.DestroyImmediate(root);
            foreach(string p in Directory.GetFiles(MeshDir,"Batch-*.asset"))AssetDatabase.DeleteAsset(p.Replace('\\','/'));
        }
        [MenuItem("Vaccine/Apply Raised Lab and Connected Walkthrough")]
        public static string ApplyWalkthroughRevision()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open Lab outside Play.");
            if(GameObject.Find("Raised lab revision"))throw new InvalidOperationException("Revision already applied.");
            RestoreLabBatches();RevisionContext();
            var complex=GameObject.Find("Research complex").transform;
            var finish=GameObject.Find("Lab finishing details").transform;
            RemoveNamed("Lab stair step","Glass stairwell mezzanine","Mezzanine guard","Upper observation guard","Glass stairwell high roof","Laboratory entrance","KESTREL / VACCINE RESEARCH","RESEARCH / AUTHORIZED STAFF","West stairwell lower return","West glass kick wall","Front stair block","Lab north wall","Entrance side glazing","Stairwell facade brace");
            foreach(var t in All().Where(t=>t.name.StartsWith("Upper stairwell")||
                ((t.IsChildOf(complex)||t.IsChildOf(finish))&&t.position.x<59&&
                  (t.name=="Armoured glazing"||t.name=="Window mullion"||t.name=="Window sill and header"||t.name=="Local practical light"))).ToArray())if(t)Object.DestroyImmediate(t.gameObject);
            foreach(var t in roofs.Cast<Transform>().ToArray())
            {if(t.position.z>=60&&t.position.y>0){if(t.position.x<59)Object.DestroyImmediate(t.gameObject);else t.position+=Vector3.up*1.8f;}}
            complex.position+=Vector3.up*1.8f;
            foreach(var t in finish.Cast<Transform>().Where(t=>t.name.StartsWith("Security window")).ToArray())t.position+=Vector3.up*1.8f;
            parent=Group("Raised lab revision",district);
            AssetDatabase.StartAssetEditing();try
            {
                Box("Reinforced raised foundation",new Vector3(82.5f,.74f,82.5f),new Vector3(85,1.48f,45),"Concrete");
                Wall("Lab north wall raised",59,105,125,105,5,TerraceY);
                // Nine risers in two perpendicular flights, with a full square turning landing.
                float rise=TerraceY/9;
                for(int i=0;i<5;i++)Box("Terrace lower step "+(i+1),new Vector3(49.5f,(i+1)*rise/2,54+(i+.5f)*.65f),new Vector3(3,(i+1)*rise,.65f),"StationFloor");
                Box("Square turning landing",new Vector3(51,5*rise/2,58.75f),new Vector3(6,5*rise,3),"StationFloor");
                for(int i=0;i<4;i++)Box("Terrace upper step "+(i+1),new Vector3(54+(i+.5f)*.65f,(i+6)*rise/2,58.75f),new Vector3(.65f,(i+6)*rise,3),"StationFloor");
                Box("Terrace top landing",new Vector3(57.8f,TerraceY/2,59.625f),new Vector3(2.4f,TerraceY,4.75f),"StationFloor");
                GlassRail(new Vector3(47.85f,0,54),new Vector3(47.85f,5*rise,57.25f));
                GlassRail(new Vector3(51.15f,0,54),new Vector3(51.15f,5*rise,57.25f));
                GlassRail(new Vector3(47.85f,5*rise,57.25f),new Vector3(47.85f,5*rise,60.4f));
                GlassRail(new Vector3(47.85f,5*rise,60.4f),new Vector3(54,5*rise,60.4f));
                GlassRail(new Vector3(51.15f,5*rise,57.1f),new Vector3(54,5*rise,57.1f));
                GlassRail(new Vector3(54,5*rise,57.1f),new Vector3(56.6f,TerraceY,57.1f));
                GlassRail(new Vector3(54,5*rise,60.4f),new Vector3(56.6f,TerraceY,60.4f));
                GlassRail(new Vector3(56.6f,TerraceY,57.1f),new Vector3(59.15f,TerraceY,57.1f));
                GlassRail(new Vector3(59.15f,TerraceY,57.1f),new Vector3(59.15f,TerraceY,60));
                GlassRail(new Vector3(40,TerraceY,60.5f),new Vector3(40,TerraceY,104.7f));
                GlassRail(new Vector3(40,TerraceY,104.7f),new Vector3(58.5f,TerraceY,104.7f));
                GlassRail(new Vector3(40,TerraceY,60.5f),new Vector3(47.5f,TerraceY,60.5f));
                // Inner rail leaves clear openings into security and the main lab entrance.
                foreach(var span in new[]{new Vector2(60.5f,65),new Vector2(71,77),new Vector2(83,104.7f)})GlassRail(new Vector3(58.5f,TerraceY,span.x),new Vector3(58.5f,TerraceY,span.y));
                SecurityEntrance();
                Sign("KESTREL / VACCINE RESEARCH",new Vector3(69,6.25f,59.7f),18);
                Sign("SECURITY / LAB ACCESS",new Vector3(58.65f,5.3f,68),7,90);
                foreach(float z in new[]{66f,79,95}){Box("Terrace wall light",new Vector3(58.6f,4.8f,z),new Vector3(.2f,.3f,1.2f),"LampWarm",false);Light(new Vector3(55,4.3f,z),28,14,true);}
                StreetRevision();ExtendTunnel();
            }finally{AssetDatabase.StopAssetEditing();}
            var review=Object.FindAnyObjectByType<LabDistrictReview>();review.stopSeconds=8;review.travelSpeed=28;
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return "Raised lab 1.8m; nine-step turning stair, glass terrace, security-controlled entry, narrow street, enlarged truck and Ship 1 parking access saved.";
        }
        static void GlassRail(Vector3 a,Vector3 b)
        {
            var d=b-a;var flat=new Vector3(d.x,0,d.z);int n=Mathf.CeilToInt(flat.magnitude/2.5f);
            for(int i=0;i<n;i++)
            {
                var p=Vector3.Lerp(a,b,(i+.5f)/n);var g=Box("Terrace glass railing",p+Vector3.up*.55f,new Vector3(.07f,1.05f,d.magnitude/n),"Station panoramic glass");
                g.transform.rotation=Quaternion.LookRotation(d);
            }
            var rail=Box("Terrace steel handrail",(a+b)/2+Vector3.up*1.12f,new Vector3(.08f,.07f,d.magnitude),"Steel");rail.transform.rotation=Quaternion.LookRotation(d);
            for(int i=0;i<=n;i++)Box("Terrace railing post",Vector3.Lerp(a,b,(float)i/n)+Vector3.up*.55f,new Vector3(.08f,1.12f,.08f),"Steel");
        }
        static void SecurityEntrance()
        {
            Wall("Main entrance south pier",59,75,59,78.2f,5,TerraceY);Wall("Main entrance north pier",59,81.8f,59,85,5,TerraceY);
            Box("Main lab entrance lintel",new Vector3(59,TerraceY+3.85f,80),new Vector3(.35f,2.3f,3.6f),"StationWall");
            var root=Group("Security-controlled main lab door",parent);root.position=new Vector3(59,TerraceY+1.35f,80);root.rotation=Quaternion.Euler(0,90,0);
            var gate=root.gameObject.AddComponent<LabSecurityGate>();
            for(int i=0;i<2;i++)
            {
                var leaf=Group(i==0?"Left sliding leaf":"Right sliding leaf",root);leaf.localPosition=new Vector3(i==0?-.9f:.9f,0,0);
                var panel=Box("Lab door panel",leaf.position,new Vector3(1.8f,2.7f,.2f),"Steel",true,leaf,true);panel.transform.localRotation=Quaternion.identity;
                var stripe=Box("Lab door warning strip",leaf.TransformPoint(new Vector3(0,-.7f,-.12f)),new Vector3(1.65f,.13f,.03f),"Amber",false,leaf,true);stripe.transform.localRotation=Quaternion.identity;
                if(i==0)gate.left=leaf;else gate.right=leaf;
            }
            Sign("LAB ACCESS / SECURITY RELEASE",new Vector3(58.7f,TerraceY+3.25f,80),8,90);
            var console=Group("Main door security control",parent);console.position=new Vector3(63.5f,TerraceY,71.8f);console.rotation=Quaternion.Euler(0,90,0);
            var cabinet=Box("Access console pedestal",console.TransformPoint(new Vector3(0,.65f,0)),new Vector3(1.2f,1.3f,.7f),"Steel",true,console);cabinet.transform.localRotation=Quaternion.identity;
            var screen=Box("Access console screen",console.TransformPoint(new Vector3(0,1.5f,0)),new Vector3(1.5f,.8f,.16f),"Glass",true,console);screen.transform.localRotation=Quaternion.identity;
            var status=new GameObject("Control status").AddComponent<TextMeshPro>();status.transform.SetParent(console,false);status.transform.localPosition=new Vector3(0,1.5f,-.09f);status.rectTransform.sizeDelta=new Vector2(1.4f,.75f);status.font=TMP_Settings.defaultFontAsset;status.fontSize=2.4f;status.alignment=TextAlignmentOptions.Center;status.text="LAB ACCESS\nRELEASE DOOR";status.color=new Color(1,.72f,.24f);
            var control=console.gameObject.AddComponent<LabSecurityPanel>();control.player=player;control.gate=gate;control.status=status;
        }
        static void StreetRevision()
        {
            foreach(var t in All().Where(t=>(t.name=="Overlapping parking sheet"||t.name=="Sheet barricade support")&&Mathf.Abs(t.position.z-34)<.05f).ToArray())t.position+=Vector3.forward*8;
            GameObject.Find("Red rectangle / parking entrance collapse").transform.position+=Vector3.forward*8;
            RemoveNamed("Parking apron");Floor("Parking apron",40,106,0,42,.04f,"Asphalt");
            SheetRun(40,34,40,42);SheetRun(106,34,106,42);
            foreach(var t in All().Where(t=>(t.name=="Overlapping parking sheet"||t.name=="Sheet barricade support")&&Mathf.Abs(t.position.x-106)<.05f&&t.position.z>11&&t.position.z<26).ToArray())Object.DestroyImmediate(t.gameObject);
            foreach(float z in new[]{11.3f,26f})Box("Ship 1 parking gatepost",new Vector3(106,2,z),new Vector3(.4f,4,.4f),"Steel");
            Sign("PARKING / SHIP 2",new Vector3(106.3f,3,29),9,270);
            foreach(var t in All().Where(t=>t.name=="East-west road dash"))t.position+=Vector3.forward*5;
            // Remove the bollard inside the new lower flight.
            foreach(var t in All().Where(t=>t.name=="Frontage bollard"&&t.position.x>47&&t.position.x<60).ToArray())Object.DestroyImmediate(t.gameObject);
            RemoveNamed("Abandoned delivery truck");DetailedTruck(new Vector3(75,0,55));
        }
        static void DetailedTruck(Vector3 p)
        {
            var saved=parent;parent=Group("Heavy research delivery truck",parent);
            Box("Truck chassis",p+new Vector3(0,.8f,0),new Vector3(14,.4f,3.3f),"Steel");
            Box("Cargo van body",p+new Vector3(-1.7f,2.6f,0),new Vector3(10.3f,3.4f,3.6f),"SheetMetal");
            Box("Reinforced cargo roof",p+new Vector3(-1.7f,4.33f,0),new Vector3(10.4f,.12f,3.7f),"Steel");
            Box("Truck cab",p+new Vector3(5.2f,1.85f,0),new Vector3(3.5f,2.5f,3.5f),"PaintBlue");
            Box("Cab roof",p+new Vector3(5,3.2f,0),new Vector3(3.2f,.2f,3.6f),"PaintBlue");
            Box("Front windshield",p+new Vector3(6.97f,2.55f,0),new Vector3(.06f,1,3.06f),"Glass");
            Box("Windshield centre seal",p+new Vector3(7.01f,2.55f,0),new Vector3(.06f,1.05f,.1f),"Rubber");
            Box("Front bumper",p+new Vector3(7.15f,.8f,0),new Vector3(.3f,.35f,3.65f),"Steel");
            Box("Radiator grille",p+new Vector3(7.01f,1.45f,0),new Vector3(.08f,.65f,1.9f),"Rubber");
            for(int i=0;i<5;i++)Box("Grille slat",p+new Vector3(7.07f,1.2f+i*.12f,0),new Vector3(.05f,.035f,1.8f),"Steel",false);
            foreach(float side in new[]{-1f,1f})
            {
                Box("Truck side window",p+new Vector3(5.35f,2.5f,side*1.77f),new Vector3(2.3f,1,.06f),"Glass");
                Box("Cab door handle",p+new Vector3(4.4f,1.9f,side*1.82f),new Vector3(.35f,.08f,.07f),"Steel",false);
                Box("Cab step",p+new Vector3(4.5f,.6f,side*1.9f),new Vector3(1.1f,.15f,.45f),"Steel");
                Box("Mirror arm",p+new Vector3(6.2f,2.5f,side*1.97f),new Vector3(.12f,.12f,.5f),"Steel",false);
                Box("Wing mirror",p+new Vector3(6.2f,2.6f,side*2.2f),new Vector3(.15f,.65f,.35f),"Steel",false);
                Box("Truck headlamp",p+new Vector3(7.06f,1.4f,side*1.35f),new Vector3(.08f,.4f,.5f),"LampWarm",false);
                Box("Cargo reflective rail",p+new Vector3(-1.7f,1.25f,side*1.82f),new Vector3(10,.12f,.04f),"Amber",false);
                for(int i=0;i<9;i++)Box("Cargo reinforcing rib",p+new Vector3(-6.5f+i*1.15f,2.6f,side*1.84f),new Vector3(.07f,3.2f,.07f),"Steel",false);
                foreach(float x in new[]{-5f,-2.8f,5.4f})
                {
                    var w=GameObject.CreatePrimitive(PrimitiveType.Cylinder);w.name="Truck tyre";w.transform.SetParent(parent,false);w.transform.position=p+new Vector3(x,.72f,side*1.77f);w.transform.rotation=Quaternion.Euler(90,0,0);w.transform.localScale=new Vector3(1.45f,.27f,1.45f);w.GetComponent<Renderer>().sharedMaterial=mats["Rubber"];w.isStatic=true;
                    var hub=Object.Instantiate(w,parent);hub.name="Truck wheel hub";hub.transform.position+=new Vector3(0,0,side*.28f);hub.transform.localScale=new Vector3(.75f,.035f,.75f);hub.GetComponent<Renderer>().sharedMaterial=mats["Steel"];Object.DestroyImmediate(hub.GetComponent<Collider>());
                }
            }
            foreach(float side in new[]{-1f,1f})Box("Rear cargo door",p+new Vector3(-6.9f,2.6f,side*.88f),new Vector3(.07f,3.1f,1.7f),"Steel");
            parent=saved;
        }
        static void ExtendTunnel()
        {
            RemoveNamed("Continuous tunnel track","Transit tunnel roof","Tunnel wall before exit");
            foreach(var t in All().Where(t=>t.name=="Transit tunnel side"&&t.position.z<20).ToArray())Object.DestroyImmediate(t.gameObject);
            Floor("Continuous tunnel track",-620,50,14,26,-13.5f,"Track ballast",GameObject.Find("Transit tunnel scenery").transform);
            Floor("Transit tunnel roof",-620,50,14,36,-6.4f,"Concrete",roofs);
            Wall("Transit tunnel side",-620,14,50,14,7,-13.5f,"Concrete");Wall("Tunnel wall before exit",-620,36,5.7f,36,7,-13.5f,"Concrete");
        }
        public static string ConnectHelicopterRoute()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
            var scenes=EditorBuildSettings.scenes.ToList();
            foreach(string path in new[]{VaccineBlockoutBuilder.ScenePath,ScenePath})
            {var s=scenes.FirstOrDefault(s=>s.path==path);if(s!=null)s.enabled=true;else scenes.Add(new EditorBuildSettingsScene(path,true));}
            EditorBuildSettings.scenes=scenes.ToArray();
            EditorSceneManager.OpenScene(VaccineBlockoutBuilder.ScenePath);
            var p=Object.FindAnyObjectByType<VaccineWalkthrough>();var b=p.GetComponent<VaccineBoardingTransfer>();if(!b)b=p.gameObject.AddComponent<VaccineBoardingTransfer>();b.player=p;b.train=GameObject.Find("Platform 1 wide connected train").transform;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return "Helicopter scene boards into four-stop route; both Vaccine scenes enabled in build settings.";
        }
    }
}
