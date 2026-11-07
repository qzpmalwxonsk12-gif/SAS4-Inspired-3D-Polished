using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ShipEnemies;
using ShipHUD;
using Onslaught.Mission;
using TMPro;

namespace Vaccine
{
    [DefaultExecutionOrder(-90)]
    public sealed partial class VaccineMission : MonoBehaviour
    {
        public static VaccineMission Instance{get;private set;}
        static int[] savedAmmo;static float savedHealth;static bool savedProgress;
        public bool labDistrict;
        public VaccineWalkthrough player;public Vitality health;public PlayerWeapons weapons;public ShipHud hud;
        public EnemyBrain[] enemyPrefabs;public Transform enemiesRoot;
        public VaccineSpawnPoint[] spawns;public VaccineBreakableDoor[] waitingDoors;
        public Light[] stationLights;public Renderer[] stationFixtures;
        public GameObject tailSeal,stationShutter;public GameObject[] trainBoardingBarriers;
        public Transform tailSpawn,bossSpawn;public VaccineInteractable generator,tailCharge;
        public VaccineInteractable[] vaccineCases;
        public GameObject extractionCraft;public Transform[] extractionSites;public Transform extractionEntry;
        public LabDistrictReview train;public Unity.AI.Navigation.NavMeshLink[] boardingLinks;
        public TMP_Text subtitle,notice,resultTitle,resultDetail;public GameObject subtitlePanel,resultPanel;public CanvasGroup fade;
        public AudioSource radio;public AudioClip[] messages;
        public int maximumAlive=28;
        public ReviewDoor[] labDoors = new ReviewDoor[0];
        public bool PowerOn{get;private set;}public bool TailActive{get;private set;}public bool TailSealed{get;private set;}
        public bool VaccineRecovered{get;private set;}public bool BossSpawned{get;private set;}public bool BossDefeated{get;private set;}
        public bool Extracting{get;private set;}public bool Victory{get;private set;}public bool Defeated{get;private set;}public bool Paused{get;private set;}
        public int Kills{get;private set;}public int SpawnFailures{get;private set;}
        public int CurrentCombatZone=>train&&train.Touring?15:labDistrict?9:player.transform.position.y<-2?6:TailActive&&!TailSealed?7:0;
        public bool CombatActive=>!Defeated&&!Victory&&!Extracting;
        public bool BoardingAllowed=>!labDistrict&&PowerOn&&TailSealed&&activated.Contains(6)&&new[]{1,2,3,4,5}.All(activated.Contains)&&Remaining==0;
        public int Remaining=>actors.Count(a=>a&&a.brain&&a.brain.Health.Alive)+queue.Count+reserves.Count+UnpreparedDistrictCount;
        public int ActiveCount=>actors.Count(a=>a&&a.brain&&a.brain.Health.Alive);
        public int EngagedCount=>actors.Count(a=>a&&a.brain&&a.brain.Health.Alive&&(a.BossReinforcement||AreaAlerted(a.zone)));
        public int Pending=>queue.Count+reserves.Count;
        readonly HashSet<int> activated=new HashSet<int>();readonly List<VaccineEnemyActor> actors=new List<VaccineEnemyActor>();
        readonly Queue<(Vector3 pos,int kind,int zone)> queue=new Queue<(Vector3,int,int)>();
        InputActionMap actions;float nextSpawn,nextTail,nextHud,messageUntil,noticeUntil,elapsed;bool planting,labStarted,boardingAnnounced,extractionReady,arrivalReady;
        int extractionIndex;Vector3 landedPosition;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){Instance=null;savedAmmo=null;savedProgress=false;}
        void Awake()
        {
            if(!labDistrict||!VaccineRouteState.Pending)MedicalSupplies.Reset();
            Instance=this;Time.timeScale=1;actions=new InputActionMap("Vaccine mission");actions.AddAction("Pause",InputActionType.Button,"<Keyboard>/escape");actions.AddAction("Retry",InputActionType.Button,"<Keyboard>/enter");actions.AddAction("Help",InputActionType.Button,"<Keyboard>/f1");actions.Enable();
            EnemyBrain.CanSpawnParasite=()=>CombatActive&&EngagedCount<maximumAlive;
        }
        IEnumerator Start()
        {
            health.maximum=160;health.ResetHealth();health.Died+=Fail;hud.playerHealth=health;hud.externalClock=true;resultPanel.SetActive(false);fade.alpha=0;
            if(labDistrict)
            {
                // Visible residents are placed while the train approaches; entry
                // still controls their awareness and the later co-op reserves.
                foreach(int zone in new[]{11,12,13})ActivateZone(zone);
                extractionCraft.SetActive(false);extractionIndex=UnityEngine.Random.Range(0,extractionSites.Length);
                int selected=UnityEngine.Random.Range(0,vaccineCases.Length);for(int i=0;i<vaccineCases.Length;i++)vaccineCases[i].gameObject.SetActive(i==selected);
                train.DepartureAllowed=()=>ZoneRemaining(15)==0;train.ReachedStop=Arrive;
                if(!VaccineRouteState.Pending){Arrive(3);Announce(7,"MISSION CONTROL","Research district reached. Recover a vaccine sample from the laboratory.");}
            }
            else
            {
                SetPower(false);ActivateZone(0);ActivateZone(1);tailSeal.SetActive(false);stationShutter.SetActive(true);
                Announce(0,"MISSION CONTROL","The chopper is out of fuel. Clear the crash courtyard, break into the electrical room, and restore backup power to the subway.",12);
            }
            yield return null; // PlayerWeapons initializes its magazines in Start.
            RestoreTrainInventory();
        }
        public void RestoreTrainInventory(){if(labDistrict&&savedProgress){health.Replicate(savedHealth,health.maximum);weapons.RestoreAmmunition(savedAmmo);savedProgress=false;}}
        public void SaveForTrain(){savedHealth=health.Current;savedAmmo=weapons.SaveAmmunition();savedProgress=true;}
        public void Register(VaccineEnemyActor a){if(!actors.Contains(a))actors.Add(a);}
        public void Unregister(VaccineEnemyActor a)=>actors.Remove(a);
        public int ZoneRemaining(int zone)=>actors.Count(a=>a&&a.zone==zone&&a.brain&&a.brain.Health.Alive)+queue.Count(q=>q.zone==zone)+reserves.Count(q=>q.zone==zone);
        public void ActivateZone(int zone)
        {
            if(!SASCoop.CoopSession.Authority||!activated.Add(zone))return;
            var points=spawns.Where(p=>p.zone==zone).OrderBy(_=>UnityEngine.Random.value).ToArray();int count=EncounterCount(points.Length);
            for(int i=0;i<count;i++){var p=points[i%points.Length];if(i<InitialCount(points.Length))queue.Enqueue((p.transform.position,p.kind,zone));else reserves.Add((p.transform.position,p.kind,zone));}
        }
        public void DoorBreached(int zone){if(zone>=0)ActivateZone(zone);if(zone>=3&&zone<=5)AlertArea(zone);}
        public EnemyBrain Spawn(Vector3 point,int kind,int zone)
        {
            var prefab=enemyPrefabs[Mathf.Clamp(kind,0,enemyPrefabs.Length-1)];
            if(!FindEncounterPosition(prefab,point,zone,out var safe)){SpawnFailures++;return null;}
            var e=Instantiate(prefab,safe,Quaternion.identity,enemiesRoot);e.GetComponent<VaccineEnemyActor>().Bind(this,zone);return e;
        }
        public void EnemyKilled(VaccineEnemyActor a)
        {
            Kills++;if(a.brain.definition.kind==EnemyKind.Regurgitator){BossDefeated=true;Announce(9,"MISSION CONTROL","The Regurgitator is down. Recover the vaccine and clear the remaining infected before extraction.");}
            if(Kills%5==0){weapons.AddAmmo(12,36);SASCoop.CoopWorld.Salvage(12,36);Notify("SALVAGED AMMO  +36 RIFLE / +12 PISTOL",2);}
        }
        void Update()
        {
            if(actions["Help"].WasPressedThisFrame())hud.ToggleHelp();
            if(actions["Retry"].WasPressedThisFrame()&&(Defeated||Victory)){Restart();return;}
            if(actions["Pause"].WasPressedThisFrame()&&!Defeated&&!Victory&&!Extracting){if(SASCoop.CoopSession.Online){player.Capture(false);return;}Paused=!Paused;Time.timeScale=Paused?0:1;player.Capture(!Paused);Notify(Paused?"PAUSED / ESC TO RESUME":"",999);}
            if(Paused)return;
            if(CombatActive)
            {
                elapsed+=Time.deltaTime;if(player.transform.position.y<-25)health.TakeDamage(10000);
                if(queue.Count>0&&EngagedCount<maximumAlive&&Time.time>=nextSpawn){var q=queue.Dequeue();if(!Spawn(q.pos,q.kind,q.zone))queue.Enqueue(q);nextSpawn=Time.time+.18f;}
                if(labDistrict)DistrictUpdate();else SurfaceUpdate();
                UpdateEncounterAreas();
                ReleaseReinforcements();
            }
            if(Time.time>=messageUntil)subtitlePanel.SetActive(false);if(Time.time>=noticeUntil)notice.text="";
            if(Time.time>=nextHud){nextHud=Time.time+.15f;RefreshHud();}
        }
        void SurfaceUpdate()
        {
            var p=player.transform.position;
            if(PowerOn&&ZoneRemaining(0)==0&&!TailActive)
            {TailActive=true;nextTail=Time.time;Announce(2,"MISSION CONTROL","Sprinters are pouring out beneath the raised tail. Demolitions, plant C4 on the tail and seal that opening.",12);}
            if(TailActive&&!TailSealed&&Time.time>=nextTail&&ActiveCount<maximumAlive&&ZoneRemaining(7)<EncounterCount(8)){AlertArea(7);Spawn(tailSpawn.position,1,7);nextTail=Time.time+2.4f/(baseDensity*SASCoop.CoopSession.PopulationFor(SASCoop.CoopSession.SquadSize));}
            if(TailSealed&&SASCoop.CoopWorld.AnyPosition(pos=>pos.x>49&&pos.z>-.5f&&pos.y>-.5f,p))foreach(var d in waitingDoors)if(d&&!d.Broken)d.Breach();
            if(TailSealed&&SASCoop.CoopWorld.AnyPosition(pos=>pos.y<-8&&pos.x>70,p))ActivateZone(6);
            bool ready=BoardingAllowed;foreach(var gate in trainBoardingBarriers)if(gate)gate.SetActive(!ready);
            var boarding=player.GetComponent<VaccineBoardingTransfer>();foreach(var d in boarding.train.GetComponentsInChildren<SubwayTrainDoor>())if(d.name=="Side automatic door")d.routeOpen=!ready;
            if(ready&&!boardingAnnounced){boardingAnnounced=true;Announce(5,"MISSION CONTROL","Platforms are clear. Board the intact train on Platform One. Departure is automatic.");}
        }
        void DistrictUpdate()
        {
            foreach(var link in boardingLinks)link.enabled=!train.Travelling;
            if(!train.Touring&&train.StopIndex==3&&!labStarted&&SASCoop.CoopWorld.AnyPosition(pos=>pos.y>-.8f,player.transform.position))
            {labStarted=true;ActivateZone(9);Announce(7,"MISSION CONTROL","Reach the laboratory. Use security to release the main door, then search the four research rooms for the vaccine.",12);}
            var p=player.transform.position;
            if(labStarted&&!BossSpawned&&SASCoop.CoopWorld.AnyPosition(pos=>pos.x>83&&pos.x<128&&pos.z>41&&pos.z<61,p))
            {var boss=Spawn(bossSpawn.position,4,14);if(boss){BossSpawned=true;nextBossReinforcement=Time.time+5;AlertArea(14);hud.SetBoss(boss.Health);Announce(8,"MISSION CONTROL","Heavy infected near the parking enclosure! Regurgitator incoming. Keep moving and bring it down.",10);}}
            UpdateBossReinforcements();
            if(labStarted&&VaccineRecovered&&BossDefeated&&Remaining==0&&!extractionReady){extractionReady=true;StartCoroutine(ArriveExtraction());}
            if(arrivalReady&&!Extracting&&CombatActive)
            {
                var participants=FindObjectsByType<VaccineWalkthrough>().Where(v=>v.gameObject.activeInHierarchy&&v.GetComponent<Vitality>()&&v.GetComponent<Vitality>().Alive).ToArray();
                if(participants.Length>0&&SASCoop.CoopWorld.AllPosition(InsideCraft,player.transform.position))StartCoroutine(Depart(participants));
            }
        }
        public bool InsideCraft(Vector3 point){var p=extractionCraft.transform.InverseTransformPoint(point);return Mathf.Abs(p.x)<2.4f&&p.z>-3.7f&&p.z<3.7f&&p.y>.15f&&p.y<3.5f;}
        public void Arrive(int index)
        {
            if(index<3)
            {
                AlertArea(15);reinforcementTimers.Remove(15);float x=train.train.position.x;for(int i=0;i<EncounterCount(6+index*2);i++){var request=(new Vector3(x+104.5f+(i%6)*8.2f,-12,30+(i/6)%2),i%5==4?2:i%3==0?1:0,15);if(i<InitialCount(6+index*2))queue.Enqueue(request);else reserves.Add(request);}
                Announce(6,"TRAIN CONTROL","Station stop. Infected are boarding. Clear the train before we can continue.",8);
            }
            else{ActivateZone(8);AlertArea(8);Announce(7,"MISSION CONTROL","Final stop: Research District. Clear the platform and take the stairs to the street.",9);}
        }
        void SetPower(bool on)
        {
            PowerOn=on;foreach(var l in stationLights)if(l)l.enabled=on;
            foreach(var r in stationFixtures)if(r){var block=new MaterialPropertyBlock();if(!on){block.SetColor("_BaseColor",new Color(.04f,.045f,.05f));block.SetColor("_EmissionColor",Color.black);}r.SetPropertyBlock(block);}
        }
        public string InteractionPrompt(VaccineInteractable item)
        {
            if(item.medical)return MedicalSupplies.Used(item.medicalKey,MedicalSupplies.LocalId)?"MEDKIT / ALREADY USED":health.Current>=health.maximum?"MEDKIT / HEALTH FULL":"F  Medkit +80 health / one use per player";
            switch(item.action)
            {
                case VaccineUse.Power:return PowerOn?"BACKUP POWER ONLINE":ZoneRemaining(1)>0?"CLEAR THE ELECTRICAL ROOM":"F  Start backup generator";
                case VaccineUse.TailCharge:return !SASCoop.RoleLoadout.CanPlant?"DEMO REQUIRED / FULL SQUAD":TailSealed?"TAIL OPENING SEALED":!TailActive?"C4 POINT / AWAIT CLEARANCE":planting?"PLANTING C4...":"F / HOLD F  Plant C4 on the tail";
                case VaccineUse.Vaccine:return VaccineRecovered?"VACCINE SECURED":"F  Recover vaccine sample";
                default:return Time.time<item.readyAt?"SUPPLY CACHE REPLENISHING":"F  Resupply ammunition";
            }
        }
        public void Use(VaccineInteractable item)
        {
            if(!CombatActive||Paused)return;
            if(item.medical){UseMedical(item,health,MedicalSupplies.LocalId);return;}
            if(item.action==VaccineUse.Power&&!PowerOn&&activated.Contains(1)&&ZoneRemaining(1)==0){SetPower(true);Announce(1,"MISSION CONTROL","Backup power restored. Subway lights are online. Finish clearing the crash courtyard.");}
            else if(item.action==VaccineUse.TailCharge&&TailActive&&!TailSealed&&!planting&&SASCoop.RoleLoadout.CanPlant)StartCoroutine(Demolition(item));
            else if(item.action==VaccineUse.Vaccine&&!VaccineRecovered){VaccineRecovered=true;item.gameObject.SetActive(false);Announce(9,"MISSION CONTROL","Vaccine sample secured. Clear the district and the heavy infected, then reach extraction.");}
            else if(item.action==VaccineUse.Supplies&&Time.time>=item.readyAt){weapons.AddAmmo(36,120);item.readyAt=Time.time+35;Notify("SUPPLIES / +120 RIFLE / +36 PISTOL",4);}
        }
        public bool UseMedical(VaccineInteractable item,Vitality recipient,ulong id)
        {
            if(!CombatActive||Paused||!item.medical)return false;
            bool healed=MedicalSupplies.Take(item.medicalKey,id,recipient);if(healed&&recipient==health)Notify("MEDKIT / +80 HEALTH",3);return healed;
        }
        IEnumerator Demolition(VaccineInteractable point)
        {
            planting=true;float held=0;
            while(held<2.2f){if(Vector3.Distance(player.transform.position,point.transform.position)>7.5f){planting=false;yield break;}held+=Time.deltaTime;Notify("PLANTING C4  "+Mathf.CeilToInt(Mathf.Clamp01(held/2.2f)*100)+"%",1);yield return null;}
            Announce(3,"DEMOLITIONS","Charge armed. Stand clear of the tail.",4);yield return new WaitForSeconds(4);
            CompleteDemolition();
        }
        void CompleteDemolition()
        {
            TailSealed=true;tailSeal.SetActive(true);stationShutter.SetActive(false);planting=false;ActivateZone(2);
            foreach(var a in actors.ToArray())if(a&&a.brain.Health.Alive&&Vector3.Distance(a.transform.position,tailSpawn.position)<10)a.brain.Health.TakeDamage(500);
            if(Vector3.Distance(player.transform.position,tailSpawn.position)<6)health.TakeDamage(35);
            SASCoop.CoopWorld.Blast(tailSpawn.position,6,35);
            var explosion=tailSeal.GetComponent<AudioSource>();if(explosion)explosion.Play();Announce(4,"MISSION CONTROL","Tail opening sealed. Clear the ticket hall, corridor and both platforms. Watch the waiting rooms.",10);
        }
        IEnumerator ArriveExtraction()
        {
            landedPosition=extractionSites[extractionIndex].position;extractionCraft.transform.SetPositionAndRotation(landedPosition+Vector3.up*28,extractionSites[extractionIndex].rotation);extractionCraft.SetActive(true);
            Announce(10,"EXTRACTION PILOT","Landing at ship zone "+(extractionIndex+1)+". Bring the vaccine aboard. We leave when everyone is inside.",12);
            float t=0;while(t<6){t+=Time.deltaTime;extractionCraft.transform.position=Vector3.Lerp(landedPosition+Vector3.up*28,landedPosition,Mathf.SmoothStep(0,1,t/6));yield return null;}
            arrivalReady=true;hud.radar.objective=extractionEntry;
        }
        IEnumerator Depart(VaccineWalkthrough[] participants)
        {
            Extracting=true;weapons.Locked=true;Announce(11,"EXTRACTION PILOT","All personnel aboard. Vaccine secured. Lifting off.",7);
            foreach(var p in participants)p.enabled=false;yield return new WaitForSeconds(2);
            float t=0;while(t<7){var before=extractionCraft.transform.position;t+=Time.deltaTime;extractionCraft.transform.position=landedPosition+new Vector3(0,t*t*1.4f,t*t*.6f);foreach(var p in participants){var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.position+=extractionCraft.transform.position-before;cc.enabled=true;}fade.alpha=Mathf.Clamp01((t-4)/3);yield return null;}
            Victory=true;resultPanel.SetActive(true);resultTitle.text="VACCINE RECOVERED";resultDetail.text="Team extracted / Mission complete\nENTER  Replay from helicopter";player.Capture(false);
        }
        void Fail(){if(Defeated||Victory||SASCoop.CoopSession.Online)return;Defeated=true;weapons.Locked=true;player.enabled=false;player.Capture(false);resultPanel.SetActive(true);resultTitle.text="MISSION FAILED";resultDetail.text="Vaccine recovery team lost\nENTER  Retry from helicopter";}
        public void Restart(){if(SASCoop.CoopSession.Online){SASCoop.CoopSession.Instance.Travel("Vaccine_Blockout",false);return;}savedProgress=false;VaccineRouteState.Pending=false;VaccineRouteState.FromHelicopter=false;Time.timeScale=1;SceneManager.LoadScene("Vaccine_Blockout");}
        public void Notify(string text,float seconds=3){notice.text=text;noticeUntil=Time.time+seconds;}
        public void Announce(int clip,string who,string text,float seconds=8){subtitle.text=who+"\n"+text;subtitlePanel.SetActive(true);messageUntil=Time.time+seconds;if(clip>=0&&clip<messages.Length&&messages[clip]){radio.Stop();radio.clip=messages[clip];radio.Play();}}
        void RefreshHud()
        {
            string objective;
            if(labDistrict)objective=Extracting?"Extraction underway":arrivalReady?"Board aircraft / Ship zone "+(extractionIndex+1):extractionReady?"Extraction aircraft approaching":train.Touring?(train.Travelling?"Remain aboard / Next station":"Repel boarding infected / "+ZoneRemaining(15)+" remaining"):!labStarted?"Clear final platform / Take the stairs":!VaccineRecovered?"Search the laboratory / Recover vaccine":!BossSpawned?"Proceed past the truck":!BossDefeated?"Eliminate the Regurgitator":"Sweep "+NextDistrictSweep+" / "+Remaining+" remaining";
            else objective=!PowerOn?"Clear courtyard / Restore backup power":!TailActive?"Clear crash courtyard / "+ZoneRemaining(0)+" remaining":!TailSealed?"Demolitions / Seal the tail with C4":!activated.Contains(6)?"Clear station / Reach the platforms":!BoardingAllowed?"Clear both platforms and trains / "+Remaining+" remaining":"Board Platform 1 train";
            hud.SetMission("02 / VACCINE",objective,(labDistrict?"RESEARCH DISTRICT":"CRASH SITE")+"  /  "+Kills+" KILLS");hud.timerText.text=$"{(int)elapsed/60:00}:{(int)elapsed%60:00}";
            hud.crosshair.SetActive(player.Captured&&!player.MapVisible&&health.Alive);hud.cursorHint.text=Paused?"PAUSED / ESC TO RESUME":player.MapVisible?"TACTICAL OVERVIEW / TAB TO RETURN":player.Captured?"":"CLICK TO LOOK AROUND";
            hud.zoneText.text=player.area.text.Replace("VACCINE / ","");
            if(!labDistrict)hud.radar.objective=BoardingAllowed?player.GetComponent<VaccineBoardingTransfer>().train:!PowerOn?generator.transform:!TailSealed?tailCharge.transform:null;
            else if(!arrivalReady&&labStarted)hud.radar.objective=!VaccineRecovered?vaccineCases.First(v=>v.gameObject.activeSelf).transform:BossSpawned&&!BossDefeated?bossSpawn:null;
        }
        void OnDestroy(){if(health)health.Died-=Fail;actions?.Dispose();if(Instance==this){Instance=null;EnemyBrain.CanSpawnParasite=null;}Time.timeScale=1;}
    }
}
