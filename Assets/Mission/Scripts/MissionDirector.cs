using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ShipEnemies;
using ShipHUD;

namespace Onslaught.Mission
{
    public enum MissionPhase { Defense, BossApproaching, BossFight, ClearDeck, Sealing, Launching, Victory, Defeat }
    [DefaultExecutionOrder(-100)]
    public sealed partial class MissionDirector : MonoBehaviour
    {
        public static MissionDirector Instance {get;private set;}
        public MissionTuning tuning;
        public Vitality player;
        public ExplorerController explorer;
        public PlayerWeapons weapons;
        public ShipHud hud;
        public DefenseGate[] gates;
        public EnemyBrain[] enemyPrefabs;
        public Transform[] spawnPoints;
        public Transform enemiesRoot,shipRoot;
        public GameObject ammoPickup,medicalPickup;
        public AudioSource intercom,engines;
        public AudioClip intro,bossWarning,clearMessage,launchMessage,alertTone;
        public TMPro.TMP_Text subtitle,notification,endTitle,endDetail,remainingText;
        public GameObject subtitlePanel,endPanel,cinematicBackdrop;
        public CanvasGroup fade;
        public Camera departureCamera;
        public Transform[] retractingRamps;
        public MissionPhase Phase {get;private set;}
        public float DefenseElapsed {get;private set;}
        public float Remaining=>Mathf.Max(0,tuning.defenseSeconds-DefenseElapsed);
        public bool Paused {get;private set;}
        public bool CombatActive=>Phase==MissionPhase.Defense||Phase==MissionPhase.BossApproaching||Phase==MissionPhase.BossFight||Phase==MissionPhase.ClearDeck;
        public bool Spawning=>Phase==MissionPhase.Defense||Phase==MissionPhase.BossApproaching||Phase==MissionPhase.BossFight;
        public EnemyBrain Boss {get;private set;}
        public int Spawned {get;private set;}
        public int Kills {get;private set;}
        public int BossAnnouncements {get;private set;}
        public int LivingCount=>EnemyBrain.Living.Count;
        public InputActionMap Actions {get;private set;}
        float nextSpawn,phaseStarted,subtitleUntil,notificationUntil,nextHud;
        int lane,lastRemaining=-1;
        bool launchStarted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatic()=>Instance=null;
        void Awake()
        {
            Instance=this;Time.timeScale=1;EnemyBrain.CanSpawnParasite=CanAddEnemy;
            Actions=new InputActionMap("Mission");Actions.AddAction("Pause",InputActionType.Button,"<Keyboard>/escape");Actions.AddAction("Retry",InputActionType.Button,"<Keyboard>/enter");Actions.AddAction("Help",InputActionType.Button,"<Keyboard>/f1");
        }
        void OnEnable()=>Actions.Enable();
        void OnDisable()=>Actions?.Disable();
        void OnDestroy(){Actions?.Dispose();if(player!=null)player.Died-=Fail;if(Instance==this){Instance=null;EnemyBrain.CanSpawnParasite=null;}Time.timeScale=1;AudioListener.pause=false;}
        void Start()
        {
            player.maximum=tuning.playerHealth;player.ResetHealth();player.Died+=Fail;
            Phase=MissionPhase.Defense;nextSpawn=Time.time+tuning.firstSpawnDelay;phaseStarted=Time.time;hud.externalClock=true;
            foreach(var gate in gates){gate.Health.maximum=tuning.gateHealth;gate.Health.ResetHealth();gate.RefreshUI();}
            endPanel.SetActive(false);fade.alpha=0;departureCamera.enabled=false;cinematicBackdrop.SetActive(false);
            Announce("CREW INTERCOM", "Hold the boarding ramps for four minutes while we prepare the engines.",intro,8);
            RefreshHud();
        }
        public bool CanAddEnemy()=>Spawning&&LivingCount<AdaptiveLivingLimit-(Boss==null?1:0);
        public void Register(EnemyBrain brain){brain.SetTarget(player);}
        void Update()
        {
            if(Actions["Retry"].WasPressedThisFrame()&&(Phase==MissionPhase.Victory||Phase==MissionPhase.Defeat)){if(SASCoop.CoopSession.Online){SASCoop.CoopSession.Instance.Travel(gameObject.scene.name,false);return;}Time.timeScale=1;AudioListener.pause=false;SceneManager.LoadScene(gameObject.scene.name);return;}
            if(Actions["Pause"].WasPressedThisFrame()&&CombatActive)SetPaused(!Paused);
            if(Actions["Help"].WasPressedThisFrame())hud.ToggleHelp();
            if(Paused)return;
            if(Phase==MissionPhase.Defense)
            {
                DefenseElapsed=Mathf.Min(tuning.defenseSeconds,DefenseElapsed+Time.deltaTime);
                if(DefenseElapsed>=tuning.defenseSeconds)
                {
                    Phase=MissionPhase.BossApproaching;phaseStarted=Time.time;BossAnnouncements++;
                    Announce("CREW INTERCOM", "A powerful infected is approaching. Eliminate it and clear the remaining hostiles so we can take off.",bossWarning,10);
                    RefreshHud();
                }
            }
            UpdateAdaptivePressure();
            if(Phase==MissionPhase.BossApproaching&&Time.time-phaseStarted>=tuning.bossArrivalDelay)SpawnBoss();
            if(Spawning&&Time.time>=nextSpawn)
            {
                float interval=Phase==MissionPhase.Defense?Mathf.Lerp(tuning.initialSpawnInterval,tuning.finalSpawnInterval,DefenseElapsed/tuning.defenseSeconds):tuning.bossSpawnInterval;
                nextSpawn=Time.time+interval/((SASCoop.CoopSession.InitialPopulationFor(SASCoop.CoopSession.SquadSize)+(DefenseElapsed>=6?SASCoop.CoopSession.ReinforcementsFor(SASCoop.CoopSession.SquadSize):0))*AdaptiveSpawnMultiplier);
                if(CanAddEnemy())SpawnRegular();
            }
            if(Phase==MissionPhase.ClearDeck&&LivingCount==0&&!launchStarted)
            {
                if(SASCoop.CoopWorld.AllPosition(p=>p.x<gates[0].transform.position.x-1,player.transform.position))StartCoroutine(Depart());
                else hud.missionObjective.text="Return aboard to launch";
            }
            if(Time.time>=subtitleUntil)subtitlePanel.SetActive(false);
            if(Time.time>=notificationUntil)notification.text="";
            if(Time.time>=nextHud){nextHud=Time.time+.2f;RefreshHud();}
        }
        void SpawnRegular()
        {
            float r=Random.value;int type=0;
            if(DefenseElapsed>135)type=r<.48f?0:r<.73f?1:r<.9f?2:3;
            else if(DefenseElapsed>70)type=r<.6f?0:r<.85f?1:2;
            else if(DefenseElapsed>25)type=r<.8f?0:1;
            Spawn(type,lane++%2);
        }
        public EnemyBrain Spawn(int type,int ramp)
        {
            var prefab=enemyPrefabs[type];var position=spawnPoints[ramp%spawnPoints.Length].position;
            position.z+=Random.Range(-1f,1f);
            var filter=new NavMeshQueryFilter{agentTypeID=prefab.GetComponent<NavMeshAgent>().agentTypeID,areaMask=NavMesh.AllAreas};
            if(!SafeEnemyPlacement.Find(prefab,position,out var safe,4))return null;
            var enemy=Instantiate(prefab,safe,Quaternion.Euler(0,-90,0),enemiesRoot);enemy.SetTarget(player);Spawned++;return enemy;
        }
        void SpawnBoss()
        {
            Boss=Spawn(4,lane++%2);
            if(Boss==null){nextSpawn=Time.time+2;phaseStarted=Time.time;return;}
            Phase=MissionPhase.BossFight;hud.SetBoss(Boss.Health);RefreshHud();
        }
        public void EnemyKilled(EnemyBrain enemy)
        {
            if(!CombatActive)return;Kills++;
            if(enemy==Boss)
            {
                Phase=MissionPhase.ClearDeck;phaseStarted=Time.time;
                Announce("CREW INTERCOM","The heavy infected is down. Clear the remaining hostiles and return aboard. No more are boarding.",clearMessage,9);
            }
            if(Kills%4==0)
            {
                var prefab=ammoPickup;
                if(prefab!=null)Instantiate(prefab,enemy.transform.position+Vector3.up*.35f,Quaternion.identity,shipRoot);
            }
            RefreshHud();
        }
        public void Announce(string speaker,string text,AudioClip clip,float seconds)
        {
            subtitlePanel.SetActive(true);subtitle.text="<color=#EFA847>"+speaker+"</color>\n"+text;subtitleUntil=Time.time+seconds;
            intercom.Stop();if(clip!=null){intercom.clip=clip;intercom.Play();}else if(alertTone!=null)intercom.PlayOneShot(alertTone);
        }
        public void Notify(string message,float seconds){notification.text=message;notificationUntil=Time.time+seconds;}
        public void SetPaused(bool paused)
        {
            if(SASCoop.CoopSession.Online){explorer.Capture(false);return;}Paused=paused;Time.timeScale=paused?0:1;AudioListener.pause=paused;
            explorer.enabled=!paused;weapons.Locked=paused;
            if(paused){endPanel.SetActive(true);endTitle.text="PAUSED";endDetail.text="ESC TO RESUME";}
            else{endPanel.SetActive(false);explorer.Capture(true);}
        }
        void RefreshHud()
        {
            int left=Mathf.CeilToInt(Remaining);
            if(left!=lastRemaining){lastRemaining=left;hud.timerText.SetText("{0:00}:{1:00}",(float)(left/60),(float)(left%60));}
            hud.waveText.text=Phase==MissionPhase.Defense?"DEFENSE  /  HOLD FOR 4 MINUTES":Phase==MissionPhase.BossApproaching?"WARNING  /  HEAVY CONTACT":Phase==MissionPhase.BossFight?"BOSS  /  REINFORCEMENTS ACTIVE":Phase==MissionPhase.ClearDeck?"CLEARANCE  /  SPAWNING STOPPED":Phase.ToString().ToUpperInvariant();
            hud.missionObjective.text=Phase==MissionPhase.Defense?"Defend the boarding ramps":Phase==MissionPhase.BossApproaching?"Heavy infected approaching":Phase==MissionPhase.BossFight?"Eliminate the Regurgitator":Phase==MissionPhase.ClearDeck?(LivingCount>0?"Clear the remaining hostiles":"Return aboard to launch"):Phase==MissionPhase.Sealing?"Sealing the boarding ramps":Phase==MissionPhase.Launching?"Taking off":Phase==MissionPhase.Victory?"Ship secured":"Crew member lost";
            remainingText.SetText("{0:00} HOSTILES   /   {1:000} ELIMINATED",(float)LivingCount,(float)Kills);
        }
        void Fail()
        {
            if(!CombatActive||SASCoop.CoopSession.Online)return;Phase=MissionPhase.Defeat;Paused=false;Time.timeScale=1;AudioListener.pause=false;
            explorer.enabled=false;weapons.Locked=true;weapons.CancelReload();ClearAcid();intercom.Stop();
            endPanel.SetActive(true);endTitle.text="MISSION FAILED";endDetail.text="THE SHIP WAS OVERRUN\nENTER TO RETRY";RefreshHud();
        }
        void ClearAcid(){foreach(var p in FindObjectsByType<AcidProjectile>())Destroy(p.gameObject);foreach(var p in FindObjectsByType<AcidPool>())Destroy(p.gameObject);}
        IEnumerator Depart()
        {
            launchStarted=true;Phase=MissionPhase.Sealing;explorer.SetMap(false);explorer.enabled=false;weapons.Locked=true;weapons.CancelReload();ClearAcid();
            // Bodies and supplies on a withdrawing ramp must not float beside the departing ship.
            foreach(Transform body in enemiesRoot)Destroy(body.gameObject);
            foreach(var supply in FindObjectsByType<SupplyPickup>())Destroy(supply.gameObject);
            Announce("CREW INTERCOM","All hostiles eliminated. Ramps sealing. Brace for departure.",launchMessage,7);
            var positions=new Vector3[retractingRamps.Length];var scales=new Vector3[positions.Length];
            for(int i=0;i<positions.Length;i++){positions[i]=retractingRamps[i].position;scales[i]=retractingRamps[i].localScale;}
            float t=0;
            while(t<tuning.rampSealSeconds)
            {
                t+=Time.deltaTime;float p=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/tuning.rampSealSeconds));
                foreach(var gate in gates)gate.Seal(p);
                for(int i=0;i<positions.Length;i++){var r=retractingRamps[i];r.localScale=new Vector3(scales[i].x*(1-.98f*p),scales[i].y,scales[i].z);r.position=Vector3.Lerp(positions[i],new Vector3(gates[0].transform.position.x+.1f,positions[i].y,positions[i].z),p);}
                yield return null;
            }
            foreach(var gate in gates)gate.Seal(1);
            Phase=MissionPhase.Launching;explorer.eye.enabled=false;explorer.overview.enabled=false;departureCamera.enabled=true;cinematicBackdrop.SetActive(true);
            // The external shot only needs its mission banner, intercom and result overlays.
            foreach(Transform panel in hud.transform.Find("Safe area"))
                if(panel.name!="Mission"&&panel.name!="Intercom subtitles"&&panel.name!="Launch fade"&&panel.name!="Mission result")panel.gameObject.SetActive(false);
            foreach(var surface in FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>())surface.enabled=false;
            engines.Play();Vector3 origin=shipRoot.position;t=0;
            while(t<tuning.flightSeconds)
            {
                t+=Time.deltaTime;float p=Mathf.Clamp01(t/tuning.flightSeconds);shipRoot.position=origin+new Vector3(-48,22,12)*(p*p);engines.volume=Mathf.Lerp(.1f,.55f,p);fade.alpha=Mathf.Clamp01((p-.75f)*4);yield return null;
            }
            Phase=MissionPhase.Victory;engines.Stop();endPanel.SetActive(true);endTitle.text="MISSION COMPLETE";endDetail.text="MANTIS HAS DEPARTED\n"+Kills+" HOSTILES ELIMINATED\nENTER TO PLAY AGAIN";RefreshHud();
        }
    }
}
