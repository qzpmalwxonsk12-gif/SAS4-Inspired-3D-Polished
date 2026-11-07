#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Onslaught
{
    // Spawned only by Editor automation; never saved to the map or included in a player build.
    public sealed class WalkthroughChecks : MonoBehaviour
    {
        public bool Done;
        public int Passed, Failed;
        public string Stage;
        ExplorerController explorer;
        Keyboard keyboard;
        Mouse mouse;
        InputSettings.BackgroundBehavior priorBackground;
        InputSettings.EditorInputBehaviorInPlayMode priorEditorInput;
        bool settingsChanged;
        readonly List<string> results = new List<string>();
        public static void Begin()
        {
            if(!Application.isPlaying)throw new System.InvalidOperationException("Play mode required");
            new GameObject("Temporary walkthrough verification").AddComponent<WalkthroughChecks>();
        }
        static Vector3 Point(float x,float y,float height=0) => new Vector3((x-445)*.091f,height,(286-y)*.091f);
        void Check(bool success,string label)
        {
            if(success)Passed++;else Failed++;
            results.Add((success?"PASS ":"FAIL ")+label); Save();
            if(!success){results.Add($"State: captured={explorer.Captured}, position={explorer.transform.position}, controller={explorer.GetComponent<CharacterController>().enabled}, input={explorer.Actions.FindAction("Move").ReadValue<Vector2>()}");Save();}
        }
        void Save()
        {
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/onslaught-checks.txt",$"Completed: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n"+string.Join("\n",results));
        }
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        void Teleport(Vector3 position,float yaw=0)
        {
            var cc=explorer.GetComponent<CharacterController>();cc.enabled=false;
            explorer.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));cc.enabled=true;Physics.SyncTransforms();
        }
        IEnumerator Go(string name,params Vector2[] points)
        {
            Stage=name; Save(); bool reached=true;
            foreach(var p in points)
            {
                var target=Point(p.x,p.y);float deadline=Time.time+7;
                while(Time.time<deadline)
                {
                    Vector3 direction=target-explorer.transform.position;direction.y=0;
                    if(direction.magnitude<.22f)break;
                    explorer.transform.rotation=Quaternion.LookRotation(direction); explorer.Capture(true);
                    if(direction.magnitude<1.2f)Keys(Key.W);else Keys(Key.W,Key.LeftShift);yield return null;
                }
                Keys();yield return null;
                var delta=target-explorer.transform.position;delta.y=0;
                if(delta.magnitude>.4f){results.Add($"Route blocked approaching image {p}: world position {explorer.transform.position}, distance {delta.magnitude:F2} m");reached=false;break;}
                if(explorer.transform.position.y < -1.4f){reached=false;break;}
            }
            Check(reached,name);
        }
        IEnumerator Start()
        {
            explorer=FindAnyObjectByType<ExplorerController>();
            priorBackground=InputSystem.settings.backgroundBehavior;
            priorEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            settingsChanged=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            // Background running alone does not route keyboard events to an unfocused Game view.
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            explorer.Actions.devices=new InputDevice[]{keyboard,mouse};explorer.Capture(true);
            yield return new WaitForSeconds(.3f);
            Check(Time.frameCount>3,"Live Play loop advances");
            Check(explorer.GetComponent<CharacterController>().isGrounded,"Spawn grounded on deck");
            Check(Vector3.Distance(GameObject.Find("Ship layout - 130 percent").transform.localScale,Vector3.one*1.3f)<.001f,"Whole original map scaled exactly 1.3x");
            Check(Mathf.Abs(explorer.GetComponent<CharacterController>().height-1.8f)<.001f,"Explorer remains human sized");
            Vector3 origin=explorer.transform.position;
            Keys(Key.W);yield return new WaitForSeconds(.5f);Keys();yield return null;
            float walk=Vector3.Distance(origin,explorer.transform.position);
            Check(walk>1.7f && walk<3.1f,"WASD action moves explorer");
            Teleport(origin,-90);Keys(Key.W,Key.LeftShift);yield return new WaitForSeconds(.5f);Keys();yield return null;
            Check(Vector3.Distance(origin,explorer.transform.position)>walk*1.3f,"Sprint faster than walking");
            float yaw=explorer.transform.eulerAngles.y;
            InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(100,0));yield return null;yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw,explorer.transform.eulerAngles.y))>4,"Mouse look responds");
            Keys(Key.Escape);yield return null;yield return null;Keys();
            Check(!explorer.Captured,"Escape releases cursor");
            explorer.Capture(true);yield return null;
            Keys(Key.Tab);yield return null;yield return null;Keys();yield return null;
            Check(explorer.MapVisible && explorer.overview.enabled && !explorer.eye.enabled && !explorer.ceiling.activeSelf,"Tab shows overhead camera and hides ceiling");
            Keys(Key.Tab);yield return null;yield return null;Keys();yield return null;
            Check(!explorer.MapVisible && explorer.eye.enabled && explorer.Captured && explorer.ceiling.activeSelf,"Tab returns to first person with ceiling");
            Keys(Key.Enter);yield return null;yield return null;Keys();yield return null;
            Check(Vector3.Distance(explorer.transform.position,origin)<.2f,"Enter resets spawn");
            yield return Go("Central room east opening",new Vector2(340,286));
            yield return Go("Upper doorway and side passage loop",new Vector2(340,145),new Vector2(500,147),new Vector2(550,220),new Vector2(540,286));
            yield return Go("Lower doorway and side passage loop",new Vector2(340,286),new Vector2(340,425),new Vector2(500,420),new Vector2(540,350),new Vector2(540,286));
            yield return Go("Forward room automatic doorway and control consoles",new Vector2(200,287),new Vector2(175,250),new Vector2(150,250),new Vector2(150,268),new Vector2(190,268),new Vector2(200,323),new Vector2(200,287),new Vector2(540,286));
            foreach(var door in FindObjectsByType<ShipDoor>())
            {
                if(!door.automatic)continue;
                Teleport(door.transform.position + door.transform.forward * 2 + Vector3.up*.05f);
                yield return new WaitForSeconds(1.1f);
                Check(door.IsOpen && !door.barrier.enabled && !door.obstacle.enabled,door.name+" opens and clears collision/navigation");
                Teleport(origin);yield return new WaitForSeconds(2.4f);
                Check(!door.IsOpen && door.barrier.enabled && door.obstacle.enabled,door.name+" closes after leaving");
            }
            foreach(var gate in explorer.gates)
            {
                Stage=gate.name;Save();
                Teleport(gate.transform.position-Vector3.right*2.6f+Vector3.up*.05f,90);explorer.Capture(true);
                Keys(Key.W);yield return new WaitForSeconds(.9f);Keys();yield return null;
                Check(explorer.transform.position.x<gate.transform.position.x-.25f,gate.name+" blocks player from ship side");
                Teleport(gate.transform.position+Vector3.right*2.6f+Vector3.down*.2f,-90);
                Keys(Key.W);yield return new WaitForSeconds(.9f);Keys();yield return null;
                Check(explorer.transform.position.x>gate.transform.position.x+.25f,gate.name+" blocks player from ramp side");
                var proxy=new GameObject("Temporary enemy-sized collision proxy");var cc=proxy.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.35f;
                proxy.transform.position=gate.transform.position+Vector3.right*2+Vector3.down*.15f;Physics.SyncTransforms();
                float deadline=Time.time+.8f;while(Time.time<deadline){cc.Move(Vector3.left*4*Time.deltaTime);yield return null;}
                Check(proxy.transform.position.x>gate.transform.position.x+.25f && gate.obstacle.enabled,gate.name+" blocks enemy-sized controller and carves navigation");
                Destroy(proxy);yield return null;
                Teleport(gate.transform.position-Vector3.right*2+Vector3.up*.05f,90);
                Keys(Key.E);yield return null;yield return null;Keys();yield return new WaitForSeconds(.9f);
                Check(gate.IsOpen && !gate.barrier.enabled && !gate.obstacle.enabled,gate.name+" opens through E interaction");
                Teleport(gate.transform.position+Vector3.up*.05f);yield return new WaitForSeconds(.2f);gate.Request(false);yield return new WaitForSeconds(1);
                Check(gate.IsOpen && !gate.barrier.enabled,gate.name+" will not close on an occupying character");
                Teleport(origin);yield return new WaitForSeconds(1);
                Check(!gate.IsOpen && gate.barrier.enabled,gate.name+" closes once the threshold is clear");
                gate.Request(true);yield return new WaitForSeconds(.9f);
            }
            yield return Go("Descend upper boarding ramp",new Vector2(540,230),new Vector2(790,230));
            Check(explorer.transform.position.y<-.6f,"Ramp has descending elevation");
            yield return Go("Enter and leave booth through ramp-facing doorway",new Vector2(740,230),new Vector2(740,287),new Vector2(680,287),new Vector2(740,287));
            yield return Go("Cross between ramps beyond the booth",new Vector2(790,287),new Vector2(790,345));
            yield return Go("Ascend lower ramp back to ship",new Vector2(540,345));
            Check(explorer.transform.position.y>-.15f,"Return to ship deck height");
            Teleport(Point(420,208,.05f),90);explorer.Capture(true);Keys(Key.W);
            yield return new WaitForSeconds(1);Keys();yield return null;
            Check(explorer.transform.position.x<Point(448,208).x-.25f,"Central bulkhead collision blocks walking through wall");
            Teleport(Point(790,230,-1f),90);Keys(Key.W);yield return new WaitForSeconds(.8f);Keys();yield return null;
            Check(explorer.transform.position.x<Point(800,230).x-.2f,"Map stops at ramp end");
            explorer.ResetPosition();Cleanup();Done=true;Stage="complete";Save();
            Debug.Log($"Onslaught walkthrough: {Passed} passed, {Failed} failed.");
        }
        void Cleanup()
        {
            if(explorer!=null && explorer.Actions!=null)explorer.Actions.devices=null;
            if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}
            if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}
            if(settingsChanged)
            {
                InputSystem.settings.backgroundBehavior=priorBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode=priorEditorInput;
                settingsChanged=false;
            }
        }
        void OnDestroy(){Cleanup();}
    }
}
#endif
