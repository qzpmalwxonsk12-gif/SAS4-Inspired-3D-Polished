#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ShipEnemies;
using Onslaught;

namespace ShipHUD
{
    // Created only by Editor automation; not saved in either production prefab or preview scene.
    public sealed class HudChecks : MonoBehaviour
    {
        public bool Done;
        public int Passed,Failed;
        public string Stage;
        readonly List<string> results=new List<string>();
        ShipHud hud;HudPreview preview;ExplorerController explorer;Keyboard keyboard;Mouse mouse;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousInput;
        bool changedInput;
        public static void Begin(){if(!Application.isPlaying)throw new System.InvalidOperationException("Play required");new GameObject("Temporary HUD checks").AddComponent<HudChecks>();}
        void Check(bool valid,string label){if(valid)Passed++;else Failed++;results.Add((valid?"PASS ":"FAIL ")+label);Save();}
        void Save(){Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/hud-checks.txt",$"Completed: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n"+string.Join("\n",results));}
        IEnumerator Press(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
        IEnumerator Start()
        {
            hud=FindAnyObjectByType<ShipHud>();preview=FindAnyObjectByType<HudPreview>();explorer=hud.explorer;
            previousBackground=InputSystem.settings.backgroundBehavior;previousInput=InputSystem.settings.editorInputBehaviorInPlayMode;changedInput=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();explorer.Actions.devices=new InputDevice[]{keyboard,mouse};preview.Actions.devices=new InputDevice[]{keyboard,mouse};
            yield return new WaitForSeconds(.3f);preview.ResetPreview();explorer.ResetPosition();
            Stage="Live HUD and scene geometry";hud.radar.RefreshNow();
            Check(Time.frameCount>3,"Player loop advances");
            Check(hud.radar.ContactCount==4,"Four preview enemies detected at their world positions");
            Check(hud.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>g.GetComponent<CanvasRenderer>()!=null),"Every HUD graphic has a CanvasRenderer");
            Check(hud.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget),"Informational HUD never intercepts pointer input");
            Check(hud.GetComponent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay,"HUD uses overlay canvas");
            Check(hud.radar.data.floorTriangles.Length/3==31 && hud.radar.data.wallQuads.Length>=160,"Floorplan baked from ship decks and wall footprints");
            var map=hud.radar;Vector2 o=map.MapPoint(Vector3.zero),x=map.MapPoint(Vector3.right*10),z=map.MapPoint(Vector3.forward*10);
            Check(x.x>o.x&&Mathf.Abs(x.y-o.y)<.01f&&z.y>o.y&&Mathf.Abs(z.x-o.x)<.01f,"Radar preserves world east/right and north/up");
            Check(Mathf.Abs((x-o).magnitude-(z-o).magnitude)<.01f,"Radar uses equal scale on both world axes");
            Check(!FindObjectsByType<Camera>().Any(c=>c.targetTexture!=null),"Minimap needs no additional camera or render texture");
            var actor=FindObjectsByType<HudPreviewActor>().First(a=>a.enabled);
            map.TryContactPoint(actor.transform,out Vector2 initial);yield return new WaitForSeconds(.7f);map.RefreshNow();map.TryContactPoint(actor.transform,out Vector2 moved);
            Check(Vector2.Distance(initial,moved)>.25f,"Enemy pip moves while its animated actor moves");
            Check(Vector2.Distance(moved,map.MapPoint(actor.transform.position))<.01f,"Enemy marker matches real world position");
            actor.gameObject.SetActive(false);yield return null;map.RefreshNow();Check(map.ContactCount==3&&!map.HasContact(actor.transform),"Disabled enemy disappears from radar");
            actor.gameObject.SetActive(true);yield return null;map.RefreshNow();Check(map.ContactCount==4,"Re-enabled enemy returns to radar");

            Stage="Enemy registry and life cycle";
            var probe=new GameObject("Temporary damageable contact");probe.transform.position=new Vector3(5,0,5);var health=probe.AddComponent<Vitality>();var contact=probe.AddComponent<MinimapContact>();
            yield return null;map.RefreshNow();Check(map.ContactCount==5&&map.HasContact(probe.transform),"Newly spawned damageable contact is discovered");
            health.TakeDamage(999);map.RefreshNow();Check(map.ContactCount==4&&!map.HasContact(probe.transform),"Dead contact removed immediately without waiting for Destroy");Destroy(probe);yield return null;
            var realRoot=new GameObject("Temporary registered enemy");realRoot.SetActive(false);realRoot.transform.position=new Vector3(7,0,4);
            realRoot.AddComponent<UnityEngine.AI.NavMeshAgent>().enabled=false;var brain=realRoot.AddComponent<EnemyBrain>();
            brain.definition=UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemies/Definitions/Shambler.asset");realRoot.SetActive(true);yield return null;map.RefreshNow();
            Check(map.ContactCount==5&&map.HasContact(realRoot.transform),"Production EnemyBrain registry is detected without marker component");
            realRoot.AddComponent<MinimapContact>();yield return null;map.RefreshNow();Check(map.ContactCount==5,"EnemyBrain plus marker is not double counted");
            brain.Health.TakeDamage(99999);map.RefreshNow();Check(map.ContactCount==4,"Production enemy death removes its radar marker");Destroy(realRoot);yield return null;

            Stage="Input System and player marker";explorer.ResetPosition();explorer.Capture(true);Vector3 start=explorer.transform.position;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.4f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;map.RefreshNow();
            Check(Vector3.Distance(start,explorer.transform.position)>1,"WASD still moves player with HUD active");
            Check(Vector2.Distance(map.PlayerPoint,map.MapPoint(explorer.transform.position))<.01f,"Player arrow tracks movement");
            float yaw=explorer.transform.eulerAngles.y;InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(150,0));yield return null;yield return null;map.RefreshNow();
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw,map.PlayerYaw))>5,"Mouse look rotates player arrow");
            yield return Press(Key.Tab);hud.RefreshNow();Check(explorer.MapVisible&&!hud.crosshair.activeSelf,"Overhead view remains functional and hides crosshair");
            yield return Press(Key.Tab);hud.RefreshNow();Check(!explorer.MapVisible&&hud.crosshair.activeSelf,"Returning to first person restores crosshair");
            yield return Press(Key.Escape);hud.RefreshNow();Check(!explorer.Captured&&!hud.crosshair.activeSelf&&hud.cursorHint.text.Contains("CLICK"),"Escape releases cursor and shows focus prompt");
            bool help=hud.helpPanel.activeSelf;yield return Press(Key.F1);Check(hud.helpPanel.activeSelf!=help,"F1 toggles control guide");yield return Press(Key.F1);

            Stage="Health, ammunition and boss display states";
            yield return Press(Key.F6);hud.RefreshNow();Check(hud.playerHealth.Current==75&&Mathf.Abs(hud.healthBar.fill-.75f)<.01f,"F6 damage updates numeric health and bar");
            yield return Press(Key.F6);yield return Press(Key.F6);hud.RefreshNow();Check(hud.healthStatus.text=="CRITICAL"&&hud.healthBar.color==HudDrawing.Red,"Low health has text and color warning");
            yield return Press(Key.F6);hud.RefreshNow();Check(hud.healthStatus.text=="VITALS LOST"&&hud.healthBar.fill==0,"Zero health is represented accurately");
            yield return Press(Key.F9);Check(hud.playerHealth.Current==100&&hud.Magazine==30,"F9 restores preview health and ammo");
            yield return Press(Key.F7);Check(hud.Magazine==4&&hud.weaponStatus.text=="LOW AMMO","F7 shows low ammunition warning");
            yield return Press(Key.F7);yield return new WaitForSeconds(.5f);Check(hud.Reloading&&hud.reloadPanel.activeSelf&&hud.ReloadProgress>.1f&&hud.ReloadProgress<1,"Reload progress advances in real Play frames");
            yield return new WaitForSeconds(1.7f);Check(!hud.Reloading&&hud.Magazine==30&&hud.Reserve==150&&!hud.reloadPanel.activeSelf,"Preview reload completes and updates magazine/reserve");
            hud.SetReload(true,.5f);hud.SetWeapon(null,0,0,0);Check(!hud.WeaponBound&&!hud.Reloading&&!hud.reloadPanel.activeSelf&&hud.ammoNumber.text=="--","Unbound weapon clears ammo and ongoing reload UI");
            hud.SetWeapon("TEST",999,30,-5);Check(hud.Magazine==30&&hud.Reserve==0,"Weapon data is clamped at valid bounds");
            yield return Press(Key.F8);yield return null;hud.RefreshNow();map.RefreshNow();Check(hud.bossPanel.activeSelf&&map.ContactCount==5&&map.HasContact(preview.boss.transform),"F8 shows actual boss contact and boss health bar");
            preview.boss.Health.TakeDamage(preview.boss.Health.maximum*.4f);hud.RefreshNow();Check(Mathf.Abs(hud.bossBar.fill-.6f)<.01f,"Boss bar follows actual boss Vitality");
            yield return Press(Key.F8);hud.RefreshNow();map.RefreshNow();Check(!hud.bossPanel.activeSelf&&map.ContactCount==4,"Hiding boss removes bar and marker");

            Stage="Gates and bounded radar buffer";explorer.ResetPosition();var gate=explorer.gates[0];gate.Request(true);yield return new WaitForSeconds(1.2f);map.RefreshNow();Check(gate.IsOpen&&map.OpenGates==1,"Gate state updates on radar");
            gate.Request(false);yield return new WaitForSeconds(1.4f);map.RefreshNow();Check(!gate.IsOpen&&map.OpenGates==0,"Closed gate status returns correctly");
            var contacts=new List<GameObject>();
            for(int i=0;i<140;i++){var p=new GameObject("Temporary radar load "+i);p.transform.position=new Vector3((i%14)*.2f,0,(i/14)*.2f);p.AddComponent<MinimapContact>();contacts.Add(p);}
            map.RefreshNow();Check(map.ContactCount==144&&map.DisplayedCount==HudRadar.Capacity,"Radar buffer stays bounded at 128 with excess contacts");
            preview.ToggleBoss();yield return null;map.RefreshNow();Check(map.HasContact(preview.boss.transform),"Boss marker retains priority when radar is crowded");
            foreach(var p in contacts)Destroy(p);yield return null;preview.ResetPreview();map.RefreshNow();Check(map.ContactCount==4,"Temporary contacts release cleanly");
            explorer.ResetPosition();Cleanup();Done=true;Stage="Complete";Save();
        }
        void Cleanup()
        {
            if(!changedInput)return;changedInput=false;
            if(explorer!=null&&explorer.Actions!=null)explorer.Actions.devices=null;if(preview!=null&&preview.Actions!=null)preview.Actions.devices=null;
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior=previousBackground;InputSystem.settings.editorInputBehaviorInPlayMode=previousInput;
        }
        void OnDestroy()=>Cleanup();
    }
}
#endif
