#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Onslaught.Mission
{
    public sealed class MissionRestartChecks : MonoBehaviour
    {
        public bool Done;
        public int Passed,Failed;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode routing;
        readonly List<string> rows=new List<string>();
        public static void Begin(){var go=new GameObject("Temporary mission restart checks");DontDestroyOnLoad(go);go.AddComponent<MissionRestartChecks>();}
        void Check(bool ok,string label){if(ok)Passed++;else Failed++;rows.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllText("Artifacts/mission-restart-checks.txt",$"Complete: {Done}; Passed: {Passed}; Failed: {Failed}\n"+string.Join("\n",rows));}
        IEnumerator PressEnter(){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
        IEnumerator Start()
        {
            background=InputSystem.settings.backgroundBehavior;routing=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>();var m=MissionDirector.Instance;Check(m.Phase==MissionPhase.Victory,"Starts from actual completed mission");m.Actions.devices=new InputDevice[]{keyboard};yield return PressEnter();yield return new WaitForSeconds(.5f);m=MissionDirector.Instance;
            Check(m.Phase==MissionPhase.Defense&&m.DefenseElapsed<3&&m.Boss==null&&m.Kills==0,"Enter after victory reloads a fresh four-minute mission");
            Check(m.player.Current==120&&m.weapons.Rounds==30&&m.weapons.Reserve==540&&!m.gates[0].Broken&&m.gates[0].Health.Current==420,"Replay restores player HP, weapon ammo and both gates");
            m.Actions.devices=new InputDevice[]{keyboard};m.player.TakeDamage(99999);int count=m.Spawned;yield return new WaitForSeconds(3.5f);
            Check(m.Phase==MissionPhase.Defeat&&!m.Spawning&&m.Spawned==count&&!m.explorer.enabled&&m.weapons.Locked,"Player death shows defeat, disables combat and stops spawning");
            Check(m.endPanel.activeSelf&&m.endTitle.text=="MISSION FAILED","Defeat provides a visible retry prompt");yield return PressEnter();yield return new WaitForSeconds(.5f);m=MissionDirector.Instance;
            Check(m.Phase==MissionPhase.Defense&&m.DefenseElapsed<3&&m.player.Current==120&&Time.timeScale==1&&!AudioListener.pause,"Enter after defeat restores a playable mission without stale pause or death state");
            Done=true;Check(m.LivingCount==0&&!m.endPanel.activeSelf,"Restart clears enemy registry and previous result panel");Cleanup();
        }
        bool cleaned;
        void Cleanup(){if(cleaned)return;cleaned=true;if(MissionDirector.Instance!=null)MissionDirector.Instance.Actions.devices=null;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=routing;}
        void OnDestroy()=>Cleanup();
    }
}
#endif
