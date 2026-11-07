using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using ShipEnemies;
using ShipHUD;
using TMPro;

namespace Onslaught.Mission.Editor
{
    public static class MissionBuilder
    {
        public const string Root="Assets/Mission",ScenePath=Root+"/Scenes/Onslaught_Mission.unity";
        static Vector3 P(float x,float y,float height=0)=>new Vector3((x-445)*.091f,height,(286-y)*.091f);
        [MenuItem("Onslaught/Play mission/Open Onslaught mission")]
        public static void Open(){if(Application.isPlaying)return;if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
        [MenuItem("Onslaught/Play mission/Create integrated mission")]
        public static void Build()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Stop Play and save the current scene first.");
            if(File.Exists(ScenePath))throw new System.InvalidOperationException("Mission exists; edit it in place.");
            MissionArt.Prepare();
            var source=EditorSceneManager.OpenScene("Assets/HUD/Scenes/HUDPreview.unity");EditorSceneManager.SaveScene(source,ScenePath,true);var scene=EditorSceneManager.OpenScene(ScenePath);
            foreach(string name in new[]{"HUD display preview controls","HUD preview contacts - display motion only"}){var go=GameObject.Find(name);if(go!=null)Object.DestroyImmediate(go);}
            var ship=new GameObject("Mantis ship - flight root").transform;
            foreach(var go in scene.GetRootGameObjects())
                if(go!=ship.gameObject&&go.name!="Ship HUD"&&!go.name.Contains("instructions")&&!go.name.Contains("HUD objective"))go.transform.SetParent(ship,true);
            var hud=Object.FindAnyObjectByType<ShipHud>();var explorer=Object.FindAnyObjectByType<ExplorerController>();
            explorer.gates=new ShipDoor[0];explorer.allowPositionReset=false;explorer.handlesEscape=false;explorer.gameObject.layer=0;
            explorer.eye.nearClipPlane=.025f;explorer.interactionPrompt.text="";
            explorer.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            var mission=new GameObject("Onslaught mission director").AddComponent<MissionDirector>();mission.shipRoot=ship;mission.explorer=explorer;mission.hud=hud;
            var tuning=ScriptableObject.CreateInstance<MissionTuning>();AssetDatabase.CreateAsset(tuning,Root+"/Data/OnslaughtBalance.asset");mission.tuning=tuning;
            mission.player=explorer.GetComponent<Vitality>();mission.player.maximum=tuning.playerHealth;mission.player.receivesEnemyAttacks=true;mission.player.aimHeight=1.1f;hud.playerHealth=mission.player;hud.externalClock=true;
            var enemies=new GameObject("Active ramp attackers").transform;enemies.SetParent(ship,false);mission.enemiesRoot=enemies;
            mission.enemyPrefabs=CreateEnemyVariants();
            mission.gates=CreateGates(tuning);hud.radar.doors=Object.FindObjectsByType<ShipDoor>().Where(d=>d.automatic).ToArray();hud.radar.barricades=mission.gates;
            mission.spawnPoints=new Transform[2];
            for(int i=0;i<2;i++){var p=new GameObject(i==0?"Upper ramp spawn":"Lower ramp spawn").transform;p.SetParent(ship,false);p.position=P(778,i==0?228:346,-1);mission.spawnPoints[i]=p;}
            mission.weapons=CreateWeapons(explorer,hud,mission.player);CreateHud(mission);CreateAudio(mission);CreateDeparture(mission);
            mission.ammoPickup=Pickup("Ammo supply",false,MissionArt.accent);mission.medicalPickup=Pickup("Medical supply",true,MissionArt.glove);
            BakeNavigation(ship);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=mission.gameObject;
            Debug.Log("Onslaught mission created: two weapons, destructible barred gates, four navigation sizes, timed defense/boss/clearance/launch flow and voiced intercom.");
        }
        static EnemyBrain[] CreateEnemyVariants()
        {
            var names=new[]{"Shambler","Sprinter","Spitter","Bloater","Regurgitator","Worm","PukeWorm"};var definitions=new EnemyDefinition[7];var prefabs=new EnemyBrain[7];
            for(int i=0;i<7;i++)
            {
                var d=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemies/Definitions/"+names[i]+".asset"));d.name=names[i]+" mission balance";
                d.health=new[]{100f,70,140,320,3600,20,35}[i];d.speed=new[]{1.8f,4.6f,2.4f,1.2f,1.8f,2.8f,3.1f}[i];
                d.meleeDamage=new[]{8f,7,6,20,30,3,5}[i];d.meleeCooldown=new[]{1.25f,1.05f,1.4f,1.9f,2.2f,1.05f,1.15f}[i];
                d.acidImpactDamage=i==4?14:7;d.acidDamagePerSecond=i==4?8:4;d.acidLifetime=i==4?4:3;d.rangedCooldown=i==4?9:4.5f;
                d.deathParasites=i==3?2:0;d.vomitParasites=i==4?2:0;definitions[i]=d;AssetDatabase.CreateAsset(d,Root+"/Data/"+names[i]+".asset");
            }
            for(int i=6;i>=0;i--)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Prefabs/"+names[i]+".prefab"));go.name=names[i];
                var brain=go.GetComponent<EnemyBrain>();brain.definition=definitions[i];brain.displayOnly=false;go.GetComponent<Vitality>().maximum=definitions[i].health;
                go.AddComponent<MissionEnemy>();PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
                prefabs[i]=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+names[i]+".prefab").GetComponent<EnemyBrain>();Object.DestroyImmediate(go);
            }
            definitions[3].parasitePrefab=prefabs[5];definitions[4].parasitePrefab=prefabs[6];EditorUtility.SetDirty(definitions[3]);EditorUtility.SetDirty(definitions[4]);return prefabs;
        }
        static DefenseGate[] CreateGates(MissionTuning tuning)
        {
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=settings.FindProperty("layers");
            if(!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(8).stringValue))throw new System.InvalidOperationException("Expected free layer 8");layers.GetArrayElementAtIndex(8).stringValue="GateMovementBarrier";settings.ApplyModifiedPropertiesWithoutUndo();
            var old=Object.FindObjectsByType<ShipDoor>().Where(d=>!d.automatic).OrderByDescending(d=>d.transform.position.z).ToArray();var result=new DefenseGate[2];
            for(int i=0;i<old.Length;i++)
            {
                var door=old[i];door.enabled=false;door.panel.gameObject.SetActive(false);door.gameObject.layer=8;
                var gate=door.gameObject.AddComponent<DefenseGate>();gate.displayName=i==0?"PORT GATE":"STARBOARD GATE";gate.width=door.width;gate.height=door.height;
                gate.barrier=door.barrier;gate.obstacle=door.obstacle;gate.obstacle.carving=true;gate.GetComponent<Vitality>().maximum=tuning.gateHealth;
                var bars=new GameObject("Prison bar barrier").transform;bars.SetParent(door.transform,false);gate.bars=bars;
                int count=Mathf.RoundToInt((door.width-.2f)/.48f);
                for(int b=0;b<=count;b++)MissionArt.Box("Vertical steel bar",bars,new Vector3(Mathf.Lerp(-door.width*.5f+.12f,door.width*.5f-.12f,(float)b/count),door.height*.5f,0),new Vector3(.085f,door.height,.085f),MissionArt.steel,true);
                foreach(float y in new[]{.12f,1.1f,door.height-.12f})MissionArt.Box("Cross rail",bars,new Vector3(0,y,0),new Vector3(door.width,.12f,.14f),MissionArt.dark,true);
                gate.blastShutter=MissionArt.Box("Departure blast shutter",door.transform,new Vector3(0,door.height*1.5f+.3f,0),new Vector3(door.width,door.height,.22f),MissionArt.dark).transform;gate.blastShutter.gameObject.SetActive(false);
                door.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
                gate.audioSource=door.gameObject.AddComponent<AudioSource>();gate.audioSource.spatialBlend=1;gate.audioSource.minDistance=4;gate.audioSource.maxDistance=40;gate.audioSource.playOnAwake=false;gate.clang=MissionArt.Clip("GateClang");gate.collapse=MissionArt.Clip("GateCollapse");
                result[i]=gate;
                // A continuous threshold overlaps the traced deck/ramp seam so every agent size can cross it.
                MissionArt.Box("Boarding threshold plate",door.transform.parent,new Vector3(door.transform.position.x,-.12f,door.transform.position.z),new Vector3(1.6f,.24f,door.width),MissionArt.steel,true);
            }
            foreach(var door in old)Object.DestroyImmediate(door);
            foreach(var door in Object.FindObjectsByType<ShipDoor>())door.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            return result;
        }
        static PlayerWeapons CreateWeapons(ExplorerController explorer,ShipHud hud,Vitality health)
        {
            var weapons=explorer.gameObject.AddComponent<PlayerWeapons>();weapons.explorer=explorer;weapons.hud=hud;weapons.health=health;
            weapons.specifications=new WeaponSpec[2];weapons.models=new GameObject[2];weapons.slides=new Transform[2];weapons.magazines=new Transform[2];weapons.muzzles=new Transform[2];
            var mount=new GameObject("First person weapon mount").transform;mount.SetParent(explorer.eye.transform,false);mount.localPosition=new Vector3(.22f,-.25f,.4f);weapons.mount=mount;
            for(int i=0;i<2;i++)
            {
                var d=ScriptableObject.CreateInstance<WeaponSpec>();d.displayName=i==0?"P-34 SERVICE PISTOL":"AR-24 SERVICE RIFLE";d.automatic=i==1;d.damage=i==0?34:24;d.magazineSize=i==0?12:30;d.startingReserve=i==0?120:540;d.maximumReserve=i==0?180:720;d.shotsPerSecond=i==0?4:8;d.reloadSeconds=i==0?1.35f:2;d.recoilDegrees=i==0?1.3f:.8f;d.shot=MissionArt.Clip(i==0?"PistolShot":"RifleShot");d.reload=MissionArt.Clip("Reload");
                AssetDatabase.CreateAsset(d,Root+"/Data/"+(i==0?"Pistol":"Rifle")+".asset");weapons.specifications[i]=d;
                weapons.models[i]=MissionArt.Weapon(mount,i==1,out weapons.slides[i],out weapons.magazines[i],out weapons.muzzles[i]);weapons.models[i].SetActive(i==1);
            }
            weapons.flash=MissionArt.Box("Brief muzzle flash",explorer.eye.transform,Vector3.zero,new Vector3(.08f,.08f,.14f),MissionArt.flash);weapons.flash.SetActive(false);weapons.flash.layer=2;
            weapons.impact=GameObject.CreatePrimitive(PrimitiveType.Sphere);weapons.impact.name="Reused shot impact";Object.DestroyImmediate(weapons.impact.GetComponent<Collider>());weapons.impact.GetComponent<Renderer>().sharedMaterial=MissionArt.flash;weapons.impact.SetActive(false);
            var line=new GameObject("Reused shot tracer").AddComponent<LineRenderer>();line.sharedMaterial=MissionArt.flash;line.positionCount=2;line.startWidth=.009f;line.endWidth=.003f;line.enabled=false;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;weapons.tracer=line;
            weapons.audioSource=explorer.eye.gameObject.AddComponent<AudioSource>();weapons.audioSource.playOnAwake=false;weapons.audioSource.volume=.42f;weapons.audioSource.spatialBlend=0;
            AddWeaponFill(explorer.eye.transform);
            return weapons;
        }
        public static void AddWeaponFill(Transform eye)
        {
            if(eye.Find("Weapon soft fill")!=null)return;
            var light=new GameObject("Weapon soft fill").AddComponent<Light>();light.transform.SetParent(eye,false);light.transform.localPosition=new Vector3(-.2f,.15f,.2f);
            light.type=LightType.Point;light.range=2.5f;light.intensity=.9f;light.color=new Color(.76f,.85f,1);light.shadows=LightShadows.None;light.cullingMask=1<<2;
        }
        static void BakeNavigation(Transform ship)
        {
            var doors=Object.FindObjectsByType<NavMeshObstacle>();foreach(var obstacle in doors)obstacle.enabled=false;
            foreach(string name in new[]{"Enemy standard","Enemy heavy","Enemy boss","Enemy parasite"})
            {
                int id=Enumerable.Range(0,NavMesh.GetSettingsCount()).Select(i=>NavMesh.GetSettingsByIndex(i).agentTypeID).First(i=>NavMesh.GetSettingsNameFromID(i)==name);
                // Include the gate layer so its ignore-from-build modifier also removes the child bar colliders.
                var surface=ship.gameObject.AddComponent<NavMeshSurface>();surface.agentTypeID=id;surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.layerMask=~(1<<2);surface.overrideVoxelSize=true;surface.voxelSize=Mathf.Max(.075f,NavMesh.GetSettingsByID(id).agentRadius/3);surface.BuildNavMesh();
                if(surface.navMeshData==null)throw new System.InvalidOperationException("NavMesh bake failed for "+name);
                AssetDatabase.CreateAsset(surface.navMeshData,Root+"/Navigation/"+name.Replace(" ","")+".asset");
            }
            foreach(var obstacle in doors)obstacle.enabled=true;
        }
        static GameObject Pickup(string name,bool medical,Material material)
        {
            var go=new GameObject(name);var pickup=go.AddComponent<SupplyPickup>();pickup.medical=medical;
            MissionArt.Box("Supply case",go.transform,Vector3.zero,new Vector3(.5f,.28f,.38f),material);
            MissionArt.Box("Supply stripe",go.transform,new Vector3(0,.15f,0),new Vector3(.4f,.02f,.08f),MissionArt.steel);
            if(medical)MissionArt.Box("Medical cross",go.transform,new Vector3(0,.16f,0),new Vector3(.08f,.02f,.28f),MissionArt.steel);
            var result=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+(medical?"Medkit":"Ammo")+".prefab");Object.DestroyImmediate(go);return result;
        }
        static void CreateAudio(MissionDirector m)
        {
            m.intercom=m.gameObject.AddComponent<AudioSource>();m.intercom.playOnAwake=false;m.intercom.volume=.75f;
            m.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency=3500;m.gameObject.AddComponent<AudioHighPassFilter>().cutoffFrequency=400;
            m.intro=MissionArt.Clip("Intro");m.bossWarning=MissionArt.Clip("BossWarning");m.clearMessage=MissionArt.Clip("ClearDeck");m.launchMessage=MissionArt.Clip("Departure");m.alertTone=MissionArt.Clip("Alert");
            var engine=new GameObject("Departure engine audio");m.engines=engine.AddComponent<AudioSource>();m.engines.clip=MissionArt.Clip("Engine");m.engines.loop=true;m.engines.playOnAwake=false;m.engines.volume=.1f;
        }
        static void CreateHud(MissionDirector m)
        {
            var safe=m.hud.transform.Find("Safe area");m.hud.transform.Find("Safe area/Preview caption").GetComponent<TMP_Text>().text="F1 CONTROLS   /   ESC PAUSE";
            m.hud.helpPanel.transform.Find("Movement keys").GetComponent<TMP_Text>().text="WASD MOVE      LMB FIRE   RMB AIM   R RELOAD   E SWITCH   TAB MAP";
            var gates=Panel(safe,"Gate integrity",new Vector2(0,1),new Vector2(24,-330),new Vector2(330,87));
            for(int i=0;i<2;i++)
            {
                m.gates[i].status=Label(gates,"Gate "+i,15,10+i*37,300,20,12);m.gates[i].status.text=m.gates[i].displayName+"  /  420 HP";
                var r=Rect(gates,"Gate bar "+i,new Vector2(0,1),new Vector2(15,-34-i*37),new Vector2(300,4));var bar=r.gameObject.AddComponent<HudShape>();bar.style=HudShape.Style.Bar;bar.color=HudDrawing.Amber;bar.segments=20;bar.raycastTarget=false;m.gates[i].healthBar=bar;
            }
            m.remainingText=Label(m.hud.transform.Find("Safe area/Mission"),"Hostiles remaining",10,151,300,22,12);m.remainingText.alignment=TextAlignmentOptions.Right;
            var intercom=Panel(safe,"Intercom subtitles",new Vector2(.5f,0),new Vector2(0,210),new Vector2(760,90));m.subtitlePanel=intercom.gameObject;
            m.subtitle=Label(intercom,"Message",18,9,724,74,17);m.subtitle.textWrappingMode=TextWrappingModes.Normal;m.subtitle.alignment=TextAlignmentOptions.MidlineLeft;
            m.notification=Label(safe,"Pickup notice",0,0,700,28,16);var n=m.notification.rectTransform;n.anchorMin=n.anchorMax=n.pivot=new Vector2(.5f,.5f);n.anchoredPosition=new Vector2(0,-90);m.notification.alignment=TextAlignmentOptions.Center;m.notification.color=HudDrawing.Amber;
            var hit=Rect(safe,"Hit marker",new Vector2(.5f,.5f),Vector2.zero,new Vector2(36,36));hit.localRotation=Quaternion.Euler(0,0,45);var hitShape=hit.gameObject.AddComponent<HudShape>();hitShape.style=HudShape.Style.Crosshair;hitShape.color=HudDrawing.Paper;hitShape.raycastTarget=false;m.weapons.hitGraphic=hitShape;m.weapons.hitMarker=hit.gameObject.AddComponent<CanvasGroup>();m.weapons.hitMarker.alpha=0;m.weapons.hitMarker.blocksRaycasts=false;
            var fade=Rect(safe,"Launch fade",Vector2.zero,Vector2.zero,Vector2.zero);fade.anchorMin=Vector2.zero;fade.anchorMax=Vector2.one;fade.offsetMin=fade.offsetMax=Vector2.zero;var black=fade.gameObject.AddComponent<UnityEngine.UI.Image>();black.color=Color.black;black.raycastTarget=false;m.fade=fade.gameObject.AddComponent<CanvasGroup>();m.fade.alpha=0;m.fade.blocksRaycasts=false;
            var end=Panel(safe,"Mission result",new Vector2(.5f,.5f),Vector2.zero,new Vector2(620,220));m.endPanel=end.gameObject;m.endTitle=Label(end,"Result title",24,30,572,50,34);m.endTitle.alignment=TextAlignmentOptions.Center;m.endTitle.color=HudDrawing.Amber;
            m.endDetail=Label(end,"Result detail",24,92,572,100,19);m.endDetail.textWrappingMode=TextWrappingModes.Normal;m.endDetail.alignment=TextAlignmentOptions.Center;end.gameObject.SetActive(false);
        }
        static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=position;r.sizeDelta=size;return r;}
        static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
        {var r=Rect(parent,name,anchor,position,size);var p=r.gameObject.AddComponent<HudShape>();p.style=HudShape.Style.Panel;p.color=HudDrawing.Ink;p.raycastTarget=false;return r;}
        static TMP_Text Label(Transform parent,string name,float x,float y,float w,float h,int size)
        {var r=Rect(parent,name,new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h));var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.fontSize=size;t.color=HudDrawing.Paper;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Truncate;t.alignment=TextAlignmentOptions.MidlineLeft;return t;}
        static void CreateDeparture(MissionDirector m)
        {
            var camera=new GameObject("Departure exterior camera").AddComponent<Camera>();camera.transform.position=new Vector3(66,54,-59);camera.transform.LookAt(new Vector3(-4,0,0));camera.fieldOfView=53;camera.farClipPlane=500;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.008f,.015f,.025f);camera.enabled=false;m.departureCamera=camera;
            m.retractingRamps=new[]{"Upper boarding ramp","Lower boarding ramp","Open strip between ramps","Upper outside ramp edge","Lower outside ramp edge","End of map low boundary"}.Select(n=>GameObject.Find(n).transform).ToArray();
            var backdrop=new GameObject("Departure dock backdrop - visual only");m.cinematicBackdrop=backdrop;
            MissionArt.Box("Dock",backdrop.transform,new Vector3(48,-3,0),new Vector3(25,2,42),MissionArt.dark);
            foreach(float z in new[]{-20f,20f}){MissionArt.Box("Dock tower",backdrop.transform,new Vector3(43,5,z),new Vector3(3,18,3),MissionArt.steel);MissionArt.Box("Dock beacon",backdrop.transform,new Vector3(42,15,z),new Vector3(1,.4f,1),MissionArt.flash);}
            backdrop.SetActive(false);
            foreach(var t in m.shipRoot.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
        }
    }
}
