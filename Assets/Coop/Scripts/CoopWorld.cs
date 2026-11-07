using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using ShipEnemies;
using Onslaught.Mission;
using Vaccine;

namespace SASCoop
{
    // One authoritative simulation; clients send player intent and receive world snapshots.
    // Stable scene indices are sorted by hierarchy path and travel messages are host-only.
    [DefaultExecutionOrder(-900)]
    public sealed class CoopWorld : MonoBehaviour
    {
        public static CoopWorld Instance;
        [Serializable]public class ActorState{public int id,kind;public Vector3 p;public Quaternion q;public float hp,max;public string animation;}
        [Serializable]public class PlayerState{public ulong id;public Vector3 p;public Quaternion q;public float hp=160,max=160;public int role;public string scene;public bool held;public float trainX,reviveIn;}
        [Serializable]public class WorldFrame
        {
            public string scene;public ActorState[] enemies,acid;public PlayerState[] players;
            public bool power,tail,tailActive,vaccine,boarding,boss,extracting,victory,defeat,craftActive,lab;
            public Vector3 craft,ship,train;public Quaternion craftQ;public bool touring,travelling;public int stop,phase;public float timer,fade;
            public string objective,wave,clock,sub,notice,resultTitle,resultDetail,radioClip;public bool subVisible,result;
            public bool[] breached,unlocked,doorOpen;public float[] gateHp;public bool[] gateBroken;
            public int[] cases;public bool ready;
            public MedicalSupplies.Entry[] medical;
        }
        [Serializable]public class Intent{public string scene,action;public Vector3 origin,direction;public int index,weapon;}
        [Serializable]public class ShotEffect{public ulong id;public Vector3 a,b;}
        [Serializable]public class Supplies{public int pistol,rifle;}
        public readonly Dictionary<ulong,PlayerState> Players=new Dictionary<ulong,PlayerState>();
        readonly Dictionary<ulong,GameObject> avatars=new Dictionary<ulong,GameObject>();
        readonly Dictionary<EnemyBrain,int> enemyIds=new Dictionary<EnemyBrain,int>();
        readonly Dictionary<int,EnemyBrain> proxies=new Dictionary<int,EnemyBrain>();
        readonly Dictionary<UnityEngine.Object,int> acidIds=new Dictionary<UnityEngine.Object,int>();
        readonly Dictionary<int,GameObject> acidProxies=new Dictionary<int,GameObject>();
        readonly Dictionary<ulong,float> nextShot=new Dictionary<ulong,float>(),deadAt=new Dictionary<ulong,float>();
        readonly Dictionary<ulong,Vector3> respawnPositions=new Dictionary<ulong,Vector3>();
        readonly Dictionary<ulong,Vector3> lastSafePositions=new Dictionary<ulong,Vector3>();
        Vector3 missionSpawn;
        readonly Dictionary<ulong,int[]> magazines=new Dictionary<ulong,int[]>();
        readonly Dictionary<ulong,float[]> reloads=new Dictionary<ulong,float[]>();
        CoopSession session;VaccineMission v;MissionDirector m;PlayerWeapons weapons;Vitality local;Transform body;Camera eye;
        VaccineBreakableDoor[] breakDoors;VaccineInteractable[] interactables;LabSecurityPanel[] panels;ReviewDoor[] reviewDoors;Onslaught.ShipDoor[] shipDoors;
        EnemyBrain[] prefabs;float nextSend,nextState,nextSense,startTime;int nextId=1;bool initialized,applying,loaded;string scene;
        WorldFrame latest;string spoken;static Material tracerMaterial;
        public static bool AnyPosition(Func<Vector3,bool> test,Vector3 fallback)=>Instance?Instance.Players.Values.Any(p=>p.hp>0&&p.scene==Instance.scene&&test(p.p)):test(fallback);
        public static bool AllPosition(Func<Vector3,bool> test,Vector3 fallback)=>Instance?Instance.session.net.ConnectedClientsIds.All(id=>Instance.Players.TryGetValue(id,out var p)&&p.scene==Instance.scene&&p.hp>0&&test(p.p)):test(fallback);
        public static bool HasLivingTeam=>Instance&&Instance.avatars.Values.Where(a=>a).Select(a=>a.GetComponent<Vitality>()).Append(Instance.local).Any(h=>h&&h.Alive);
        public static Vitality Nearest(Vector3 point,Vitality fallback)
        {
            if(!Instance||CoopSession.Client)return fallback;
            var candidates=Instance.avatars.Values.Where(a=>a).Select(a=>a.GetComponent<Vitality>()).Append(Instance.local).Where(h=>h&&h.Alive);
            return candidates.OrderBy(h=>(h.transform.position-point).sqrMagnitude).FirstOrDefault()??fallback;
        }
        Vitality TargetFor(EnemyBrain enemy)
        {
            var people=avatars.Values.Where(a=>a).Select(a=>a.GetComponent<Vitality>()).Append(local).Where(h=>h&&h.Alive).ToArray();
            var targeting=enemy.GetComponent<EnemyTargeting>();return targeting?targeting.Choose(people):people.OrderBy(p=>(p.transform.position-enemy.transform.position).sqrMagnitude).FirstOrDefault();
        }
        public static string Path(Component c)
        {
            // Unity can reorder scene roots while building. Names and authored
            // local transforms remain stable; root sibling indices do not.
            string Segment(Transform t)=>t.name+"@"+t.localPosition.ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
            string path=Segment(c.transform);for(var p=c.transform.parent;p;p=p.parent)path=Segment(p)+"/"+path;return path;
        }
        static T[] Sorted<T>() where T:Component=>UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include).OrderBy(c=>Path(c)).ToArray();
        void Awake()
        {
            Instance=this;session=CoopSession.Instance;scene=SceneManager.GetActiveScene().name;v=VaccineMission.Instance;m=MissionDirector.Instance;
            if(CoopSession.Client)
            {
                if(v){v.enabled=false;v.StopAllCoroutines();v.health.maximum=160;v.health.ResetHealth();v.hud.playerHealth=v.health;v.hud.externalClock=true;v.resultPanel.SetActive(false);}
                if(m){m.enabled=false;m.StopAllCoroutines();m.player.maximum=m.tuning.playerHealth;m.player.ResetHealth();m.hud.externalClock=true;m.endPanel.SetActive(false);m.departureCamera.enabled=false;m.cinematicBackdrop.SetActive(false);}
                foreach(var t in FindObjectsByType<VaccineBoardingTransfer>())t.enabled=false;
            }
            session.net.CustomMessagingManager.RegisterNamedMessageHandler("sas.pose",Pose);
            session.net.CustomMessagingManager.RegisterNamedMessageHandler("sas.world",Frame);
            session.net.CustomMessagingManager.RegisterNamedMessageHandler("sas.intent",ReceiveIntent);
            session.net.CustomMessagingManager.RegisterNamedMessageHandler("sas.shot",(id,r)=>{if(id!=0||!CoopSession.Client)return;var s=CoopSession.Read<ShotEffect>(r);if(s.id!=session.net.LocalClientId)DrawShot(s.a,s.b);});
            session.net.CustomMessagingManager.RegisterNamedMessageHandler("sas.supplies",(id,r)=>{if(id!=0||!CoopSession.Client)return;var s=CoopSession.Read<Supplies>(r);weapons.AddAmmo(s.pistol,s.rifle);});
            startTime=Time.unscaledTime;
        }
        IEnumerator Start()
        {
            weapons=v?v.weapons:m.weapons;local=v?v.health:m.player;body=v?v.player.transform:m.explorer.transform;eye=v?v.player.eye:m.explorer.eye;prefabs=v?v.enemyPrefabs:m.enemyPrefabs;
            breakDoors=Sorted<VaccineBreakableDoor>();interactables=Sorted<VaccineInteractable>();panels=Sorted<LabSecurityPanel>();reviewDoors=Sorted<ReviewDoor>();shipDoors=Sorted<Onslaught.ShipDoor>();
            missionSpawn=body.position;yield return null;yield return null;
            if(!v||!v.labDistrict)Place(body.position+Vector3.right*((int)session.net.LocalClientId%4)*1.1f);
            
            if(CoopSession.Client&&v)v.RestoreTrainInventory();
            initialized=true;Time.timeScale=0;
        }
        void Place(Vector3 position){var cc=body.GetComponent<CharacterController>();cc.enabled=false;body.position=position;cc.enabled=true;}
        void Update()
        {
            if(!initialized||!CoopSession.Online)return;
            // Escape releases the mouse in co-op; the host never pauses other players' simulation.
            if(UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true){if(v)v.player.Capture(false);else m.explorer.Capture(false);}
            if(UnityEngine.InputSystem.Keyboard.current?.f10Key.wasPressedThisFrame==true){session.Leave();return;}
            if(Time.unscaledTime>=nextSend)
            {
                nextSend=Time.unscaledTime+.066f;
                var p=new PlayerState{id=session.net.LocalClientId,p=body.position,q=body.rotation,hp=local.Current,max=local.maximum,role=session.LocalRole,scene=scene,held=v&&v.player.Actions["Interact"].IsPressed(),trainX=v&&v.train?v.train.train.position.x:0};
                Players[p.id]=p;if(CoopSession.Client)session.Send("sas.pose",p,0,NetworkDelivery.UnreliableSequenced);
            }
            if(CoopSession.Authority)
            {
                foreach(var id in avatars.Keys.ToArray())if(!session.net.ConnectedClientsIds.Contains(id)){Destroy(avatars[id]);avatars.Remove(id);Players.Remove(id);}
                bool all=session.net.ConnectedClientsIds.All(id=>Players.TryGetValue(id,out var p)&&p.scene==scene);
                if(!loaded&&all){loaded=true;Time.timeScale=1;}
                if(!loaded&&Time.unscaledTime-startTime>75){session.Status="A teammate did not finish loading. Returned to the menu.";session.Leave();return;}
                if(Time.time>=nextSense){nextSense=Time.time+.3f;foreach(var e in EnemyBrain.Living.ToArray())if(e&&!e.displayOnly){var actor=e.GetComponent<VaccineEnemyActor>();if(actor&&actor.mission&&!actor.BossReinforcement&&!actor.Alerted&&!actor.mission.AreaAlerted(actor.zone))continue;var target=TargetFor(e);if(e.target!=target)e.SetTarget(target);}CheckDeaths();}
                if(Time.unscaledTime>=nextState){nextState=Time.unscaledTime+.1f;session.Broadcast("sas.world",Capture());}
            }
            else if(latest!=null)Interpolate();
            if(!local.Alive){if(v)v.player.enabled=false;else m.explorer.enabled=false;weapons.Locked=true;}
        }
        void CheckDeaths()
        {
            var team=avatars.Values.Where(a=>a).Select(a=>a.GetComponent<Vitality>()).Append(local).ToArray();
            foreach(var p in Players.Values.ToArray())
            {
                var hp=p.id==session.net.LocalClientId?local:avatars.TryGetValue(p.id,out var a)?a.GetComponent<Vitality>():null;if(!hp)continue;
                if(hp.Alive){deadAt.Remove(p.id);if(Physics.Raycast(hp.transform.position+Vector3.up*.2f,Vector3.down,out var floor,.7f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&floor.normal.y>.65f&&!floor.collider.GetComponentInParent<Vitality>())lastSafePositions[p.id]=hp.transform.position;continue;}
                if(!deadAt.ContainsKey(p.id))deadAt[p.id]=Time.time;
                if(Time.time-deadAt[p.id]<10)continue;
                var living=team.Where(h=>h.Alive).OrderBy(h=>(h.transform.position-hp.transform.position).sqrMagnitude).FirstOrDefault();
                if(!CoopRevival.FindPosition(living?living.transform.position:hp.transform.position,hp,out var position)&&!CoopRevival.FindPosition(lastSafePositions.TryGetValue(p.id,out var safe)?safe:missionSpawn,hp,out position))continue;
                hp.ResetHealth();hp.InvulnerableUntil=Time.time+3;hp.transform.position=position;
                p.p=hp.transform.position;p.hp=hp.Current;if(p.id!=session.net.LocalClientId)respawnPositions[p.id]=p.p;deadAt.Remove(p.id);
                if(hp==local){Place(p.p);if(v)v.player.enabled=true;else m.explorer.enabled=true;weapons.Locked=false;}
            }
        }
        void Pose(ulong id,FastBufferReader r)
        {
            if(!CoopSession.Authority||id==session.net.LocalClientId)return;var p=CoopSession.Read<PlayerState>(r);if(p.scene!=scene||!Finite(p.p)||!Finite(p.q))return;p.id=id;
            p.role=session.Members.TryGetValue(id,out var member)?member.role:0;
            if(!avatars.TryGetValue(id,out var avatar)){avatar=Avatar(id);avatars[id]=avatar;magazines[id]=new[]{12,RoleLoadout.Magazine(p.role)};reloads[id]=new float[2];}
            var hp=avatar.GetComponent<Vitality>();if(respawnPositions.TryGetValue(id,out var reset)){if(Vector3.Distance(p.p,reset)<3)respawnPositions.Remove(id);else p.p=reset;}p.hp=hp.Current;p.max=hp.maximum;
            if(v&&v.train&&v.train.Touring)p.p.x+=v.train.train.position.x-p.trainX;
            avatar.transform.SetPositionAndRotation(p.p,p.q);Players[id]=p;
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&p.sqrMagnitude<100000000;
        static bool Finite(Quaternion q)=>!float.IsNaN(q.x+q.y+q.z+q.w);
        GameObject Avatar(ulong id)
        {
            var a=new GameObject("Squad member "+(id+1));var hp=a.AddComponent<Vitality>();hp.maximum=local?local.maximum:160;hp.ResetHealth();if(session.CarriedHealth.TryGetValue(id,out float carried))hp.Replicate(carried,hp.maximum);hp.receivesEnemyAttacks=true;hp.aimHeight=1;
            var capsule=a.AddComponent<CapsuleCollider>();capsule.radius=.32f;capsule.height=1.8f;capsule.center=Vector3.up*.9f;
            Shape(a.transform,PrimitiveType.Capsule,new Vector3(0,.82f,0),new Vector3(.62f,.65f,.4f),new Color(.14f,.22f,.24f));
            Shape(a.transform,PrimitiveType.Sphere,new Vector3(0,1.6f,0),Vector3.one*.42f,new Color(.11f,.15f,.18f));
            Shape(a.transform,PrimitiveType.Cube,new Vector3(.24f,1.08f,.35f),new Vector3(.1f,.14f,.65f),new Color(.07f,.08f,.09f));
            var label=new GameObject("Name");label.transform.SetParent(a.transform);label.transform.localPosition=new Vector3(0,2.15f,0);var text=label.AddComponent<TextMesh>();text.text="SQUAD "+(id+1);text.characterSize=.08f;text.fontSize=32;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.4f,.9f,.9f);
            return a;
        }
        public static void Shape(Transform parent,PrimitiveType type,Vector3 p,Vector3 scale,Color color)
        {var g=GameObject.CreatePrimitive(type);g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;Destroy(g.GetComponent<Collider>());var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;g.GetComponent<Renderer>().sharedMaterial=mat;}
        WorldFrame Capture()
        {
            var enemies=new List<ActorState>();
            foreach(var e in EnemyBrain.Living.ToArray())if(e&&e.Health.Alive){if(!enemyIds.TryGetValue(e,out int id))enemyIds[e]=id=nextId++;enemies.Add(new ActorState{id=id,kind=(int)e.definition.kind,p=e.transform.position,q=e.transform.rotation,hp=e.Health.Current,max=e.Health.maximum,animation=e.AnimationState});}
            foreach(var id in Players.Keys.ToArray()){var p=Players[id];if(id==session.net.LocalClientId){p.hp=local.Current;p.p=body.position;}else if(avatars.TryGetValue(id,out var a)){p.hp=a.GetComponent<Vitality>().Current;p.p=a.transform.position;}p.reviveIn=deadAt.TryGetValue(id,out float died)?Mathf.Max(0,10-(Time.time-died)):10;}
            var acid=new List<ActorState>();foreach(var a in FindObjectsByType<AcidProjectile>())AcidState(a,0,acid);foreach(var a in FindObjectsByType<AcidPool>())AcidState(a,1,acid);foreach(var a in FindObjectsByType<SupplyPickup>())AcidState(a,a.medical?3:2,acid);
            var h=v?v.hud:m.hud;
            var f=new WorldFrame{scene=scene,ready=loaded,enemies=enemies.ToArray(),acid=acid.ToArray(),players=Players.Values.ToArray(),breached=breakDoors.Select(d=>d.Broken).ToArray(),unlocked=panels.Select(p=>p.gate.Unlocked).ToArray(),doorOpen=reviewDoors.Select(d=>d.Open).Concat(shipDoors.Select(d=>d.RequestedOpen)).ToArray(),objective=h.missionObjective.text,wave=h.waveText.text,clock=h.timerText.text};
            f.radioClip=v?(v.radio.clip?v.radio.clip.name:""):(m.intercom.clip?m.intercom.clip.name:"");
            f.medical=MedicalSupplies.Snapshot();
            if(v){f.lab=v.labDistrict;f.power=v.PowerOn;f.tail=v.TailSealed;f.tailActive=v.TailActive;f.vaccine=v.VaccineRecovered;f.boarding=v.BoardingAllowed;f.boss=v.BossDefeated;f.extracting=v.Extracting;f.victory=v.Victory;f.defeat=v.Defeated;f.sub=v.subtitle.text;f.subVisible=v.subtitlePanel.activeSelf;f.notice=v.notice.text;f.result=v.resultPanel.activeSelf;f.resultTitle=v.resultTitle.text;f.resultDetail=v.resultDetail.text;f.fade=v.fade.alpha;f.cases=v.vaccineCases.Select(c=>c.gameObject.activeSelf?1:0).ToArray();if(v.extractionCraft){f.craftActive=v.extractionCraft.activeSelf;f.craft=v.extractionCraft.transform.position;f.craftQ=v.extractionCraft.transform.rotation;}if(v.train){f.train=v.train.train.position;f.touring=v.train.Touring;f.travelling=v.train.Travelling;f.stop=v.train.StopIndex;f.timer=v.train.Remaining;}}
            else{f.phase=(int)m.Phase;f.timer=m.DefenseElapsed;f.sub=m.subtitle.text;f.subVisible=m.subtitlePanel.activeSelf;f.notice=m.notification.text;f.result=m.endPanel.activeSelf;f.resultTitle=m.endTitle.text;f.resultDetail=m.endDetail.text;f.fade=m.fade.alpha;f.ship=m.shipRoot.position;f.gateHp=m.gates.Select(g=>g.Health.Current).ToArray();f.gateBroken=m.gates.Select(g=>g.Broken).ToArray();}
            return f;
        }
        void AcidState(Component a,int kind,List<ActorState> list){if(!acidIds.TryGetValue(a,out int id))acidIds[a]=id=nextId++;list.Add(new ActorState{id=id,kind=kind,p=a.transform.position,q=a.transform.rotation,hp=a.transform.localScale.x});}
        void Frame(ulong id,FastBufferReader r)
        {
            if(!CoopSession.Client||id!=0||!initialized)return;var f=CoopSession.Read<WorldFrame>(r);if(f.scene!=scene)return;latest=f;applying=true;
            if(f.ready&&!loaded){loaded=true;Time.timeScale=1;}
            MedicalSupplies.Apply(f.medical);
            foreach(var p in f.players)
            {
                Players[p.id]=p;
                if(p.id==session.net.LocalClientId){bool revive=!local.Alive&&p.hp>0;local.Replicate(p.hp,p.max);if(revive){Place(p.p);if(v)v.player.enabled=true;else m.explorer.enabled=true;weapons.Locked=false;}continue;}
                if(!avatars.ContainsKey(p.id))avatars[p.id]=Avatar(p.id);avatars[p.id].GetComponent<Vitality>().Replicate(p.hp,p.max);
            }
            foreach(var key in avatars.Keys.ToArray())if(!f.players.Any(p=>p.id==key)){Destroy(avatars[key]);avatars.Remove(key);}
            foreach(var a in f.enemies)
            {
                if(!proxies.TryGetValue(a.id,out var e))
                {
                    var prefab=prefabs.FirstOrDefault(p=>(int)p.definition.kind==a.kind);if(!prefab)continue;
                    e=Instantiate(prefab,a.p,a.q);e.displayOnly=true;e.Agent.enabled=false;var va=e.GetComponent<VaccineEnemyActor>();if(va)va.enabled=false;var ma=e.GetComponent<MissionEnemy>();if(ma)ma.enabled=false;e.MovementOverride=null;EnemyBrain.Living.Add(e);proxies[a.id]=e;
                }
                e.Health.Replicate(a.hp,a.max);e.Play(a.animation=="Chase"?"Move":a.animation=="Ranged"?(e.definition.kind==EnemyKind.Regurgitator?"Vomit":"Spit"):a.animation);
            }
            foreach(var key in proxies.Keys.ToArray())if(!f.enemies.Any(a=>a.id==key)){var e=proxies[key];if(e){EnemyBrain.Living.Remove(e);e.Health.Replicate(0,e.Health.maximum);foreach(var c in e.GetComponentsInChildren<Collider>())c.enabled=false;e.Play("Death",true);Destroy(e.gameObject,2);}proxies.Remove(key);}
            for(int i=0;i<breakDoors.Length&&i<f.breached.Length;i++)if(f.breached[i])breakDoors[i].Breach();
            for(int i=0;i<panels.Length&&i<f.unlocked.Length;i++)if(f.unlocked[i])panels[i].Activate();
            for(int i=0;i<reviewDoors.Length&&i<f.doorOpen.Length;i++)if(reviewDoors[i].Open!=f.doorOpen[i])reviewDoors[i].Toggle();
            for(int i=0;i<shipDoors.Length&&i+reviewDoors.Length<f.doorOpen.Length;i++)shipDoors[i].Request(f.doorOpen[i+reviewDoors.Length]);
            if(v)v.ApplyCoop(f);else m.ApplyCoop(f);
            if(f.subVisible&&f.sub!=spoken){spoken=f.sub;var clips=v?v.messages:new[]{m.intro,m.bossWarning,m.clearMessage,m.launchMessage};var clip=clips.FirstOrDefault(c=>c&&c.name==f.radioClip);var audio=v?v.radio:m.intercom;if(clip){audio.clip=clip;audio.Play();}}
            var h=v?v.hud:m.hud;h.missionObjective.text=f.objective;h.waveText.text=f.wave;h.timerText.text=f.clock;
            h.crosshair.SetActive(local.Alive&&(v?v.player.Captured&&!v.player.MapVisible:m.explorer.Captured&&!m.explorer.MapVisible));
            var boss=proxies.Values.FirstOrDefault(e=>e&&e.definition.kind==EnemyKind.Regurgitator);h.SetBoss(boss?boss.Health:null);
            foreach(var a in f.acid){if(!acidProxies.TryGetValue(a.id,out var go)){go=GameObject.CreatePrimitive(a.kind==0?PrimitiveType.Sphere:PrimitiveType.Cylinder);Destroy(go.GetComponent<Collider>());var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=a.kind>=2?new Color(.15f,.7f,1):new Color(.35f,.85f,.06f);go.GetComponent<Renderer>().sharedMaterial=material;acidProxies[a.id]=go;}go.transform.position=a.p;go.transform.localScale=a.kind>=2?new Vector3(.5f,.3f,.5f):a.kind==0?Vector3.one*.25f:new Vector3(a.hp,.02f,a.hp);}
            foreach(var key in acidProxies.Keys.ToArray())if(!f.acid.Any(a=>a.id==key)){Destroy(acidProxies[key]);acidProxies.Remove(key);}
            applying=false;
        }
        void Interpolate()
        {
            float t=1-Mathf.Exp(-18*Time.unscaledDeltaTime);
            foreach(var p in latest.players)if(avatars.TryGetValue(p.id,out var a)){a.transform.position=Vector3.Lerp(a.transform.position,p.p,t);a.transform.rotation=Quaternion.Slerp(a.transform.rotation,p.q,t);var label=a.GetComponentInChildren<TextMesh>();if(label)label.transform.rotation=eye.transform.rotation;}
            foreach(var p in latest.enemies)if(proxies.TryGetValue(p.id,out var e)&&e){e.transform.position=Vector3.Lerp(e.transform.position,p.p,t);e.transform.rotation=Quaternion.Slerp(e.transform.rotation,p.q,t);}
        }
        public static bool Request(string action,Component target=null,int weapon=0,Vector3 origin=default,Vector3 direction=default)
        {
            if(!Instance||Instance.applying||!CoopSession.Client)return false;var w=Instance;int index=-1;
            if(target is VaccineInteractable vi)index=Array.IndexOf(w.interactables,vi);else if(target is LabSecurityPanel sp)index=Array.IndexOf(w.panels,sp);else if(target is ReviewDoor rd)index=Array.IndexOf(w.reviewDoors,rd);else if(target is Onslaught.ShipDoor sd)index=Array.IndexOf(w.shipDoors,sd);
            w.session.Send("sas.intent",new Intent{scene=w.scene,action=action,index=index,weapon=weapon,origin=origin,direction=direction},0);return true;
        }
        void ReceiveIntent(ulong id,FastBufferReader r)
        {
            if(!CoopSession.Authority||!initialized||!Players.TryGetValue(id,out var player)||player.hp<=0)return;var p=CoopSession.Read<Intent>(r);if(p.scene!=scene)return;
            if(p.action=="reload"){int slot=Mathf.Clamp(p.weapon,0,1);reloads[id][slot]=Time.time+(slot==0?1.35f:RoleLoadout.Reload(player.role));return;}
            if(p.action=="shot"){Fire(id,p,player);return;}
            Component target=null;
            if(p.action=="use"&&p.index>=0&&p.index<interactables.Length)target=interactables[p.index];
            if(p.action=="security"&&p.index>=0&&p.index<panels.Length)target=panels[p.index];
            if(p.action=="door"&&p.index>=0&&p.index<reviewDoors.Length)target=reviewDoors[p.index];
            if(p.action=="shipdoor"&&p.index>=0&&p.index<shipDoors.Length)target=shipDoors[p.index];
            if(!target||Vector3.Distance(player.p,target.transform.position)>5)return;
            if(target is VaccineInteractable item){if(item.medical){v.UseMedical(item,avatars[id].GetComponent<Vitality>(),id);}else if(item.action==VaccineUse.TailCharge){if(CoopSession.SquadSize==4&&player.role!=0)return;StartCoroutine(Plant(id,item));}else if(item.action==VaccineUse.Supplies){if(Time.time>=item.readyAt){session.Send("sas.supplies",new Supplies{pistol=36,rifle=120},id);item.readyAt=Time.time+35;}}else v.Use(item);}
            if(target is LabSecurityPanel panel)panel.Activate();if(target is ReviewDoor door)door.Toggle();if(target is Onslaught.ShipDoor shipDoor)shipDoor.Toggle();
        }
        IEnumerator Plant(ulong id,VaccineInteractable item)
        {float held=0;while(held<2.5f){if(!Players.TryGetValue(id,out var p)||p.hp<=0||!p.held||Vector3.Distance(p.p,item.transform.position)>4)yield break;held+=Time.deltaTime;yield return null;}v.CoopDetonate();}
        void Fire(ulong id,Intent p,PlayerState player)
        {
            if(!Finite(p.origin)||!Finite(p.direction)||Vector3.Distance(p.origin,player.p+Vector3.up*1.6f)>2||p.direction.sqrMagnitude<.5f)return;
            int slot=Mathf.Clamp(p.weapon,0,1);float rate=slot==0?4:RoleLoadout.Rate(player.role);if(nextShot.TryGetValue(id,out float at)&&Time.time<at)return;
            if(reloads[id][slot]>0&&Time.time>=reloads[id][slot]){magazines[id][slot]=slot==0?12:RoleLoadout.Magazine(player.role);reloads[id][slot]=0;}
            if(reloads[id][slot]>0||magazines[id][slot]<=0)return;magazines[id][slot]--;nextShot[id]=Time.time+1/rate*.9f;
            float damage=(slot==0?34:RoleLoadout.Damage(player.role))*(player.role==3?1.1f:1);
            var end=Hit(new Ray(p.origin,p.direction.normalized),damage,slot==1&&player.role==2?2:1,avatars[id].transform);
            DrawShot(p.origin,end);session.Broadcast("sas.shot",new ShotEffect{id=id,a=p.origin,b=end});
        }
        public static Vector3 Hit(Ray ray,float damage,int penetration,Transform owner)
        {
            var end=ray.GetPoint(90);var seen=new HashSet<EnemyBrain>();
            foreach(var h in Physics.RaycastAll(ray,90,Physics.DefaultRaycastLayers&~(1<<8),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(h.transform.IsChildOf(owner))continue;var hp=h.collider.GetComponentInParent<Vitality>();if(hp&&hp.receivesEnemyAttacks)continue;
                end=h.point;var e=h.collider.GetComponentInParent<EnemyBrain>();
                if(e&&e.Health.Alive){if(!seen.Add(e))continue;float bonus=e.definition.kind==EnemyKind.Regurgitator?(Vector3.Distance(h.point,e.mouth.position)<1.1f?1.5f:1):e.definition.kind==EnemyKind.Worm||e.definition.kind==EnemyKind.PukeWorm?1:h.point.y-e.transform.position.y>e.definition.height*.76f?2:1;e.Health.TakeDamage(damage*bonus,DamageKind.Physical,owner.GetComponent<Vitality>());if(seen.Count>=penetration)break;}
                else{var d=h.collider.GetComponentInParent<VaccineBreakableDoor>();if(d)d.Strike(damage);break;}
            }
            return end;
        }
        static void DrawShot(Vector3 a,Vector3 b){var g=new GameObject("Squad tracer");var l=g.AddComponent<LineRenderer>();l.positionCount=2;l.SetPosition(0,a);l.SetPosition(1,b);l.startWidth=.018f;l.endWidth=.008f;if(!tracerMaterial)tracerMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){color=new Color(1,.75f,.35f)};l.sharedMaterial=tracerMaterial;Destroy(g,.06f);}
        void LateUpdate()
        {
            if(!initialized||!local||local.Alive)return;
            float seconds=CoopSession.Authority?(deadAt.TryGetValue(session.net.LocalClientId,out float at)?Mathf.Max(0,10-(Time.time-at)):10):(Players.TryGetValue(session.net.LocalClientId,out var p)?p.reviveIn:10);
            string message="DOWNED / REVIVING IN "+Mathf.CeilToInt(seconds)+"s";
            if(v)v.notice.text=message;else m.notification.text=message;
        }
        public static void LocalShot(Vector3 a,Vector3 b){if(Instance&&CoopSession.Online&&CoopSession.Authority)Instance.session.Broadcast("sas.shot",new ShotEffect{id=Instance.session.net.LocalClientId,a=a,b=b});}
        public static void GrantSupply(Vitality target,bool medical)
        {
            if(!Instance)return;var w=Instance;if(medical)target.Heal(30);
            else if(target==w.local)w.weapons.AddAmmo(24,60);
            else{foreach(var pair in w.avatars)if(pair.Value&&pair.Value.GetComponent<Vitality>()==target){w.session.Send("sas.supplies",new Supplies{pistol=24,rifle=60},pair.Key);break;}}
        }
        public static void Blast(Vector3 point,float radius,float damage){if(!Instance||CoopSession.Client)return;foreach(var a in Instance.avatars.Values)if(a&&Vector3.Distance(a.transform.position,point)<radius)a.GetComponent<Vitality>().TakeDamage(damage);}
        public static void Salvage(int pistol,int rifle){if(Instance&&CoopSession.Authority)foreach(var id in Instance.session.net.ConnectedClientsIds)if(id!=Instance.session.net.LocalClientId)Instance.session.Send("sas.supplies",new Supplies{pistol=pistol,rifle=rifle},id);}
        void OnDestroy(){if(Instance==this)Instance=null;if(session&&session.net&&session.net.CustomMessagingManager!=null)foreach(var name in new[]{"sas.pose","sas.world","sas.intent","sas.shot","sas.supplies"})session.net.CustomMessagingManager.UnregisterNamedMessageHandler(name);}
    }
}
