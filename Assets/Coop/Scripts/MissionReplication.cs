using UnityEngine;
using SASCoop;
namespace Vaccine
{
    public sealed partial class VaccineMission
    {
        public void CoopFail()=>Fail();
        public void CoopDetonate(){if(TailActive&&!TailSealed&&!planting)StartCoroutine(RemoteDemolition());}
        System.Collections.IEnumerator RemoteDemolition(){planting=true;Announce(3,"DEMOLITIONS","Charge armed. Stand clear of the tail.",4);yield return new WaitForSeconds(4);CompleteDemolition();}
        public void ApplyCoop(CoopWorld.WorldFrame f)
        {
            SetPower(f.power);TailActive=f.tailActive;TailSealed=f.tail;VaccineRecovered=f.vaccine;BossDefeated=f.boss;Extracting=f.extracting;Victory=f.victory;Defeated=f.defeat;
            if(tailSeal)tailSeal.SetActive(f.tail);if(stationShutter)stationShutter.SetActive(!f.tail);foreach(var gate in trainBoardingBarriers)if(gate)gate.SetActive(!f.boarding);
            for(int i=0;i<vaccineCases.Length&&i<f.cases.Length;i++)vaccineCases[i].gameObject.SetActive(f.cases[i]==1);
            if(extractionCraft){var delta=f.craft-extractionCraft.transform.position;extractionCraft.SetActive(f.craftActive);extractionCraft.transform.SetPositionAndRotation(f.craft,f.craftQ);if(f.extracting){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position+=delta;cc.enabled=true;}}
            subtitle.text=f.sub;subtitlePanel.SetActive(f.subVisible);notice.text=f.notice;resultPanel.SetActive(f.result);resultTitle.text=f.resultTitle;resultDetail.text=f.resultDetail;fade.alpha=f.fade;
            if(train)train.ApplyCoop(f.train,f.touring,f.travelling,f.stop,f.timer);
            weapons.Locked=f.defeat||f.victory||f.extracting||!health.Alive;
            if(f.defeat||f.victory)player.Capture(false);
        }
    }
}
namespace Onslaught.Mission
{
    public sealed partial class MissionDirector
    {
        public void CoopFail()=>Fail();
        public void ApplyCoop(CoopWorld.WorldFrame f)
        {
            Phase=(MissionPhase)f.phase;DefenseElapsed=f.timer;
            for(int i=0;i<gates.Length&&i<f.gateHp.Length;i++){gates[i].Health.maximum=tuning.gateHealth;gates[i].ReplicateState(f.gateHp[i],f.gateBroken[i]);if(Phase>=MissionPhase.Sealing&&Phase!=MissionPhase.Defeat)gates[i].Seal(1);}
            shipRoot.position=f.ship;subtitle.text=f.sub;subtitlePanel.SetActive(f.subVisible);notification.text=f.notice;endPanel.SetActive(f.result);endTitle.text=f.resultTitle;endDetail.text=f.resultDetail;fade.alpha=f.fade;
            bool cinematic=Phase==MissionPhase.Launching||Phase==MissionPhase.Victory;departureCamera.enabled=cinematic;cinematicBackdrop.SetActive(cinematic);if(cinematic)explorer.eye.enabled=false;
            weapons.Locked=!CombatActive||!player.Alive;if(!CombatActive)explorer.enabled=false;if(Phase==MissionPhase.Victory||Phase==MissionPhase.Defeat)explorer.Capture(false);
        }
    }
}
