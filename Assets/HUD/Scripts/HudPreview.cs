using UnityEngine;
using UnityEngine.InputSystem;
using ShipEnemies;

namespace ShipHUD
{
    // Explicit display-state driver, deliberately absent from the reusable HUD prefab.
    public sealed class HudPreview : MonoBehaviour
    {
        public ShipHud hud;
        public EnemyBrain boss;
        public InputActionMap Actions {get;private set;}
        int ammoState;
        float reloadStarted=-1;
        void Awake()
        {
            Actions=new InputActionMap("HUD preview");
            Actions.AddAction("Help",InputActionType.Button,"<Keyboard>/f1");
            Actions.AddAction("Damage",InputActionType.Button,"<Keyboard>/f6");
            Actions.AddAction("Ammo",InputActionType.Button,"<Keyboard>/f7");
            Actions.AddAction("Boss",InputActionType.Button,"<Keyboard>/f8");
            Actions.AddAction("Reset",InputActionType.Button,"<Keyboard>/f9");
        }
        void OnEnable() => Actions.Enable();
        void OnDisable() => Actions?.Disable();
        void OnDestroy() => Actions?.Dispose();
        System.Collections.IEnumerator Start() {yield return null;ResetPreview();}
        void Update()
        {
            if(Actions["Help"].WasPressedThisFrame())hud.ToggleHelp();
            if(Actions["Damage"].WasPressedThisFrame())hud.playerHealth.TakeDamage(25);
            if(Actions["Ammo"].WasPressedThisFrame())CycleAmmo();
            if(Actions["Boss"].WasPressedThisFrame())ToggleBoss();
            if(Actions["Reset"].WasPressedThisFrame())ResetPreview();
            if(reloadStarted>=0)
            {
                float p=(Time.unscaledTime-reloadStarted)/2;
                hud.SetReload(true,p);
                if(p>=1){reloadStarted=-1;hud.SetReload(false,1);hud.SetWeapon("RIFLE  /  PREVIEW",30,30,150);}
            }
        }
        public void CycleAmmo()
        {
            if(reloadStarted>=0)return;
            ammoState=(ammoState+1)%3;
            if(ammoState==1){hud.SetReload(false,0);hud.SetWeapon("RIFLE  /  PREVIEW",4,30,180);}
            else if(ammoState==2){hud.SetWeapon("RIFLE  /  PREVIEW",0,30,180);reloadStarted=Time.unscaledTime;hud.SetReload(true,0);}
            else {hud.SetReload(false,0);hud.SetWeapon(null,0,0,0);}
        }
        public void ToggleBoss()
        {
            boss.gameObject.SetActive(!boss.gameObject.activeSelf);
            if(boss.gameObject.activeSelf){boss.GetComponent<Vitality>().ResetHealth();hud.SetBoss(boss.GetComponent<Vitality>());}
            else hud.SetBoss(null);
        }
        public void ResetPreview()
        {
            reloadStarted=-1;ammoState=0;hud.playerHealth.ResetHealth();hud.SetReload(false,0);hud.SetWeapon("RIFLE  /  PREVIEW",30,30,180);
            boss.gameObject.SetActive(false);hud.SetBoss(null);hud.RefreshNow();
        }
    }
}
