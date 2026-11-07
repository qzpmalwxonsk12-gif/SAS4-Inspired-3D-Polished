using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using TMPro;
using ShipEnemies;
using ShipHUD;
using Onslaught.Mission;
using Object=UnityEngine.Object;

namespace Vaccine.Editor
{
    public static class VaccineMissionBuilder
    {
        public const string Root="Assets/Vaccine/Combat";
        static VaccineMission m;static int agentId;static Transform props;
        static T Load<T>(string p) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(p);
        static Transform Group(string n,Transform parent=null){var t=new GameObject(n).transform;t.SetParent(parent,false);return t;}
        static GameObject Box(string n,Vector3 p,Vector3 size,string material="Steel",Transform parent=null,bool collider=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(parent?parent:props,true);g.transform.position=p;g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=Load<Material>("Assets/Vaccine/Materials/Art/"+material+".mat");
            if(!collider)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static void Ignore(GameObject g){var modifier=g.GetComponent<NavMeshModifier>()??g.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;}
        static Transform Anchor(string name,Vector3 p,Transform parent=null){var a=Group(name,parent?parent:props);a.position=p;return a;}
        static void Save(){var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();}
        public static string Prepare()
        {
            foreach(string d in new[]{"Prefabs","Data","Navigation","Audio"})Directory.CreateDirectory(Root+"/"+d);AssetDatabase.Refresh();
            agentId=Enumerable.Range(0,NavMesh.GetSettingsCount()).Select(i=>NavMesh.GetSettingsByIndex(i).agentTypeID).First(id=>NavMesh.GetSettingsNameFromID(id)=="Enemy standard");
            var names=new[]{"Shambler","Sprinter","Spitter","Bloater","Regurgitator","Worm","PukeWorm"};
            var ds=new EnemyDefinition[7];var ps=new EnemyBrain[7];
            for(int i=0;i<7;i++)
            {
                string path=Root+"/Data/"+names[i]+".asset";ds[i]=Load<EnemyDefinition>(path);
                if(!ds[i]){ds[i]=Object.Instantiate(Load<EnemyDefinition>("Assets/Mission/Data/"+names[i]+".asset"));AssetDatabase.CreateAsset(ds[i],path);}
                var d=ds[i];d.health=new[]{85f,60,110,220,2200,20,30}[i];d.meleeDamage=new[]{6f,6,5,16,24,3,4}[i];d.acidImpactDamage=i==4?12:6;d.acidDamagePerSecond=i==4?6:3;
                // One traversal mesh keeps the large level affordable; personal avoidance radii remain distinct.
                d.radius=i==4?.8f:i==3?.5f:Mathf.Min(d.radius,.4f);EditorUtility.SetDirty(d);
            }
            for(int i=6;i>=0;i--)
            {
                var go=Object.Instantiate(Load<GameObject>("Assets/Mission/Prefabs/"+names[i]+".prefab"));go.name=names[i];
                var previous=go.GetComponent<MissionEnemy>();if(previous)Object.DestroyImmediate(previous);
                var b=go.GetComponent<EnemyBrain>();b.definition=ds[i];b.displayOnly=false;
                var a=go.GetComponent<NavMeshAgent>();a.agentTypeID=agentId;a.radius=ds[i].radius;a.obstacleAvoidanceType=ObstacleAvoidanceType.MedQualityObstacleAvoidance;a.avoidancePriority=30+i*8;
                go.AddComponent<VaccineEnemyActor>();
                ps[i]=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+names[i]+".prefab").GetComponent<EnemyBrain>();Object.DestroyImmediate(go);
            }
            ds[3].parasitePrefab=ps[5];ds[4].parasitePrefab=ps[6];EditorUtility.SetDirty(ds[3]);EditorUtility.SetDirty(ds[4]);AssetDatabase.SaveAssets();return "Seven Vaccine enemy variants prepared.";
        }
        public static string Surface()
        {
            EditorSceneManager.OpenScene("Assets/Vaccine/Scenes/Vaccine_Blockout.unity");
            if(Object.FindAnyObjectByType<VaccineMission>())throw new InvalidOperationException("Surface mission already installed.");
            while(Object.FindAnyObjectByType<VaccineStaticArtCache>())VaccineArtBatcher.Restore();
            Common(false);SurfaceObjects();Doors();Navigation(false);MapData();VaccineArtBatcher.Bake();Save();Vaccine.EditorTools.VaccineEncounterBuilder.ConfigureCurrent();
            return Report();
        }
        public static string District()
        {
            EditorSceneManager.OpenScene(LabDistrictBuilder.ScenePath);
            if(Object.FindAnyObjectByType<VaccineMission>())throw new InvalidOperationException("District mission already installed.");
            LabDistrictBuilder.CombatGeometry();Common(true);DistrictObjects();Doors();Navigation(true);MapData();LabDistrictBuilder.Bake();Save();Vaccine.EditorTools.VaccineEncounterBuilder.ConfigureCurrent();return Report();
        }
        static void Common(bool district)
        {
            agentId=Load<GameObject>(Root+"/Prefabs/Shambler.prefab").GetComponent<NavMeshAgent>().agentTypeID;
            props=Group("Vaccine mission objects");m=props.gameObject.AddComponent<VaccineMission>();m.labDistrict=district;
            m.player=Object.FindAnyObjectByType<VaccineWalkthrough>();m.player.missionMode=true;
            Ignore(m.player.gameObject);m.health=m.player.GetComponent<Vitality>()??m.player.gameObject.AddComponent<Vitality>();m.health.maximum=160;m.health.receivesEnemyAttacks=true;
            m.enemiesRoot=Group("Active infected",props);
            m.enemyPrefabs=new[]{"Shambler","Sprinter","Spitter","Bloater","Regurgitator","Worm","PukeWorm"}.Select(n=>Load<EnemyBrain>(Root+"/Prefabs/"+n+".prefab")).ToArray();
            var oldCanvas=m.player.area.GetComponentInParent<Canvas>();if(oldCanvas)oldCanvas.gameObject.SetActive(false);
            var hudGo=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/HUD/Prefabs/ShipHUD.prefab"));m.hud=hudGo.GetComponent<ShipHud>();m.hud.explorer=null;m.hud.playerHealth=m.health;m.hud.externalClock=true;
            m.hud.radar.player=m.player.transform;m.hud.radar.doors=new Onslaught.ShipDoor[0];m.hud.radar.barricades=new DefenseGate[0];
            m.player.prompt=m.hud.transform.Find("Safe area/Interaction prompt").GetComponent<TMP_Text>();
            m.hud.transform.Find("Safe area/Preview caption").GetComponent<TMP_Text>().text="F1 CONTROLS   /   ESC PAUSE";
            m.hud.helpPanel.transform.Find("Movement keys").GetComponent<TMP_Text>().text="WASD MOVE  SHIFT SPRINT  LMB FIRE  RMB AIM  R RELOAD\nE SWITCH GUN  F INTERACT / HOLD C4  TAB MAP  ESC PAUSE";
            foreach(var label in hudGo.GetComponentsInChildren<TMP_Text>(true)){if(label.text.Contains("MANTIS"))label.text="VACCINE / TACTICAL";if(label.name=="Operation label")label.text="OPERATION / 02";}
            m.hud.helpPanel.SetActive(false);
            var safe=m.hud.transform.Find("Safe area");
            m.subtitlePanel=Panel(safe,"Mission radio",new Vector2(.5f,0),new Vector2(0,140),new Vector2(680,85)).gameObject;m.subtitle=Text(m.subtitlePanel.transform,"Radio message",new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,70),17);
            m.notice=Text(safe,"Combat notification",new Vector2(.5f,.5f),new Vector2(0,-95),new Vector2(650,45),18);
            m.resultPanel=Panel(safe,"Mission result",new Vector2(.5f,.5f),Vector2.zero,new Vector2(660,220)).gameObject;
            m.resultTitle=Text(m.resultPanel.transform,"Result title",new Vector2(.5f,.5f),new Vector2(0,40),new Vector2(630,65),36);
            m.resultDetail=Text(m.resultPanel.transform,"Result detail",new Vector2(.5f,.5f),new Vector2(0,-40),new Vector2(630,80),20);
            var fade=Panel(m.hud.transform,"Departure fade",new Vector2(.5f,.5f),Vector2.zero,new Vector2(4000,2400));fade.GetComponent<Image>().color=Color.black;m.fade=fade.gameObject.AddComponent<CanvasGroup>();m.fade.alpha=0;fade.SetAsFirstSibling();
            Weapons();
            m.radio=props.gameObject.AddComponent<AudioSource>();m.radio.playOnAwake=false;m.radio.volume=.8f;
            props.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency=3400;props.gameObject.AddComponent<AudioHighPassFilter>().cutoffFrequency=380;
            m.messages=Enumerable.Range(0,12).Select(i=>Load<AudioClip>(Root+"/Audio/Radio-"+i.ToString("00")+".wav")).ToArray();
        }
        static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size)
        {var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(parent,false);t.anchorMin=t.anchorMax=anchor;t.anchoredPosition=pos;t.sizeDelta=size;return t;}
        static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size)
        {var t=Rect(parent,name,anchor,pos,size);var i=t.gameObject.AddComponent<Image>();i.color=new Color(.015f,.025f,.03f,.91f);i.raycastTarget=false;return t;}
        static TMP_Text Text(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size,float fontSize)
        {var t=Rect(parent,name,anchor,pos,size).gameObject.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.fontSize=fontSize;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;t.raycastTarget=false;t.text="";return t;}
        static void Weapons()
        {
            var w=m.player.gameObject.AddComponent<PlayerWeapons>();m.weapons=w;w.vaccine=m.player;w.health=m.health;w.hud=m.hud;
            w.specifications=new[]{"Pistol","Rifle"}.Select(n=>Load<WeaponSpec>("Assets/Mission/Data/"+n+".asset")).ToArray();
            w.mount=Group("First person weapon mount",m.player.eye.transform);w.mount.localPosition=new Vector3(.22f,-.25f,.4f);
            w.models=new GameObject[2];w.slides=new Transform[2];w.magazines=new Transform[2];w.muzzles=new Transform[2];
            for(int i=0;i<2;i++)
            {
                w.models[i]=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Mission/Weapons/"+(i==0?"Pistol":"Rifle")+".prefab"),w.mount);
                w.models[i].transform.localPosition=Vector3.zero;w.models[i].transform.localRotation=Quaternion.identity;
                var children=w.models[i].GetComponentsInChildren<Transform>(true);w.slides[i]=children.First(t=>t.name=="Moving bolt and slide");w.magazines[i]=children.First(t=>t.name=="Magazine");w.muzzles[i]=children.First(t=>t.name=="Muzzle socket");w.models[i].SetActive(i==1);
            }
            var flash=Load<Material>("Assets/Mission/Materials/Muzzle and tracer.mat");
            if(!flash)flash=AssetDatabase.FindAssets("t:Material",new[]{"Assets/Mission/Materials"}).Select(g=>Load<Material>(AssetDatabase.GUIDToAssetPath(g))).First(a=>a.name.ToLower().Contains("flash"));
            w.flash=Box("Muzzle flash",Vector3.zero,new Vector3(.08f,.08f,.14f),"LampWarm",m.player.eye.transform,false);w.flash.layer=2;w.flash.GetComponent<Renderer>().sharedMaterial=flash;w.flash.SetActive(false);
            w.impact=Box("Reusable impact",Vector3.zero,Vector3.one*.05f,"LampWarm",props,false);w.impact.GetComponent<Renderer>().sharedMaterial=flash;w.impact.SetActive(false);
            w.tracer=Group("Reusable tracer",props).gameObject.AddComponent<LineRenderer>();w.tracer.sharedMaterial=flash;w.tracer.positionCount=2;w.tracer.startWidth=.009f;w.tracer.endWidth=.003f;w.tracer.enabled=false;
            w.audioSource=m.player.eye.gameObject.AddComponent<AudioSource>();w.audioSource.playOnAwake=false;w.audioSource.volume=.42f;
            Onslaught.Mission.Editor.MissionBuilder.AddWeaponFill(m.player.eye.transform);
            var hit=Text(m.hud.transform,"Hit marker",new Vector2(.5f,.5f),Vector2.zero,new Vector2(30,30),24);hit.text="×";w.hitGraphic=hit;w.hitMarker=hit.gameObject.AddComponent<CanvasGroup>();w.hitMarker.alpha=0;
        }
        static VaccineInteractable Use(string n,VaccineUse action,Vector3 p,Vector3 size,string material="Teal")
        {var g=Box(n,p,size,material);var use=g.AddComponent<VaccineInteractable>();use.action=action;use.label=n;return use;}
        static void Points(int zone,params Vector3[] points)
        {for(int i=0;i<points.Length;i++){var p=Anchor("Zone "+zone+" infected "+i,points[i]).gameObject.AddComponent<VaccineSpawnPoint>();p.zone=zone;p.kind=i%7==6?3:i%5==3?2:i%3==2?1:0;}}
        static void SurfaceObjects()
        {
            m.generator=Use("Backup generator control",VaccineUse.Power,new Vector3(-24.6f,1.4f,38.5f),new Vector3(1.2f,1.4f,.3f));
            m.tailSpawn=Anchor("Sprinter tail origin",new Vector3(-5,0,28));m.tailCharge=Use("Tail C4 placement",VaccineUse.TailCharge,new Vector3(-4,1.2f,26),new Vector3(.5f,.6f,.25f),"Amber");
            m.tailSeal=Group("Tail cavity sealed rubble",props).gameObject;for(int i=0;i<7;i++){var b=Box("Collapsed tail debris",new Vector3(-6+i*.9f,.6f+(i%3)*.22f,28),new Vector3(1.8f,1.5f,2),"Concrete",m.tailSeal.transform);b.transform.rotation=Quaternion.Euler(i*17,i*31,i*12);}
            var sound=m.tailSeal.AddComponent<AudioSource>();sound.playOnAwake=false;sound.clip=Load<AudioClip>("Assets/Mission/Audio/GateCollapse.wav");sound.volume=.8f;Ignore(m.tailSeal);m.tailSeal.SetActive(false);
            m.stationShutter=Box("Station security shutter",new Vector3(33,2,-9.5f),new Vector3(.3f,4,6),"Steel");Ignore(m.stationShutter);
            m.stationLights=Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Where(l=>l.type!=LightType.Directional&&((l.transform.position.x>0&&l.transform.position.z<1)||l.transform.position.y<-2)).ToArray();
            m.stationFixtures=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r=>r.sharedMaterial&&(r.sharedMaterial.name.Contains("Lamp")||r.sharedMaterial.name.Contains("Emergency"))&&((r.transform.position.x>0&&r.transform.position.z<1)||r.transform.position.y<-2)).Cast<Renderer>().ToArray();
            foreach(var r in m.stationFixtures)r.gameObject.isStatic=false;
            var barriers=new List<GameObject>();var transfer=m.player.GetComponent<VaccineBoardingTransfer>();
            foreach(var door in transfer.train.GetComponentsInChildren<SubwayTrainDoor>())
                if(door.name=="Side automatic door"){var g=Box("Train one clearance barrier",door.transform.position,new Vector3(2.5f,3,.18f),"Steel");g.layer=8;g.GetComponent<Renderer>().enabled=false;barriers.Add(g);}
            m.trainBoardingBarriers=barriers.ToArray();
            var safe=Anchor("Helicopter safe zone navigation exclusion",new Vector3(-33,1,-19.5f)).gameObject.AddComponent<NavMeshModifierVolume>();safe.size=new Vector3(66,5,39);safe.area=1;
            Points(0,new Vector3(-10,0,12),new Vector3(0,0,8),new Vector3(8,0,18),new Vector3(16,0,5),new Vector3(14,0,16),new Vector3(26,0,11),new Vector3(34,0,6),new Vector3(-15,0,19),new Vector3(5,0,29),new Vector3(8,0,26));
            Points(1,new Vector3(-23,0,33),new Vector3(-33,0,34),new Vector3(-31,0,40));
            Points(2,new Vector3(23,0,-12),new Vector3(12,0,-16),new Vector3(6,0,-22),new Vector3(20,0,-24),new Vector3(39,0,-33),new Vector3(53,0,-24),new Vector3(55,0,-12),new Vector3(54,0,0));
            for(int i=0;i<3;i++)Points(3+i,new Vector3(66,0,-33+i*15),new Vector3(71,0,-30+i*15),new Vector3(76,0,-33+i*15));
            Points(6,new Vector3(95,-12,8),new Vector3(113,-12,8),new Vector3(129,-12,8),new Vector3(146,-12,8),new Vector3(108,-12,21.6f),new Vector3(127,-12,21.6f),new Vector3(146,-12,21.6f),new Vector3(100,-12,-16),new Vector3(118,-12,-16),new Vector3(132,-12,-16),new Vector3(139,-12,-34.6f),new Vector3(147,-12,-34.6f));
            Use("Helicopter supplies",VaccineUse.Supplies,new Vector3(-8,.5f,-3),new Vector3(1.5f,1,.8f));
            Use("Platform supply cabinet",VaccineUse.Supplies,new Vector3(93,-11.5f,13),new Vector3(1.3f,1,.7f));
            m.spawns=props.GetComponentsInChildren<VaccineSpawnPoint>();m.boardingLinks=new NavMeshLink[0];
        }
        static void DistrictObjects()
        {
            m.train=Object.FindAnyObjectByType<LabDistrictReview>();m.train.stopSeconds=12;
            Points(8,new Vector3(-17,-12,30),new Vector3(-5,-12,30),new Vector3(20,-12,30),new Vector3(35,-12,30));
            Points(9,new Vector3(17,0,55),new Vector3(30,0,52),new Vector3(45,0,48),new Vector3(65,0,48),new Vector3(85,0,49),new Vector3(108,0,53),new Vector3(118,0,56));
            Points(10,new Vector3(68,1.88f,69),new Vector3(69,1.88f,79),new Vector3(85,1.88f,80),new Vector3(99,1.88f,81),new Vector3(112,1.88f,80),new Vector3(68,1.88f,94),new Vector3(87,1.88f,95),new Vector3(115,1.88f,95),new Vector3(90,1.88f,65));
            Points(11,new Vector3(48,0,15),new Vector3(63,0,20),new Vector3(84,0,19),new Vector3(96,0,25),new Vector3(72,0,7));
            Points(12,new Vector3(117,0,17),new Vector3(131,0,26),new Vector3(144,0,19),new Vector3(120,0,32));
            Points(13,new Vector3(134,0,69),new Vector3(140,0,80),new Vector3(133,0,92),new Vector3(147,0,72));
            m.bossSpawn=Anchor("Parking Regurgitator emergence",new Vector3(114,0,35));
            m.vaccineCases=new[]{new Vector3(91,2.45f,64),new Vector3(115,2.45f,98),new Vector3(87,2.45f,98),new Vector3(68,2.45f,98)}.Select((p,i)=>Use("Vaccine sample / lab "+(i+1),VaccineUse.Vaccine,p,new Vector3(.9f,.6f,.6f),"Cream")).ToArray();
            Use("Street supplies",VaccineUse.Supplies,new Vector3(26,.5f,62),new Vector3(1.5f,1,.8f));
            Use("Lab medical supplies",VaccineUse.Supplies,new Vector3(61,2.4f,64),new Vector3(1.5f,1,.8f));
            m.extractionSites=new[]{Anchor("Ship zone 1 landing",new Vector3(136,0,17)),Anchor("Ship zone 2 landing",new Vector3(76,0,22)),Anchor("Ship zone 3 landing",new Vector3(140,0,84))};
            var craft=Group("Vaccine extraction dropship",props);m.extractionCraft=craft.gameObject;
            Box("Boarding floor",new Vector3(0,.2f,0),new Vector3(5,.4f,8),"Aircraft",craft);
            Box("Cabin roof",new Vector3(0,3.3f,0),new Vector3(5,.3f,8),"Aircraft",craft);
            foreach(float x in new[]{-2.5f,2.5f}){Box("Cabin side",new Vector3(x,1.7f,0),new Vector3(.2f,3,8),"Aircraft",craft);Box("Flight wing",new Vector3(x*2,2,1),new Vector3(5,.25f,3),"Aircraft",craft);Box("Lift engine",new Vector3(x*2,1.6f,1),new Vector3(1.4f,1.1f,4),"Steel",craft);}
            Box("Cockpit",new Vector3(0,1.6f,4.2f),new Vector3(4.5f,2.7f,1),"Aircraft",craft);Box("Cockpit glass",new Vector3(0,2.2f,4.75f),new Vector3(3.8f,1.1f,.1f),"Glass",craft);
            for(int i=0;i<3;i++)Box("Boarding step",new Vector3(0,.067f*(i+1),-5.2f+i*.4f),new Vector3(3,.134f*(i+1),.4f),"Steel",craft);
            m.extractionEntry=Anchor("Extraction boarding marker",new Vector3(0,.6f,-3),craft);Ignore(craft.gameObject);craft.gameObject.SetActive(false);
            m.spawns=props.GetComponentsInChildren<VaccineSpawnPoint>();m.stationLights=new Light[0];m.stationFixtures=new Renderer[0];m.trainBoardingBarriers=new GameObject[0];m.waitingDoors=new VaccineBreakableDoor[0];
        }
        static NavMeshObstacle Obstacle(GameObject g)
        {
            var colliders=g.GetComponentsInChildren<Collider>();var bounds=colliders[0].bounds;foreach(var c in colliders)bounds.Encapsulate(c.bounds);
            var t=Anchor("Navigation door obstacle",bounds.center);var o=t.gameObject.AddComponent<NavMeshObstacle>();o.shape=NavMeshObstacleShape.Box;o.size=bounds.size+new Vector3(.1f,0,.1f);o.carving=true;o.carveOnlyStationary=false;Ignore(g);return o;
        }
        static void Doors()
        {
            var waiting=new List<VaccineBreakableDoor>();int index=3;
            foreach(var d in Object.FindObjectsByType<ReviewDoor>().OrderBy(d=>d.name))
            {
                var o=Obstacle(d.gameObject);
                if(!m.labDistrict)
                {
                    var b=d.gameObject.AddComponent<VaccineBreakableDoor>();b.door=d;b.obstacle=o;b.encounter=d.name.StartsWith("Electrical")?1:index++;b.health=b.encounter==1?68:85;if(b.encounter>=3)waiting.Add(b);
                }
                else{var sync=d.gameObject.AddComponent<VaccineDoorObstacle>();sync.door=d;sync.obstacle=o;}
            }
            m.waitingDoors=waiting.ToArray();
            foreach(var gate in Object.FindObjectsByType<LabSecurityGate>()){var sync=gate.gameObject.AddComponent<VaccineDoorObstacle>();sync.gate=gate;sync.obstacle=Obstacle(gate.gameObject);}
            foreach(var d in Object.FindObjectsByType<SubwayTrainDoor>(FindObjectsInactive.Include))Ignore(d.gameObject);
        }
        static NavMeshSurface BakeSurface(GameObject root,string name,CollectObjects collect)
        {
            var s=root.AddComponent<NavMeshSurface>();s.agentTypeID=agentId;s.collectObjects=collect;s.useGeometry=NavMeshCollectGeometry.PhysicsColliders;s.layerMask=~((1<<2)|(1<<8));s.overrideVoxelSize=true;s.voxelSize=.13f;s.BuildNavMesh();
            if(!s.navMeshData)throw new InvalidOperationException("Navigation bake failed: "+name);
            AssetDatabase.CreateAsset(s.navMeshData,Root+"/Navigation/"+name+".asset");return s;
        }
        static void Navigation(bool district)
        {
            var obstacles=Object.FindObjectsByType<NavMeshObstacle>();foreach(var o in obstacles)o.enabled=false;
            if(district)
            {
                m.train.train.gameObject.SetActive(false);foreach(var s in m.train.stations)s.SetActive(true);
                BakeSurface(Group("District navigation").gameObject,"District",CollectObjects.All);
                m.train.train.gameObject.SetActive(true);BakeSurface(m.train.train.gameObject,"MovingTrain",CollectObjects.Children);
                var links=new List<NavMeshLink>();
                foreach(var d in m.train.train.GetComponentsInChildren<SubwayTrainDoor>())
                    if(d.name=="Side automatic door"&&d.transform.position.z>24)
                    {
                        var t=Group("Train platform navigation link",m.train.train);t.position=new Vector3(d.transform.position.x,-12,26.3f);
                        var link=t.gameObject.AddComponent<NavMeshLink>();link.agentTypeID=agentId;link.startPoint=new Vector3(0,0,-2);link.endPoint=new Vector3(0,0,2.5f);link.width=1.6f;link.bidirectional=true;link.autoUpdate=true;links.Add(link);
                    }
                m.boardingLinks=links.ToArray();for(int i=0;i<m.train.stations.Length;i++)m.train.stations[i].SetActive(i==3);
            }
            else BakeSurface(Group("Vaccine station navigation").gameObject,"CrashStation",CollectObjects.All);
            foreach(var o in obstacles)o.enabled=true;
        }
        static void MapData()
        {
            var link=m.gameObject.AddComponent<VaccineMissionHud>();link.mission=m;link.mapGraphic=m.hud.GetComponentInChildren<HudMapGraphic>(true);
            // Clip the scrolling plan to its actual radar rectangle.
            ClipRadar(link.mapGraphic);
            foreach(bool below in new[]{false,true})
            {
                var data=ScriptableObject.CreateInstance<HudMapData>();data.minimum=new Vector2(-70,-45);data.maximum=new Vector2(160,110);
                var vertices=new List<Vector2>();var tris=new List<int>();var walls=new List<Vector2>();
                foreach(var c in Object.FindObjectsByType<BoxCollider>())
                {
                    if(c.GetComponentInParent<VaccineMission>()||c.GetComponentInParent<Canvas>())continue;
                    var b=c.bounds;if((b.center.y<-2)!=below||b.size.x<.15f||b.size.z<.15f)continue;
                    var pts=new[]{new Vector2(b.min.x,b.min.z),new Vector2(b.max.x,b.min.z),new Vector2(b.max.x,b.max.z),new Vector2(b.min.x,b.max.z)};
                    if(b.size.y<.7f){int n=vertices.Count;vertices.AddRange(pts);tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}else if(b.size.y>1.8f&&Mathf.Min(b.size.x,b.size.z)<1)walls.AddRange(pts);
                }
                data.floorVertices=vertices.ToArray();data.floorTriangles=tris.ToArray();data.wallQuads=walls.ToArray();AssetDatabase.CreateAsset(data,Root+"/Data/"+(m.labDistrict?"District":"Crash")+(below?"Below":"Above")+".asset");
                if(below)link.undergroundMap=data;else link.surfaceMap=data;
            }
            link.mapGraphic.data=link.surfaceMap;m.hud.radar.data=link.surfaceMap;
        }
        static string Report()=>m.spawns.Length+" encounter markers, "+m.stationLights.Length+" power-controlled lights; mission HUD, weapons and navigation saved.";
        static void ClipRadar(HudMapGraphic map)
        {
            var plan=map.rectTransform;if(plan.parent.name=="Radar viewport")return;
            var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(plan.gameObject);if(prefab)PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var stale=plan.parent.Find("Radar viewport");if(stale)Object.DestroyImmediate(stale.gameObject);
            plan.anchorMin=plan.anchorMax=new Vector2(0,1);plan.pivot=new Vector2(0,1);plan.anchoredPosition=new Vector2(14,-43);plan.sizeDelta=new Vector2(302,150);
            var mask=plan.GetComponent<RectMask2D>();if(mask)Object.DestroyImmediate(mask);
            var viewport=Rect(plan.parent,"Radar viewport",plan.anchorMin,plan.anchoredPosition,plan.sizeDelta);viewport.pivot=plan.pivot;viewport.SetSiblingIndex(plan.GetSiblingIndex());viewport.gameObject.AddComponent<RectMask2D>();
            plan.SetParent(viewport,false);plan.anchorMin=Vector2.zero;plan.anchorMax=Vector2.one;plan.offsetMin=plan.offsetMax=Vector2.zero;
        }
        public static string FinishRepairs()
        {
            foreach(string path in new[]{"Assets/Vaccine/Scenes/Vaccine_Blockout.unity",LabDistrictBuilder.ScenePath})
            {
                EditorSceneManager.OpenScene(path);m=Object.FindAnyObjectByType<VaccineMission>();ClipRadar(m.hud.GetComponentInChildren<HudMapGraphic>(true));
                if(!m.labDistrict){m.spawns.First(p=>p.zone==0&&p.name.EndsWith(" 4")).transform.position=new Vector3(14,0,16);m.spawns.First(p=>p.zone==0&&p.name.EndsWith(" 9")).transform.position=new Vector3(8,0,26);m.spawns.First(p=>p.zone==6&&p.name.EndsWith(" 10")).transform.position=new Vector3(139,-12,-34.6f);m.spawns.First(p=>p.zone==6&&p.name.EndsWith(" 11")).transform.position=new Vector3(147,-12,-34.6f);}
                var panel=(RectTransform)m.hud.missionObjective.transform.parent;panel.sizeDelta=new Vector2(310,148);
                m.hud.missionObjective.rectTransform.sizeDelta=new Vector2(275,62);m.hud.missionObjective.textWrappingMode=TextWrappingModes.Normal;m.hud.missionObjective.alignment=TextAlignmentOptions.TopLeft;
                m.hud.waveText.rectTransform.anchoredPosition=new Vector2(8,-155);
                foreach(var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))if(l.name!="Weapon soft fill")l.cullingMask &= ~(1<<2);
                m.player.eye.transform.Find("Weapon soft fill").GetComponent<Light>().intensity=.35f;
                string materialFolder=Root+"/Materials";if(!AssetDatabase.IsValidFolder(materialFolder))AssetDatabase.CreateFolder(Root,"Materials");
                foreach(var r in m.weapons.models.SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)))
                {
                    string pathMat=materialFolder+"/"+r.sharedMaterial.name+".mat";var mat=Load<Material>(pathMat);
                    if(!mat){mat=Object.Instantiate(r.sharedMaterial);mat.name=r.sharedMaterial.name;mat.SetColor("_SpecColor",Color.black);mat.SetFloat("_Smoothness",.05f);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");AssetDatabase.CreateAsset(mat,pathMat);}
                    r.sharedMaterial=mat;
                }
                Save();
            }
            EditorSceneManager.OpenScene("Assets/Vaccine/Scenes/Vaccine_Blockout.unity");return "Radar clipped and encounter positions repaired in both saved scenes.";
        }
        public static string ValidateSpawns()
        {
            m=Object.FindAnyObjectByType<VaccineMission>();var filter=new NavMeshQueryFilter{agentTypeID=m.enemyPrefabs[0].GetComponent<NavMeshAgent>().agentTypeID,areaMask=NavMesh.AllAreas};
            var missing=m.spawns.Where(p=>!NavMesh.SamplePosition(p.transform.position,out _,2.5f,filter)).Select(p=>p.name+" "+p.transform.position).ToArray();
            return string.Join("\n",missing);
        }
    }
}
