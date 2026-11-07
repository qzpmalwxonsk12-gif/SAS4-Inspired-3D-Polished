#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Vaccine
{
    [DefaultExecutionOrder(-50)]
    public sealed class VaccineSubwayChecks : MonoBehaviour
    {
        const float Level=-12;
        readonly Vector3[] stairPath={new Vector3(61.8f,0,8),new Vector3(78.5f,-4,8),new Vector3(78.5f,-4,13),new Vector3(62.3f,-8,13),new Vector3(62.3f,-8,8),new Vector3(78.5f,-12,8),new Vector3(92,-12,8)};
        public bool Done;
        public int Passed, Failed;
        public string Stage;
        VaccineWalkthrough player;
        VaccineBoardingTransfer boarding;
        bool boardingEnabled;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode routing;
        readonly List<string> lines = new List<string>();
        bool walking, cleaned;
        Vector3 destination;
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play required");
            new GameObject("Temporary subway walkthrough checks").AddComponent<VaccineSubwayChecks>();
        }
        void Save()
        {
            Directory.CreateDirectory("Artifacts/Vaccine/SubwayArt");
            File.WriteAllText("Artifacts/Vaccine/SubwayArt/walkthrough-checks.txt",$"Completed: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n"+string.Join("\n",lines));
        }
        void Check(bool pass,string label) { if(pass)Passed++;else Failed++;lines.Add((pass?"PASS ":"FAIL ")+label);Save(); }
        void Update()
        {
            if(!player||cleaned)return;
            if(!player.MapVisible)player.Capture(true);
            if(walking){var d=destination-player.transform.position;d.y=0;if(d.sqrMagnitude>.01f)player.transform.rotation=Quaternion.LookRotation(d);}
        }
        void Place(Vector3 p)
        {
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=p+Vector3.up*.08f;cc.enabled=true;Physics.SyncTransforms();player.Capture(true);
        }
        IEnumerator Walk(Vector3 point,string label)
        {
            Stage=label;destination=point;walking=true;
            // Measure game time: Editor automation can briefly suspend Play frames during captures.
            float start=Time.time,limit=6+Vector3.Distance(player.transform.position,point)/player.walkSpeed*2;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));
            while(Time.time-start<limit){var d=player.transform.position-point;d.y=0;if(d.magnitude<.55f)break;yield return null;}
            walking=false;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            var diff=player.transform.position-point;diff.y=0;
            Check(diff.magnitude<.9f&&Mathf.Abs(player.transform.position.y-point.y)<.6f,label+" reached "+player.transform.position.ToString("F2"));
        }
        IEnumerator Pause(float seconds){float end=Time.time+seconds;while(Time.time<end)yield return null;}
        IEnumerator TrainDoor(float x,float z,bool south,string name)
        {
            var d=FindObjectsByType<SubwayTrainDoor>().OrderBy(a=>Vector3.Distance(a.transform.position,new Vector3(x,Level+1.35f,z))).First();
            float outside=south?z+2.5f:z-2.5f, inside=south?z-1.1f:z+1.1f;
            yield return Walk(new Vector3(x,Level,outside),name+" approach");yield return Pause(.9f);
            Check(d.OpenAmount>.98f,name+" opens automatically from platform");
            yield return Walk(new Vector3(x,Level,inside),name+" board through open doorway");yield return Pause(1.6f);
            Check(d.OpenAmount>.98f,name+" stays open with player in threshold sensor");
            yield return Walk(new Vector3(x+4,Level,inside),name+" move inside carriage");yield return Pause(2.1f);
            Check(d.OpenAmount<.02f,name+" closes after player moves away");
            yield return Walk(new Vector3(x,Level,inside),name+" return inside doorway");yield return Pause(.8f);
            Check(d.OpenAmount>.98f,name+" opens automatically from inside");
            yield return Walk(new Vector3(x,Level,outside),name+" leave carriage");
        }
        IEnumerator Start()
        {
            player=FindAnyObjectByType<VaccineWalkthrough>();
            boarding=player.GetComponent<VaccineBoardingTransfer>();if(boarding){boardingEnabled=boarding.enabled;boarding.enabled=false;}
            background=InputSystem.settings.backgroundBehavior;routing=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();player.Actions.devices=new InputDevice[]{keyboard,mouse};
            yield return null;player.SetMap(false);
            var doors=FindObjectsByType<SubwayTrainDoor>();
            Check(doors.Length==36,"Six automatic doors on each of six carriages");
            Check(doors.GroupBy(d=>d.transform.parent).All(g=>g.Count()==6&&g.Count(d=>d.name=="Side automatic door")==4&&g.Count(d=>d.name=="End automatic door")==2),"Every carriage has two doors each side and one at each end");
            Check(doors.All(d=>d.actor&&d.leftPanel&&d.rightPanel),"Every automatic door has its actor and both leaves assigned");
            Check(doors.All(d=>d.GetComponentsInChildren<MeshRenderer>().All(r=>r.enabled&&!r.gameObject.isStatic)),"Moving door geometry remains visible and unbatched");
            Check(!GameObject.Find("Landing emergency strip"),"Floating green strip removed");
            Check(Physics.Linecast(new Vector3(32.5f,1.5f,-28.6f),new Vector3(33.5f,1.5f,-28.6f)),"Ticket hall wall gap is sealed");
            Check(Physics.Linecast(new Vector3(87,1,10.65f),new Vector3(90,1,10.65f)),"Wall closes the sky gap above subway entrance");
            var sign=GameObject.Find("CENTRAL TRANSIT  /  M2").transform;
            Check(sign.position.x>6&&sign.position.x<27&&sign.position.z<0,"Transit sign is fully behind the glass wall");
            var floors=FindObjectsByType<BoxCollider>().Where(c=>c.name=="Carriage rubber floor").ToArray();
            Check(floors.Length==6&&floors.All(c=>Mathf.Abs(c.bounds.size.z-8.4f)<.01f),"Both trains have double-width 8.4m carriage floors");
            Check(Mathf.Abs(GameObject.Find("Platform 1 floor").GetComponent<Collider>().bounds.max.y-Level)<.01f,"Platforms sit twelve metres below ground");
            Check(Physics.Linecast(new Vector3(114,Level+1.5f,-7),new Vector3(108,Level+1.5f,-7)),"Maintenance room is physically closed");
            Check(!Physics.BoxCast(new Vector3(157,Level+1.75f,21.6f),new Vector3(.2f,1.65f,4.2f),Vector3.right,Quaternion.identity,53),"Full train-width departure envelope is clear of rubble and walls");
            foreach(var p in new[]{new Vector3(162,Level+1.5f,-7),new Vector3(156,Level+1.5f,-23),new Vector3(156,Level+1.5f,-39)})
                Check(Physics.Linecast(p-Vector3.right*5,p+Vector3.right*8),"Collapse blocks far-end route at "+p);
            foreach(float z in new[]{29.5f,-43f})Check(Physics.Linecast(new Vector3(122,Level+1.5f,z-1),new Vector3(122,Level+1.5f,z+1)),"Track retaining wall contains level at Z="+z);
            Place(stairPath[0]);for(int i=1;i<stairPath.Length;i++)yield return Walk(stairPath[i],"Descend three-flight route "+i);
            yield return Walk(new Vector3(104.5f,Level,11),"Platform 1 boarding approach");
            yield return TrainDoor(104.5f,17.4f,false,"Boarding train");
            yield return Walk(new Vector3(104.5f,Level,21.6f),"Enter first carriage centre aisle");
            yield return Walk(new Vector3(125,Level,21.6f),"Cross first automatic end doors and gangway");
            yield return Walk(new Vector3(145,Level,21.6f),"Cross second automatic end doors and gangway");
            yield return Walk(new Vector3(141.1f,Level,23.5f),"Approach opposite-side carriage door");
            yield return Walk(new Vector3(141.1f,Level,27),"Exit opposite side onto service ledge");
            yield return Walk(new Vector3(141.1f,Level,23.5f),"Re-enter opposite side automatically");
            yield return Walk(new Vector3(141.1f,Level,14.8f),"Cross wide carriage and exit platform side");
            yield return Walk(new Vector3(118,Level,9),"Platform 1 crossing");
            yield return Walk(new Vector3(118,Level,-7),"Pass east of closed maintenance room");
            yield return Walk(new Vector3(118,Level,-23),"Reach Platform 2");
            yield return Walk(new Vector3(130.9f,Level,-23),"Damaged train approach");
            yield return TrainDoor(130.9f,-30.4f,true,"Damaged train");
            yield return Walk(new Vector3(143,Level,-24),"Reach rubble viewing area");
            player.SetMap(true);yield return null;yield return null;
            Check(player.GetComponent<SubwayReviewMap>().Underground&&Mathf.Abs(player.overview.transform.position.x-140)<.1f,"Tab map frames underground level and connecting stairs");
            Check(!player.roofs.activeSelf&&player.GetComponent<SubwayReviewMap>().undergroundLabels.activeSelf,"Underground plan hides ceilings and shows platform labels");
            player.SetMap(false);yield return null;
            yield return Walk(new Vector3(118,Level,-23),"Return along Platform 2");
            yield return Walk(new Vector3(118,Level,9),"Return around maintenance room");
            for(int i=stairPath.Length-1;i>=0;i--)yield return Walk(stairPath[i],"Climb three-flight route "+i);
            player.ResetPosition();player.SetMap(true);yield return null;yield return null;
            Check(Mathf.Abs(player.overview.transform.position.x-15)<.1f,"Surface overview restored after return");
            player.SetMap(false);Done=true;Stage="Complete";Save();Cleanup();
        }
        void Cleanup()
        {
            if(cleaned||!player)return;cleaned=true;walking=false;player.Actions.devices=null;
            if(boarding)boarding.enabled=boardingEnabled;
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=routing;
        }
        void OnDestroy()=>Cleanup();
    }
}
#endif
