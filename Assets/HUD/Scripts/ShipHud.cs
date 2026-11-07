using UnityEngine;
using TMPro;
using Onslaught;
using ShipEnemies;

namespace ShipHUD
{
    public sealed class ShipHud : MonoBehaviour
    {
        public ExplorerController explorer;
        public Vitality playerHealth, bossHealth;
        public HudRadar radar;
        public TMP_Text healthNumber, healthStatus, ammoNumber, reserveNumber, weaponName, weaponStatus;
        public TMP_Text missionTitle, missionObjective, waveText, zoneText, timerText, bossName, bossNumber, cursorHint;
        public HudShape healthBar, ammoBar, reloadBar, bossBar;
        public GameObject bossPanel, reloadPanel, crosshair, helpPanel;
        public CanvasGroup damageFlash;
        public bool externalClock;
        public string firingMode = "PRIMARY  /  AUTO";
        public bool WeaponBound { get; private set; }
        public bool Reloading { get; private set; }
        public int Magazine { get; private set; }
        public int Reserve { get; private set; }
        public int MagazineSize { get; private set; }
        public float ReloadProgress { get; private set; }
        float lastHealth = -1, lastMaximum = -1, flash, nextStatus;
        string currentZone;
        int lastSeconds = -1;
        void Start() { if(!WeaponBound) SetWeapon(null,0,0,0); RefreshNow(); }
        public void SetMission(string title, string objective, string wave)
        { missionTitle.text=title;missionObjective.text=objective;waveText.text=wave; }
        // Integration point for the next gun milestone. The HUD never consumes ammunition itself.
        public void SetWeapon(string title, int rounds, int capacity, int reserve)
        {
            WeaponBound=!string.IsNullOrEmpty(title) && capacity>0;
            if(!WeaponBound){Reloading=false;ReloadProgress=0;reloadPanel.SetActive(false);}
            MagazineSize=Mathf.Max(0,capacity);Magazine=Mathf.Clamp(rounds,0,MagazineSize);Reserve=Mathf.Max(0,reserve);
            weaponName.text=WeaponBound?title:"UNARMED";
            if(WeaponBound) { ammoNumber.SetText("{0:00}",Magazine);reserveNumber.SetText("/ {0:000}",Reserve); }
            else { ammoNumber.text="--";reserveNumber.text="/ ---"; }
            ammoBar.SetFill(WeaponBound?(float)Magazine/MagazineSize:0);
            bool low=WeaponBound && Magazine<=Mathf.Max(1,MagazineSize/5);
            ammoNumber.color=low?HudDrawing.Amber:HudDrawing.Paper;ammoBar.color=low?HudDrawing.Amber:HudDrawing.Paper;
            UpdateWeaponStatus();
        }
        public void SetReload(bool active, float progress)
        {
            Reloading=active && WeaponBound;ReloadProgress=Mathf.Clamp01(progress);
            reloadPanel.SetActive(Reloading);reloadBar.SetFill(ReloadProgress);UpdateWeaponStatus();
        }
        void UpdateWeaponStatus()
        {
            weaponStatus.text=Reloading?"RELOADING":!WeaponBound?"NO WEAPON EQUIPPED":Magazine==0?"MAGAZINE EMPTY":Magazine<=Mathf.Max(1,MagazineSize/5)?"LOW AMMO":firingMode;
            weaponStatus.color=Reloading||Magazine==0&&WeaponBound?HudDrawing.Amber:HudDrawing.Muted;
        }
        public void SetBoss(Vitality value, string title="REGURGITATOR") { bossHealth=value;bossName.text=title;RefreshNow(); }
        public void ToggleHelp() => helpPanel.SetActive(!helpPanel.activeSelf);
        void Update()
        {
            if(Time.unscaledTime>=nextStatus){nextStatus=Time.unscaledTime+.1f;RefreshNow();}
            flash=Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*.8f);
            if(damageFlash != null)damageFlash.alpha=flash;
        }
        public void RefreshNow()
        {
            if(playerHealth != null)
            {
                float current=playerHealth.Current,maximum=playerHealth.maximum;
                if(current!=lastHealth||maximum!=lastMaximum)
                {
                    if(lastHealth>=0 && current<lastHealth)flash=.45f;
                    lastHealth=current;lastMaximum=maximum;
                    float fraction=maximum>0?Mathf.Clamp01(current/maximum):0;
                    healthNumber.SetText("{0:0}<size=17><color=#95A7A4> / {1:0}</color></size>",current,maximum);
                    healthBar.SetFill(fraction);healthBar.color=fraction<=.25f?HudDrawing.Red:HudDrawing.Green;
                    healthStatus.text=current<=0?"VITALS LOST":fraction<=.25f?"CRITICAL":fraction<.65f?"WOUNDED":"STABLE";
                    healthStatus.color=fraction<=.25f?HudDrawing.Red:HudDrawing.Muted;
                }
            }
            else {healthNumber.text="--";healthStatus.text="NO VITALS";healthBar.SetFill(0);lastHealth=lastMaximum=-1;}
            bool boss=bossHealth!=null && bossHealth.isActiveAndEnabled && bossHealth.Alive;
            bossPanel.SetActive(boss);
            if(boss){bossBar.SetFill(bossHealth.maximum>0?bossHealth.Current/bossHealth.maximum:0);bossNumber.SetText("{0:0}%",bossBar.fill*100);}
            if(explorer!=null)
            {
                crosshair.SetActive(!explorer.MapVisible && explorer.Captured);
                cursorHint.text=explorer.MapVisible?"TACTICAL OVERVIEW  /  TAB TO RETURN":explorer.Captured?"":"CLICK TO LOOK AROUND";
                Vector3 p=explorer.transform.position;string zone=p.x>18.5f?"BOARDING RAMPS":p.x<-18?"FLIGHT DECK":p.x<.3f&&Mathf.Abs(p.z)<9.7f?"CARGO HOLD":p.x<4?"SIDE PASSAGE":"MAIN DECK";
                if(zone!=currentZone){currentZone=zone;zoneText.text="01  /  "+zone;}
            }
            int seconds=Mathf.FloorToInt(Time.timeSinceLevelLoad);
            if(!externalClock && seconds!=lastSeconds){lastSeconds=seconds;timerText.SetText("{0:00}:{1:00}",(float)(seconds/60),(float)(seconds%60));}
        }
    }
}
