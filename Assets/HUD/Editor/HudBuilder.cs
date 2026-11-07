using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Onslaught;
using ShipEnemies;

namespace ShipHUD.Editor
{
    public static class HudBuilder
    {
        public const string Root="Assets/HUD",ScenePath=Root+"/Scenes/HUDPreview.unity";
        static TMP_FontAsset font;
        static Vector3 P(float x,float y,float height=.02f) => new Vector3((x-445)*.091f,height,(286-y)*.091f);
        [MenuItem("Onslaught/HUD/Open HUD preview")]
        public static void OpenPreview()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play before switching scenes.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Onslaught/HUD/Create HUD preview")]
        public static void Build()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play before building.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save the open scene first.");
            if(File.Exists(ScenePath))throw new System.InvalidOperationException("HUD preview already exists; edit it in place.");
            foreach(string dir in new[]{"Scenes","Prefabs","Data"})Directory.CreateDirectory(Root+"/"+dir);
            AssetDatabase.Refresh();font=TMP_Settings.defaultFontAsset;
            if(font==null)throw new System.InvalidOperationException("TMP font missing.");
            var original=EditorSceneManager.OpenScene("Assets/Onslaught/Scenes/Onslaught_Blockout.unity");
            EditorSceneManager.SaveScene(original,ScenePath,true);
            var scene=EditorSceneManager.OpenScene(ScenePath);
            HudMapData map=BakeMap();
            var hud=BuildHud(map);
            // Prefab has no cross-scene references or preview state driver.
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject,Root+"/Prefabs/ShipHUD.prefab");
            hud.waveText.text="HUD PREVIEW  /  NO ACTIVE WAVE";
            hud.transform.Find("Safe area/Preview caption").GetComponent<TMP_Text>().text="HUD PREVIEW    F1 HELP    F6 DAMAGE    F7 AMMO / RELOAD    F8 BOSS    F9 RESTORE";
            var explorer=Object.FindAnyObjectByType<ExplorerController>();hud.explorer=explorer;
            hud.playerHealth=explorer.gameObject.AddComponent<Vitality>();hud.playerHealth.maximum=100;hud.playerHealth.receivesEnemyAttacks=false;
            hud.radar.player=explorer.transform;hud.radar.doors=Object.FindObjectsByType<ShipDoor>();
            var objective=new GameObject("HUD objective - boarding approach").transform;objective.position=P(610,286);hud.radar.objective=objective;
            // Rebind the existing gate prompt before disabling the old walkthrough labels in this copy.
            explorer.interactionPrompt=hud.transform.Find("Safe area/Interaction prompt").GetComponent<TMP_Text>();
            GameObject.Find("Walkthrough instructions").SetActive(false);
            var actors=new GameObject("HUD preview contacts - display motion only").transform;
            Actor("Shambler",actors,300,286,390,286,.8f);
            Actor("Sprinter",actors,300,425,430,425,2.2f);
            Actor("Spitter",actors,300,145,415,145,.7f);
            Actor("Bloater",actors,535,345,585,345,.55f);
            var boss=Actor("Regurgitator",actors,565,230,565,230,0);boss.transform.rotation=Quaternion.Euler(0,204,0);boss.GetComponent<HudPreviewActor>().enabled=false;boss.gameObject.SetActive(false);
            var preview=new GameObject("HUD display preview controls").AddComponent<HudPreview>();preview.hud=hud;preview.boss=boss;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Selection.activeGameObject=hud.gameObject;
            Debug.Log("HUD preview created with real ship geometry, four moving contacts, optional boss, reusable HUD prefab and Input System preview controls.");
        }
        static EnemyBrain Actor(string name,Transform parent,float ax,float ay,float bx,float by,float speed)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Prefabs/"+name+".prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.name=name+" - HUD preview";go.transform.position=P(ax,ay);
            var brain=go.GetComponent<EnemyBrain>();brain.displayOnly=true;go.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled=false;
            foreach(var collider in go.GetComponentsInChildren<Collider>())collider.enabled=false;
            var contact=go.AddComponent<MinimapContact>();contact.health=go.GetComponent<Vitality>();contact.boss=name=="Regurgitator";
            var move=go.AddComponent<HudPreviewActor>();move.start=P(ax,ay);move.end=P(bx,by);move.speed=speed;move.brain=brain;
            PrefabUtility.RecordPrefabInstancePropertyModifications(brain);return brain;
        }
        public static HudMapData BakeMap()
        {
            var map=ScriptableObject.CreateInstance<HudMapData>();
            var vertices=new List<Vector2>();var triangles=new List<int>();var walls=new List<Vector2>();
            foreach(string name in new[]{"Main ship deck","Rounded forward jumpseat deck","Upper boarding ramp","Lower boarding ramp","Open strip between ramps"})
            {
                var filter=GameObject.Find(name).GetComponent<MeshFilter>();var mesh=filter.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;
                for(int i=0;i<t.Length;i+=3)
                {
                    Vector3 a=filter.transform.TransformPoint(v[t[i]]),b=filter.transform.TransformPoint(v[t[i+1]]),c=filter.transform.TransformPoint(v[t[i+2]]);
                    if(Vector3.Cross(b-a,c-a).normalized.y<.6f)continue;
                    int n=vertices.Count;vertices.Add(new Vector2(a.x,a.z));vertices.Add(new Vector2(b.x,b.z));vertices.Add(new Vector2(c.x,c.z));
                    triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);
                }
            }
            foreach(var box in Object.FindObjectsByType<BoxCollider>())
            {
                if(box.GetComponentInParent<ShipDoor>()!=null||box.bounds.min.y>1.4f||box.bounds.max.y<.5f)continue;
                string n=box.name.ToLowerInvariant();
                if(!(n.Contains("hull wall")||n.Contains("partition")||n.Contains("bulkhead")||n.Contains("cargo room")||n.Contains("booth")||n.Contains("ramp edge")||n.Contains("jamb")||n.Contains("end of map")))continue;
                Vector3 s=box.size*.5f,c=box.center;
                foreach(Vector3 p in new[]{new Vector3(-s.x,0,-s.z),new Vector3(-s.x,0,s.z),new Vector3(s.x,0,s.z),new Vector3(s.x,0,-s.z)})
                {var w=box.transform.TransformPoint(c+p);walls.Add(new Vector2(w.x,w.z));}
            }
            map.floorVertices=vertices.ToArray();map.floorTriangles=triangles.ToArray();map.wallQuads=walls.ToArray();
            map.minimum=new Vector2(vertices.Min(p=>p.x)-1.4f,vertices.Min(p=>p.y)-1.4f);map.maximum=new Vector2(vertices.Max(p=>p.x)+1.4f,vertices.Max(p=>p.y)+1.4f);
            AssetDatabase.CreateAsset(map,Root+"/Data/OnslaughtMap.asset");return map;
        }
        static ShipHud BuildHud(HudMapData map)
        {
            var go=new GameObject("Ship HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            var hud=go.AddComponent<ShipHud>();
            var safe=Stretch(go.transform,"Safe area");safe.gameObject.AddComponent<HudSafeArea>();
            var radarPanel=Corner(safe,"Tactical radar",new Vector2(0,1),new Vector2(24,-24),new Vector2(330,240));Panel(radarPanel);
            Label(radarPanel,"Radar heading","MANTIS  /  TACTICAL",16,13,230,24,16,HudDrawing.Paper,FontStyles.Bold);
            Label(radarPanel,"North indicator","N",288,15,24,20,13,HudDrawing.Cyan,FontStyles.Bold).alignment=TextAlignmentOptions.Center;
            var floor=Element(radarPanel,"Deck plan",14,43,302,150).gameObject.AddComponent<HudMapGraphic>();floor.data=map;floor.raycastTarget=false;
            var contacts=Stretch(floor.transform,"Live contacts");contacts.gameObject.AddComponent<Canvas>();
            hud.radar=contacts.gameObject.AddComponent<HudRadar>();hud.radar.data=map;hud.radar.raycastTarget=false;
            hud.radar.countText=Label(radarPanel,"Contact count","00 CONTACTS",16,200,145,22,14,HudDrawing.Red,FontStyles.Bold);
            Label(radarPanel,"Radar scale","10 m",254,200,48,18,12,HudDrawing.Muted).alignment=TextAlignmentOptions.Right;
            var scale=Element(radarPanel,"10 metre scale",260,221,44.4f,1).gameObject.AddComponent<UnityEngine.UI.Image>();scale.color=HudDrawing.Muted;scale.raycastTarget=false;
            var legend=Corner(safe,"Radar legend",new Vector2(0,1),new Vector2(40,-270),new Vector2(310,22));
            Label(legend,"Legend","<color=#66EDE8>YOU</color>   <color=#FF4538>HOSTILE</color>   <color=#EFA847>GATE / OBJECTIVE</color>",0,0,310,22,12,HudDrawing.Muted);
            hud.zoneText=Label(legend,"Deck location","01  /  MAIN DECK",0,28,310,24,14,HudDrawing.Paper,FontStyles.Bold);

            var mission=Corner(safe,"Mission",new Vector2(1,1),new Vector2(-24,-24),new Vector2(310,112));Panel(mission);
            Label(mission,"Operation label","OPERATION  /  01",18,12,210,20,12,HudDrawing.Amber,FontStyles.Bold);
            hud.timerText=Label(mission,"Elapsed","00:00",223,10,68,23,17,HudDrawing.Muted);hud.timerText.alignment=TextAlignmentOptions.Right;
            hud.missionTitle=Label(mission,"Mission title","ONSLAUGHT",18,35,275,30,25,HudDrawing.Paper,FontStyles.Bold);
            hud.missionObjective=Label(mission,"Objective","Hold the boarding ramps",18,74,275,25,16,HudDrawing.Paper);
            hud.waveText=Label(mission,"Wave","AWAITING DEPLOYMENT",8,122,300,22,12,HudDrawing.Muted);hud.waveText.alignment=TextAlignmentOptions.Right;

            var vitals=Corner(safe,"Vitals",new Vector2(0,0),new Vector2(24,65),new Vector2(330,111));Panel(vitals);
            Shape(vitals,"Medical icon",14,17,28,28,HudShape.Style.Medical,HudDrawing.Green);
            Label(vitals,"Vital label","VITALS",53,14,106,22,13,HudDrawing.Muted,FontStyles.Bold);
            hud.healthStatus=Label(vitals,"Condition","STABLE",172,15,140,22,12,HudDrawing.Muted,FontStyles.Bold);hud.healthStatus.alignment=TextAlignmentOptions.Right;
            hud.healthNumber=Label(vitals,"Health number","100<size=17> / 100</size>",53,36,255,42,34,HudDrawing.Paper,FontStyles.Bold);
            hud.healthBar=Shape(vitals,"Health bar",17,86,296,8,HudShape.Style.Bar,HudDrawing.Green);hud.healthBar.segments=20;

            var ammo=Corner(safe,"Weapon",new Vector2(1,0),new Vector2(-24,65),new Vector2(288,132));Panel(ammo);
            hud.weaponName=Label(ammo,"Weapon name","UNARMED",16,13,255,22,15,HudDrawing.Paper,FontStyles.Bold);
            Shape(ammo,"Rifle schematic",17,40,88,37,HudShape.Style.Weapon,HudDrawing.Muted);
            hud.ammoNumber=Label(ammo,"Magazine","--",124,34,77,53,45,HudDrawing.Paper,FontStyles.Bold);hud.ammoNumber.alignment=TextAlignmentOptions.Right;
            hud.reserveNumber=Label(ammo,"Reserve","/ ---",207,58,65,25,18,HudDrawing.Muted);
            hud.ammoBar=Shape(ammo,"Magazine bar",17,91,254,5,HudShape.Style.Bar,HudDrawing.Paper);hud.ammoBar.segments=15;
            hud.weaponStatus=Label(ammo,"Ammo state","NO WEAPON EQUIPPED",17,105,254,19,12,HudDrawing.Muted);

            var utility=Corner(safe,"Equipment slots",new Vector2(.5f,0),new Vector2(0,65),new Vector2(214,71));
            for(int i=0;i<3;i++)
            {
                var slot=Element(utility,"Empty slot "+i,i*74,0,66,71);Panel(slot);
                Shape(slot,"Equipment symbol",18,9,30,30,i==0?HudShape.Style.Grenade:i==1?HudShape.Style.Medical:HudShape.Style.Skill,new Color(.4f,.47f,.46f));
                Label(slot,"Not equipped","EMPTY",7,47,52,17,10,HudDrawing.Muted).alignment=TextAlignmentOptions.Center;
            }
            Label(utility,"Loadout caption","EQUIPMENT",0,-23,214,18,11,HudDrawing.Muted).alignment=TextAlignmentOptions.Center;

            var center=Corner(safe,"Crosshair",new Vector2(.5f,.5f),Vector2.zero,new Vector2(32,32));
            var cross=center.gameObject.AddComponent<HudShape>();cross.style=HudShape.Style.Crosshair;cross.color=new Color(.9f,.94f,.9f,.8f);cross.raycastTarget=false;hud.crosshair=center.gameObject;
            var prompt=Corner(safe,"Interaction prompt",new Vector2(.5f,.5f),new Vector2(0,-65),new Vector2(600,30));
            var promptText=prompt.gameObject.AddComponent<TextMeshProUGUI>();Style(promptText,18,HudDrawing.Amber,FontStyles.Bold);promptText.alignment=TextAlignmentOptions.Center;
            hud.cursorHint=TextAt(safe,"Cursor hint",new Vector2(.5f,.5f),new Vector2(0,-110),new Vector2(600,26),"",16,HudDrawing.Paper);

            var reload=Corner(safe,"Reload",new Vector2(.5f,.5f),new Vector2(0,-155),new Vector2(180,31));
            Label(reload,"Reload title","RELOADING",0,0,180,20,13,HudDrawing.Amber,FontStyles.Bold).alignment=TextAlignmentOptions.Center;
            hud.reloadBar=Shape(reload,"Reload progress",0,28,180,3,HudShape.Style.Bar,HudDrawing.Amber);hud.reloadPanel=reload.gameObject;reload.gameObject.SetActive(false);

            var boss=Corner(safe,"Boss health",new Vector2(.5f,1),new Vector2(0,-28),new Vector2(440,68));Panel(boss);
            hud.bossName=Label(boss,"Boss name","REGURGITATOR",15,10,345,23,17,HudDrawing.Red,FontStyles.Bold);
            hud.bossNumber=Label(boss,"Boss percent","100%",354,12,70,22,14,HudDrawing.Paper);hud.bossNumber.alignment=TextAlignmentOptions.Right;
            hud.bossBar=Shape(boss,"Boss bar",16,44,408,8,HudShape.Style.Bar,HudDrawing.Red);hud.bossBar.segments=30;hud.bossPanel=boss.gameObject;boss.gameObject.SetActive(false);

            var help=Corner(safe,"Control help",new Vector2(.5f,0),new Vector2(0,24),new Vector2(760,25));Panel(help);
            Label(help,"Movement keys","WASD  MOVE          E  GATE     TAB  OVERHEAD     ESC  CURSOR",10,3,740,19,12,HudDrawing.Muted).alignment=TextAlignmentOptions.Center;hud.helpPanel=help.gameObject;
            TextAt(safe,"Preview caption",new Vector2(.5f,0),new Vector2(0,4),new Vector2(1000,18),"",11,HudDrawing.Amber);
            var damage=Stretch(safe,"Damage edge flash");hud.damageFlash=damage.gameObject.AddComponent<CanvasGroup>();hud.damageFlash.alpha=0;hud.damageFlash.blocksRaycasts=false;hud.damageFlash.interactable=false;
            foreach(var edge in new[]{new Vector2(0,0),new Vector2(1,0)})
            {
                var r=Corner(damage,"Damage edge",edge,Vector2.zero,new Vector2(5,900));r.anchorMin=new Vector2(edge.x,0);r.anchorMax=new Vector2(edge.x,1);r.sizeDelta=new Vector2(5,0);
                var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=HudDrawing.Red;image.raycastTarget=false;
            }
            return hud;
        }
        static RectTransform Stretch(Transform parent,string name)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        static RectTransform Corner(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=position;r.sizeDelta=size;return r;}
        static RectTransform Element(Transform parent,string name,float x,float y,float w,float h) => Corner(parent,name,new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h));
        static HudShape Panel(RectTransform r) {var s=r.gameObject.AddComponent<HudShape>();s.style=HudShape.Style.Panel;s.color=HudDrawing.Ink;s.raycastTarget=false;return s;}
        static HudShape Shape(Transform parent,string name,float x,float y,float w,float h,HudShape.Style style,Color color)
        {var s=Element(parent,name,x,y,w,h).gameObject.AddComponent<HudShape>();s.style=style;s.color=color;s.raycastTarget=false;return s;}
        static TMP_Text Label(Transform parent,string name,string text,float x,float y,float w,float h,int size,Color color,FontStyles style=FontStyles.Normal)
        {var t=Element(parent,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();Style(t,size,color,style);t.text=text;return t;}
        static TMP_Text TextAt(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,string text,int fontSize,Color color)
        {var t=Corner(parent,name,anchor,position,size).gameObject.AddComponent<TextMeshProUGUI>();Style(t,fontSize,color,FontStyles.Normal);t.alignment=TextAlignmentOptions.Center;t.text=text;return t;}
        static void Style(TMP_Text t,int size,Color color,FontStyles style)
        {t.font=font;t.fontSize=size;t.color=color;t.fontStyle=style;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Truncate;t.alignment=TextAlignmentOptions.MidlineLeft;}
    }
}
