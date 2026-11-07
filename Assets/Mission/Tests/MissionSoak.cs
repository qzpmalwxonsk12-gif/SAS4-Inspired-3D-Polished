#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEngine;
using ShipEnemies;

namespace Onslaught.Mission
{
    // Runs the unmodified 240-second countdown in real time; only player survival is overridden.
    public sealed class MissionSoak : MonoBehaviour
    {
        public bool Done;
        public int PeakLiving;
        MissionDirector mission;
        float started,gameStarted,nextRecord,firstBreach=-1,warning=-1,bossArrival=-1;
        int frameStarted,maximumProjectiles,maximumPools;
        public static void Begin(){new GameObject("Temporary four-minute soak").AddComponent<MissionSoak>();}
        void Start()
        {
            mission=MissionDirector.Instance;started=Time.realtimeSinceStartup;gameStarted=Time.time;frameStarted=Time.frameCount;
            mission.player.maximum=100000;mission.player.ResetHealth();
            Directory.CreateDirectory("Artifacts");
        }
        void Update()
        {
            if(Done)return;
            PeakLiving=Mathf.Max(PeakLiving,mission.LivingCount);maximumProjectiles=Mathf.Max(maximumProjectiles,AcidProjectile.ActiveCount);maximumPools=Mathf.Max(maximumPools,AcidPool.ActiveCount);
            if(firstBreach<0&&mission.gates.Any(g=>g.Broken))firstBreach=mission.DefenseElapsed;
            if(warning<0&&mission.Phase==MissionPhase.BossApproaching){warning=mission.DefenseElapsed;ScreenCapture.CaptureScreenshot("Artifacts/mission-soak-warning.png");}
            if(bossArrival<0&&mission.Phase==MissionPhase.BossFight)bossArrival=Time.time-gameStarted;
            if(bossArrival>=0&&Time.time-gameStarted>bossArrival+8){Done=true;mission.SetPaused(true);}
            if(Time.realtimeSinceStartup>=nextRecord||Done)
            {
                nextRecord=Time.realtimeSinceStartup+1;
                File.WriteAllText("Artifacts/mission-soak.txt",$"Complete: {Done}\nProduction duration: {mission.tuning.defenseSeconds}\nDefense elapsed: {mission.DefenseElapsed}\nUnscaled wall seconds: {Time.realtimeSinceStartup-started}\nScaled play seconds: {Time.time-gameStarted}\nObserved frames: {Time.frameCount-frameStarted}\nPhase: {mission.Phase}\nWarning at defense second: {warning}\nBoss arrival (seconds since harness start): {bossArrival}\nAnnouncement count: {mission.BossAnnouncements}\nFirst natural gate breach: {firstBreach}\nGate hits: {string.Join(", ",mission.gates.Select(g=>g.Hits))}\nPeak living: {PeakLiving} / {mission.tuning.maximumLiving}\nSpawned: {mission.Spawned}\nPlayer damage received: {mission.player.TotalDamage}\nPeak projectiles/pools: {maximumProjectiles}/{maximumPools}\nSurvival override: player maximum HP only; no clock or spawn-rate changes.\n");
            }
        }
    }
}
#endif
