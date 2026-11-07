#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ShipEnemies;

namespace Onslaught.Mission
{
    // Temporary Editor-only harness, driven through real Play frames and isolated Input System devices.
    public sealed class MissionChecks : MonoBehaviour
    {
        public bool Done;
        public int Passed,Failed;
        public string Stage;
        readonly List<string> results=new List<string>();
        MissionDirector m;PlayerWeapons w;Keyboard keyboard;Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode routing;
        MissionTuning original;
        bool cleaned;
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Begin(){if(!Application.isPlaying)throw new InvalidOperationException("Play required");new GameObject("Temporary mission checks").AddComponent<MissionChecks>();}
        static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
        void Check(bool valid,string text){if(valid)Passed++;else Failed++;results.Add((valid?"PASS ":"FAIL ")+text);Save();}
        void Save(){Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/mission-checks.txt",$"Completed: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n"+string.Join("\n",results));}
        IEnumerator Press(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
        IEnumerator Fire(float seconds){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(seconds);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;}
        // Editor focus changes must not invalidate a virtual-mouse test half-way through a press.
        void Update(){if(m!=null&&Stage=="Mission setup and weapons")m.explorer.Capture(true);}
        void MovePlayer(Vector3 p,float yaw=90,float pitch=0){var c=m.explorer.GetComponent<CharacterController>();c.enabled=false;m.explorer.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));c.enabled=true;Set(m.explorer,"pitch",pitch);m.explorer.Capture(true);Physics.SyncTransforms();}
        IEnumerator Until(Func<bool> condition,float timeout){float start=Time.realtimeSinceStartup;while(!condition()&&Time.realtimeSinceStartup-start<timeout)yield return null;}
        bool Path(int type,int ramp){var f=new NavMeshQueryFilter{agentTypeID=m.enemyPrefabs[type].GetComponent<NavMeshAgent>().agentTypeID,areaMask=-1};var p=new NavMeshPath();return NavMesh.SamplePosition(m.spawnPoints[ramp].position,out var a,2,f)&&NavMesh.SamplePosition(new Vector3(10,0,5),out var b,2,f)&&NavMesh.CalculatePath(a.position,b.position,f,p)&&p.status==NavMeshPathStatus.PathComplete;}
        IEnumerator Start()
        {
            m=MissionDirector.Instance;w=m.weapons;original=m.tuning;m.tuning=Instantiate(original);Set(m,"nextSpawn",float.PositiveInfinity);
            background=InputSystem.settings.backgroundBehavior;routing=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();var devices=new InputDevice[]{keyboard,mouse};m.Actions.devices=devices;w.Actions.devices=devices;m.explorer.Actions.devices=devices;
            m.player.maximum=100000;m.player.ResetHealth();yield return null;
            Stage="Mission setup and weapons";
            Check(original.defenseSeconds==240,"Production defense duration is exactly 240 seconds");
            Check(original.maximumLiving==28,"Combined enemy budget is bounded at 28");
            Check(m.gates.All(g=>g.Health.maximum==420&&!g.Broken),"Both barred gates begin at 420 HP");
            Check(m.explorer.gates.Length==0&&!m.explorer.allowPositionReset,"E has no legacy gate action; Enter cannot teleport during combat");
            Check(m.player.receivesEnemyAttacks&&m.player.gameObject.layer==0,"Player collider participates in enemy melee and acid hits");
            foreach(var clip in new[]{m.intro,m.bossWarning,m.clearMessage,m.launchMessage})if(clip!=null)clip.LoadAudioData();yield return null;
            Check(new[]{m.intro,m.bossWarning,m.clearMessage,m.launchMessage}.All(c=>c!=null&&c.length>2&&c.loadState==AudioDataLoadState.Loaded),"All four voiced intercom messages can load");
            MovePlayer(new Vector3(10,.08f,5));yield return new WaitForSeconds(.35f);
            Check(w.Selected==1&&w.Rounds==30&&w.Reserve==540,"Mission starts with a loaded rifle and its own reserve");
            int shots=w.ShotsFired;yield return Fire(.55f);Check(w.ShotsFired-shots>=3,"Holding LMB fires rifle repeatedly");int rifleRounds=w.Rounds,rifleReserve=w.Reserve;
            yield return Press(Key.E);yield return new WaitForSeconds(.3f);Check(w.Selected==0&&w.Rounds==12&&w.Reserve==120,"E equips pistol with separate magazine and reserve");
            shots=w.ShotsFired;yield return Fire(.55f);Check(w.ShotsFired-shots==1,"Holding LMB fires pistol only once");
            yield return Press(Key.R);yield return new WaitForSeconds(.3f);Check(w.Reloading&&m.hud.ReloadProgress>0,"R starts animated reload and HUD progress");
            yield return Press(Key.E);Check(w.Selected==1&&!w.Reloading&&w.Rounds==rifleRounds&&w.Reserve==rifleReserve,"E cancels reload without duplicating ammo and restores rifle rounds");
            yield return new WaitForSeconds(.3f);yield return Press(Key.R);yield return new WaitForSeconds(2.1f);Check(w.Rounds==30&&w.Reserve==rifleReserve-(30-rifleRounds),"Rifle reload transfers only missing rounds from reserve");
            yield return Press(Key.E);yield return new WaitForSeconds(.3f);Check(w.Rounds==11&&w.Reserve==120,"Cancelled pistol reload preserved its exact ammunition");yield return Press(Key.R);yield return new WaitForSeconds(1.5f);Check(w.Rounds==12&&w.Reserve==119,"Pistol reload completes with correct reserve");
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));yield return new WaitForSeconds(.5f);Check(m.explorer.eye.fieldOfView<61,"RMB narrows aim view");InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.4f);
            var target=m.Spawn(0,0);target.displayOnly=true;target.Agent.enabled=false;target.transform.position=new Vector3(15,0,5);MovePlayer(new Vector3(10,.08f,5),90,7);yield return new WaitForSeconds(.35f);float hp=target.Health.Current;yield return Fire(.1f);Check(Mathf.Abs(hp-target.Health.Current-34)<.01f,"Pistol body hits deal 34 damage through real raycast");
            MovePlayer(new Vector3(10,.08f,5),90,0);yield return new WaitForSeconds(.3f);yield return Fire(.1f);Check(!target.Health.Alive,"Pistol head hit deals double damage and kills damaged Shambler");Check(w.Hits>=2,"Weapon hit marker counts confirmed enemy hits");
            int reserve=w.Reserve;Instantiate(m.ammoPickup,m.player.transform.position,Quaternion.identity);yield return new WaitForSeconds(.15f);Check(w.Reserve==Mathf.Min(w.Current.maximumReserve,reserve+24),"Walking over ammo supplies refills pistol reserve");m.player.TakeDamage(50);float health=m.player.Current;Instantiate(m.medicalPickup,m.player.transform.position,Quaternion.identity);yield return new WaitForSeconds(.15f);Check(m.player.Current==health+30,"Walking over medkit restores 30 HP");
            Stage="Pause, map and barred gates";
            yield return Press(Key.Escape);float elapsed=m.DefenseElapsed;yield return new WaitForSecondsRealtime(.4f);Check(m.Paused&&Time.timeScale==0&&m.DefenseElapsed==elapsed&&w.Locked,"Escape pauses timer and weapon actions");yield return Press(Key.Escape);Check(!m.Paused&&Time.timeScale==1&&m.explorer.enabled&&m.explorer.Captured,"Escape resumes play and mouse look");
            yield return Press(Key.Tab);Check(m.explorer.MapVisible&&!w.mount.gameObject.activeSelf,"Tab tactical map hides first-person weapon");yield return Press(Key.Tab);
            var gate=m.gates[0];int holes=0,bars=0;for(int i=0;i<40;i++){Vector3 from=gate.transform.TransformPoint(new Vector3(-gate.width*.4f+i*gate.width*.8f/39,1.6f,-1));Vector3 to=gate.transform.TransformPoint(new Vector3(-gate.width*.4f+i*gate.width*.8f/39,1.6f,1));if(Physics.Linecast(from,to,Physics.DefaultRaycastLayers&~(1<<8),QueryTriggerInteraction.Ignore))bars++;else holes++;}
            Check(holes>0&&bars>0,"Physical bars stop bullets while gaps allow shooting through");
            Check(Physics.Linecast(gate.transform.position+Vector3.left*2+Vector3.up,gate.transform.position+Vector3.right*2+Vector3.up,1<<8),"Continuous gate collider blocks player movement");
            Check(!Path(0,0)&&!Path(0,1),"Closed barred gates block navigation from both ramps");
            var shambler=m.Spawn(0,0);var sprinter=m.Spawn(1,1);float gateHP=gate.Health.Current;
            yield return Until(()=>gate.Hits>0&&m.gates[1].Hits>0,14);Check(gate.Health.Current<gateHP&&m.gates[1].Hits>0,"Real enemies approach and damage both gates on melee windup");
            gate.Health.TakeDamage(9999);m.gates[1].Health.TakeDamage(9999);yield return new WaitForSeconds(.8f);
            Check(m.gates.All(g=>g.Broken&&!g.barrier.enabled&&!g.obstacle.enabled&&!g.bars.gameObject.activeSelf),"Destroyed gates collapse and release collision and navigation barriers");
            foreach(int type in new[]{0,3,4,5})for(int r=0;r<2;r++)Check(Path(type,r),m.enemyPrefabs[type].name+" has a complete route from ramp "+r+" after breach");
            MovePlayer(new Vector3(13,.08f,5));yield return Until(()=>m.player.Hits>1,14);Check(shambler.transform.position.x<18&&m.player.Hits>1,"Breached-gate attacker enters ship and damages player");
            foreach(var e in EnemyBrain.Living.ToArray())Destroy(e.gameObject);yield return null;
            var spitter=m.Spawn(2,0);spitter.Agent.Warp(new Vector3(17,.02f,5));float acidHP=m.player.Current;yield return Until(()=>spitter.RangedAttacks>0&&m.player.Current<acidHP,8);Check(spitter.RangedAttacks>0&&m.player.Current<acidHP,"Spitter projectile/pool damages the actual player collider");Destroy(spitter.gameObject);yield return null;
            Stage="Four-minute transition and full mission ending";
            // Accelerated only in this isolated test; a separate natural-time soak verifies the production timer.
            m.tuning.defenseSeconds=m.DefenseElapsed+.5f;Set(m,"nextSpawn",Time.time+.1f);yield return Until(()=>m.Phase==MissionPhase.BossApproaching,3);
            Check(m.BossAnnouncements==1&&m.subtitle.text.Contains("powerful infected")&&m.intercom.clip==m.bossWarning,"Deadline announces powerful infected once with voice and subtitles");
            yield return Until(()=>m.Phase==MissionPhase.BossFight,6);Check(m.Boss!=null&&m.Boss.Health.maximum==3600&&m.hud.bossPanel.activeSelf,"Regurgitator enters with 3600 HP and live boss bar");
            int spawned=m.Spawned;yield return new WaitForSeconds(3.5f);Check(m.Spawned>spawned&&m.Spawning,"Reinforcements continue during the boss fight");
            var bloater=m.Spawn(3,0);m.Boss.Health.TakeDamage(99999);int afterBoss=m.Spawned;Check(m.Phase==MissionPhase.ClearDeck&&!m.Spawning&&!m.CanAddEnemy(),"Boss death immediately stops every spawner");
            yield return new WaitForSeconds(3.5f);Check(m.Spawned==afterBoss&&m.Phase==MissionPhase.ClearDeck&&m.LivingCount>0,"Remaining enemies prevent departure and no reinforcements arrive");
            bloater.Health.TakeDamage(9999);Check(bloater.ParasitesCreated==0,"Bloater death cannot spawn parasites after boss death");
            MovePlayer(new Vector3(25,-.5f,5));foreach(var e in EnemyBrain.Living.ToArray())e.Health.TakeDamage(99999);yield return new WaitForSeconds(.5f);Check(m.Phase==MissionPhase.ClearDeck&&m.LivingCount==0,"Evacuation waits for player to leave the ramps and return aboard");
            MovePlayer(new Vector3(10,.08f,5));yield return Until(()=>m.Phase==MissionPhase.Sealing,2);Check(m.Phase==MissionPhase.Sealing&&w.Locked&&!m.explorer.enabled,"Clear deck and player aboard begin safe ramp closure");
            Vector3 ship=m.shipRoot.position;yield return Until(()=>m.Phase==MissionPhase.Launching,5);Check(m.gates.All(g=>g.Sealed&&g.barrier.enabled)&&m.departureCamera.enabled,"Both ramps seal before switching to exterior departure view");
            yield return new WaitForSeconds(2);Check(Vector3.Distance(ship,m.shipRoot.position)>1,"Ship actually moves away from dock during takeoff");ScreenCapture.CaptureScreenshot("Artifacts/mission-takeoff.png");
            yield return Until(()=>m.Phase==MissionPhase.Victory,8);Check(m.Phase==MissionPhase.Victory&&m.endPanel.activeSelf&&m.endTitle.text=="MISSION COMPLETE","Departure ends with mission-complete result and replay prompt");
            Done=true;Stage="Complete";Save();Cleanup();
        }
        void Cleanup()
        {
            if(cleaned)return;cleaned=true;
            if(m!=null){m.Actions.devices=null;m.explorer.Actions.devices=null;w.Actions.devices=null;if(original!=null){var clone=m.tuning;m.tuning=original;Destroy(clone);}}
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=routing;
        }
        void OnDestroy()=>Cleanup();
    }
}
#endif
