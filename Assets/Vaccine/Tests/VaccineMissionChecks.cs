#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ShipEnemies;

namespace Vaccine
{
    public sealed class VaccineMissionChecks : MonoBehaviour
    {
        public bool Done;public int Passed,Failed;public string Stage;
        VaccineMission m;Keyboard keys;Mouse mouse;bool walking;Vector3 target;readonly List<string> lines=new List<string>();
        InputSettings.BackgroundBehavior background;InputSettings.EditorInputBehaviorInPlayMode routing;
        void Check(bool pass,string text){if(pass)Passed++;else Failed++;lines.Add((pass?"PASS ":"FAIL ")+text);Save();}
        void Save(){Directory.CreateDirectory("Artifacts/Vaccine/Gameplay");File.WriteAllText("Artifacts/Vaccine/Gameplay/checks.txt",$"Done: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n"+string.Join("\n",lines));}
        void Bind(){m=VaccineMission.Instance;m.player.Actions.devices=new InputDevice[]{keys,mouse};m.weapons.Actions.devices=new InputDevice[]{keys,mouse};m.health.maximum=10000;m.health.ResetHealth();}
        void Update(){if(!Done&&m&&m.player.enabled){m.player.Capture(true);if(walking){var d=target-m.player.transform.position;d.y=0;if(d.sqrMagnitude>.01f)m.player.transform.rotation=Quaternion.LookRotation(d);}}}
        void Place(Vector3 p,float yaw=0,float pitch=0){m.player.SetPose(p+Vector3.up*.08f,Quaternion.Euler(0,yaw,0),Quaternion.Euler(pitch,0,0));Physics.SyncTransforms();}
        IEnumerator Walk(Vector3 p,string title){Stage=title;Save();walking=true;target=p;float until=Time.time+5+Vector3.Distance(m.player.transform.position,p)/4;InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W,Key.LeftShift));while(m&&m.player&&m.player.enabled&&Time.time<until&&Vector3.Distance(m.player.transform.position,p)>.5f)yield return null;walking=false;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;Check(!m||!m.player||!m.player.enabled||Vector3.Distance(m.player.transform.position,p)<.85f,title);}
        IEnumerator Press(Key key){InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;}
        void KillLiving(){foreach(var e in EnemyBrain.Living.ToArray())if(e&&e.Health.Alive)e.Health.TakeDamage(100000);}
        IEnumerator Clear(){float until=Time.time+40;while(m.ActiveCount+m.Pending>0&&Time.time<until){KillLiving();yield return new WaitForSeconds(.2f);}yield return new WaitForSeconds(.3f);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);background=InputSystem.settings.backgroundBehavior;routing=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();Bind();yield return new WaitForSeconds(8);
            Stage="Surface setup";Check(!m.labDistrict,"Surface mission loaded");Check(m.ZoneRemaining(0)==m.EncounterCount(10),"Courtyard has the scaled infected population");Check(!m.PowerOn&&m.stationLights.All(l=>!l.enabled),"Station starts without power");Check(EnemyBrain.Living.All(e=>!(e.transform.position.x<0&&e.transform.position.z<0)),"Helicopter parking has no zombies");
            Check(m.messages.Length==12&&m.messages.All(c=>c&&c.length>1),"Twelve voiced radio clips assigned");
            int before=m.weapons.Selected;yield return Press(Key.E);Check(m.weapons.Selected!=before,"E switches weapons");yield return Press(Key.E);
            // Tests use accelerated kills only to exercise mission progression, not to claim a normal combat playthrough.
            m.ActivateZone(6);yield return new WaitForSeconds(3);
            Check(EnemyBrain.Living.Where(e=>e.transform.position.y<-2).All(e=>!e.GetComponent<VaccineEnemyActor>().Alerted),"Underground enemies stay dormant while player is above ground");
            var probe=m.Spawn(new Vector3(10,0,8),0,0);Place(new Vector3(10,0,1));yield return new WaitForSeconds(.5f);
            Check(probe&&probe.GetComponent<VaccineEnemyActor>().Alerted,"Same-floor visible player alerts enemy");
            if(probe){float hp=probe.Health.Current;var aim=probe.transform.position+Vector3.up*1.1f-m.player.eye.transform.position;var q=Quaternion.LookRotation(aim);Place(m.player.transform.position-Vector3.up*.08f,q.eulerAngles.y,q.eulerAngles.x);yield return new WaitForSeconds(.4f);InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return new WaitForSeconds(.35f);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;Check(probe.Health.Current<hp,"Rifle raycast damages visible infected");}
            foreach(var d in FindObjectsByType<VaccineBreakableDoor>())if(d.encounter==1)d.Strike(1000);
            yield return Clear();Check(m.ZoneRemaining(1)==0,"Electrical breach activates and clears room");
            m.Use(m.generator);yield return null;Check(m.PowerOn&&m.stationLights.All(l=>l.enabled),"Generator restores station lights");
            Place(m.tailCharge.transform.position-Vector3.up*1.2f+Vector3.back*1.5f);yield return new WaitForSeconds(.5f);Check(m.TailActive,"Courtyard clearance and power activate sprinter breach");
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.F));yield return null;m.Use(m.tailCharge);yield return new WaitForSeconds(2.8f);InputSystem.QueueStateEvent(keys,new KeyboardState());Place(new Vector3(12,0,10));yield return new WaitForSeconds(4.5f);
            Check(m.TailSealed&&m.tailSeal.activeSelf&&!m.stationShutter.activeSelf,"Hold F demolition seals tail and unlocks station");yield return Clear();
            Place(new Vector3(27,0,-12));yield return new WaitForSeconds(.5f);Check(m.ZoneRemaining(2)>0,"Station entry activates hall encounter");
            Place(new Vector3(54,0,1));yield return new WaitForSeconds(.5f);Check(m.waitingDoors.All(d=>d.Broken),"Unbroken waiting-room doors burst at corridor end");
            yield return Clear();Check(m.BoardingAllowed,"All surface and underground enemies cleared before train boarding");
            Place(new Vector3(104.5f,-12,14));yield return Walk(new Vector3(104.5f,-12,19),"Board train through automatic doors");
            Stage="Scene transfer";Save();float deadline=Time.realtimeSinceStartup+25;while(SceneManager.GetActiveScene().name!="Vaccine_LabDistrict"&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(SceneManager.GetActiveScene().name=="Vaccine_LabDistrict","Automatic train scene transfer");
            if(SceneManager.GetActiveScene().name!="Vaccine_LabDistrict"){Finish();yield break;}
            yield return null;yield return null;Bind();yield return new WaitForSeconds(1);Check(m.train.Touring&&m.train.Travelling,"Train journey begins automatically");
            bool[] seen=new bool[3];float started=Time.time;bool sawBoard=false;
            while(m.train.Touring&&Time.time-started<150)
            {
                if(!m.train.Travelling&&m.train.StopIndex<3&&m.train.StopIndex>=0)
                {
                    int i=m.train.StopIndex;
                    if(!seen[i])
                    {
                        seen[i]=true;Stage="Station "+(i+1)+" boarding";Save();yield return new WaitForSeconds(8);
                        sawBoard|=EnemyBrain.Living.Any(e=>e.transform.position.z<25.9f);
                        Check(m.ZoneRemaining(15)>0&&!m.train.Travelling,"Stop "+(i+1)+" holds with live attackers");
                        var pos=m.player.transform.position;Place(new Vector3(pos.x,-12,30));yield return null;yield return null;Check(m.player.transform.position.z<=25.3f,"Intermediate stop prevents player leaving");
                    }
                    yield return Clear();
                }
                yield return null;
            }
            Check(seen.All(x=>x),"All three boarding stops observed");Check(sawBoard,"Infected navigate through doors into moving-train mesh");Check(!m.train.Touring&&m.train.StopIndex==3,"Final station releases player");
            yield return Clear();Place(new Vector3(8,-12,32));yield return Walk(new Vector3(8,0,59),"One continuous final exit flight");yield return Walk(new Vector3(12,0,60),"Exit directly onto road");
            yield return new WaitForSeconds(2);Check(m.ZoneRemaining(9)>0&&!m.AreaAlerted(10),"Exit street activates while lab stays dormant");
            Check(Physics.Linecast(new Vector3(38,1,26),new Vector3(42,1,26)),"Old parking side access closed");
            Check(!Physics.Linecast(new Vector3(104,1,20),new Vector3(108,1,20)),"Ship one parking entrance remains accessible");
            FindAnyObjectByType<LabSecurityGate>().Unlock();foreach(var d in FindObjectsByType<ReviewDoor>())if(!d.Open)d.Toggle();
            var sample=m.vaccineCases.First(v=>v.gameObject.activeSelf);m.Use(sample);Check(m.VaccineRecovered,"Vaccine can be recovered from selected lab room");
            Place(new Vector3(90,0,48));yield return new WaitForSeconds(1);Check(m.BossSpawned&&m.hud.bossHealth,"Passing truck spawns boss and boss HUD");foreach(int zone in new[]{10,11,12,13,16})m.ActivateZone(zone);yield return Clear();
            Check(m.BossDefeated&&m.Remaining==0,"Boss and all district encounters clear");yield return new WaitForSeconds(7);Check(m.extractionCraft.activeSelf,"Extraction arrives after vaccine, boss and clearance");
            Place(m.extractionCraft.transform.position+Vector3.up*.42f);yield return new WaitForSeconds(10);Check(m.Victory,"Boarding extraction completes mission");Check(m.Pending==0,"All queued district spawns eventually succeeded");
            Finish();
        }
        void Finish(){Done=true;Stage="Completed";Save();Cleanup();}
        void Cleanup(){walking=false;InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=routing;if(keys!=null&&keys.added)InputSystem.RemoveDevice(keys);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);}
        void OnDestroy(){if(keys!=null)Cleanup();}
    }
}
#endif
