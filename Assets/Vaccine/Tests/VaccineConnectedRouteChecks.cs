#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Vaccine
{
    [DefaultExecutionOrder(-50)]
    public sealed class VaccineConnectedRouteChecks : MonoBehaviour
    {
        public bool Done;public int Passed,Failed;public string Stage;
        VaccineWalkthrough p;CharacterController cc;LabDistrictReview tour;
        Keyboard keys;Mouse mouse;bool walking,cleaned;Vector3 target;
        InputSettings.BackgroundBehavior background;InputSettings.EditorInputBehaviorInPlayMode routing;
        readonly List<string> lines=new List<string>();
        const string Output="Artifacts/Vaccine/ConnectedRoute/checks.txt";
        void Check(bool pass,string text){if(pass)Passed++;else Failed++;lines.Add((pass?"PASS ":"FAIL ")+text);Save();}
        void Save(){Directory.CreateDirectory(Path.GetDirectoryName(Output));File.WriteAllText(Output,$"Done: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n"+string.Join("\n",lines));}
        void Bind(){p=FindAnyObjectByType<VaccineWalkthrough>();cc=p.GetComponent<CharacterController>();tour=p.GetComponent<LabDistrictReview>();p.Actions.devices=new InputDevice[]{keys,mouse};p.SetMap(false);}
        void Update(){if(p&&!cleaned){if(!p.MapVisible)p.Capture(true);if(walking){var d=target-p.transform.position;d.y=0;if(d.sqrMagnitude>.01f)p.transform.rotation=Quaternion.LookRotation(d);}}}
        void Place(Vector3 point){p.SetPose(point+Vector3.up*.08f,Quaternion.identity,Quaternion.identity);Physics.SyncTransforms();}
        IEnumerator Walk(Vector3 point,string label)
        {
            Stage=label;Save();target=point;walking=true;float start=Time.time,limit=5+Vector3.Distance(p.transform.position,point)/p.walkSpeed*2;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W,Key.LeftShift));
            while(Time.time-start<limit&&p){var d=p.transform.position-point;d.y=0;if(d.magnitude<.45f)break;yield return null;}
            walking=false;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return null;yield return null;
            if(p){var d=p.transform.position-point;d.y=0;Check(d.magnitude<.8f&&Mathf.Abs(p.transform.position.y-point.y)<.55f,label+" "+p.transform.position.ToString("F2"));}
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);background=InputSystem.settings.backgroundBehavior;routing=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();Bind();yield return null;
            if(!tour)
            {
                Check(Vector3.Distance(p.transform.position,new Vector3(-35,0,-19))<1,"Starts at helicopter");
                foreach(var d in FindObjectsByType<ReviewDoor>())if(!d.Open)d.Toggle();yield return new WaitForSeconds(1);
                var route=GameObject.Find("Review route checkpoints").transform.Cast<Transform>().Select(t=>t.position).ToArray();
                foreach(var point in route)yield return Walk(point,"Helicopter to subway checkpoint");
                yield return Walk(new Vector3(104.5f,-12,12),"Approach boarding train");
                yield return Walk(new Vector3(104.5f,-12,19),"Automatic train boarding");
                Stage="Scene handover";float begin=Time.time;while(SceneManager.GetActiveScene().name!="Vaccine_LabDistrict"&&Time.time-begin<20)yield return null;
                Check(SceneManager.GetActiveScene().name=="Vaccine_LabDistrict","Automatic transition into moving train");
                if(SceneManager.GetActiveScene().name!="Vaccine_LabDistrict"){Done=true;Save();Cleanup();yield break;}
                yield return null;yield return null;Bind();Check(VaccineRouteState.FromHelicopter&&tour.Touring,"Journey continues from helicopter boarding");
                bool[] stopped=new bool[4];float start=Time.time;Vector3 before=p.transform.position;
                while(tour.Touring&&Time.time-start<90)
                {
                    var next="Train journey, next stop "+(tour.StopIndex+2);if(Stage!=next){Stage=next;Save();}
                    if(!tour.Travelling&&tour.StopIndex>=0)stopped[tour.StopIndex]=true;
                    yield return null;
                }
                Check(stopped[0]&&stopped[1]&&stopped[2],"All three intermediate station pauses observed at production timings");
                Check(!tour.Touring&&tour.StopIndex==3,"Fourth station permits exit");
                Check(Vector3.Distance(before,p.transform.position)>400,"Player carried by moving train");
                yield return Walk(new Vector3(-15.5f,-12,24),"Approach final train door");yield return new WaitForSeconds(1);
                yield return Walk(new Vector3(-15.5f,-12,29),"Disembark at fourth station");
                yield return Walk(new Vector3(8,-12,29),"Walk to station exit");
                foreach(var v in new[]{new Vector3(8,-12,33.3f),new Vector3(8,-8,49),new Vector3(15,-8,49),new Vector3(15,-4,32),new Vector3(8,-4,32),new Vector3(8,0,49),new Vector3(10,0,54)})yield return Walk(v,"Climb research station exit");
            }
            // Follow the real controller and Input System across the new exterior and lab.
            foreach(var door in FindObjectsByType<ReviewDoor>())if(!door.Open)door.Toggle();
            yield return Walk(new Vector3(47,0,54),"Cross narrowed road");yield return Walk(new Vector3(49.5f,0,53),"Approach low stairs");
            yield return Walk(new Vector3(49.5f,1.0444f,58.75f),"First five terrace risers");yield return Walk(new Vector3(53.6f,1.0444f,58.75f),"Turn on square landing");
            yield return Walk(new Vector3(57.6f,1.88f,58.75f),"Four upper terrace risers");yield return Walk(new Vector3(57.6f,1.88f,63),"Join raised terrace");
            yield return Walk(new Vector3(55,1.88f,80),"Approach security-locked lab entrance");
            Check(Physics.Linecast(new Vector3(57,3,80),new Vector3(61,3,80),out var hit)&&hit.collider.GetComponentInParent<LabSecurityGate>(),"Main lab door blocks passage before security release");
            yield return Walk(new Vector3(55,1.88f,68),"Return to security entrance");yield return Walk(new Vector3(61.5f,1.88f,68),"Enter security room");yield return Walk(new Vector3(61.5f,1.88f,71.8f),"Approach security control");
            var panel=FindAnyObjectByType<LabSecurityPanel>();p.SetPose(p.transform.position,Quaternion.Euler(0,90,0),Quaternion.identity);p.Capture(true);yield return null;yield return null;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.E));yield return null;yield return null;InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSeconds(1.2f);
            Check(panel.gate.Unlocked&&panel.gate.OpenAmount>.98f,"E at security control opens main lab door");
            foreach(var point in new[]{new Vector3(61.5f,1.88f,68),new Vector3(55,1.88f,68),new Vector3(55,1.88f,80),new Vector3(68,1.88f,80),new Vector3(68,1.88f,94),new Vector3(68,1.88f,80),new Vector3(87,1.88f,80),new Vector3(87,1.88f,94),new Vector3(87,1.88f,80),new Vector3(115,1.88f,80),new Vector3(115,1.88f,94),new Vector3(115,1.88f,80),new Vector3(91,1.88f,80),new Vector3(91,1.88f,69)})yield return Walk(point,"Raised laboratory rooms");
            Check(FindObjectsByType<Transform>().Count(t=>t.name.StartsWith("Terrace lower step")||t.name.StartsWith("Terrace upper step"))==9,"Nine total terrace steps");
            Check(!GameObject.Find("Glass stairwell high roof"),"Terrace open to the night sky");
            Place(new Vector3(132,0,20));yield return Walk(new Vector3(110,0,20),"Ship 1 parking approach");yield return Walk(new Vector3(99,.04f,20),"Enter parking through Ship 1 opening");
            p.SetMap(true);yield return null;Check(p.overview.enabled&&!p.roofs.activeSelf,"Map still works");p.SetMap(false);
            Done=true;Stage="Complete";Save();Cleanup();
        }
        void Cleanup(){if(cleaned)return;cleaned=true;if(p)p.Actions.devices=null;if(keys!=null)InputSystem.RemoveDevice(keys);if(mouse!=null)InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=routing;}
        void OnDestroy()=>Cleanup();
    }
}
#endif

