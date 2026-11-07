using System.Linq;
using UnityEngine;
using SASCoop;
namespace Vaccine
{
    public sealed partial class VaccineMission
    {
        float nextBossReinforcement;
        public int BossReinforcementsSpawned {get;private set;}
        void UpdateBossReinforcements()
        {
            if(!BossSpawned||BossDefeated||!CombatActive||Time.time<nextBossReinforcement)return;
            nextBossReinforcement=Time.time+4/CoopSession.InitialPopulationFor(CoopSession.SquadSize);
            int cap=Mathf.CeilToInt(8*CoopSession.InitialPopulationFor(CoopSession.SquadSize));
            if(EngagedCount>=maximumAlive||actors.Count(a=>a&&a.BossReinforcement&&a.brain.Health.Alive)>=cap)return;
            var boss=actors.FirstOrDefault(a=>a&&a.brain.Health.Alive&&a.brain.definition.kind==ShipEnemies.EnemyKind.Regurgitator);if(!boss)return;
            var markers=spawns.Where(s=>s.zone==9||s.zone==16||s.zone==11||s.zone==12||s.zone==13).OrderBy(_=>Random.value);
            foreach(var marker in markers)
            {
                if(Vector3.Distance(marker.transform.position,boss.transform.position)>32)continue;
                float roll=Random.value;int kind=roll<.55f?0:roll<.8f?1:roll<.95f?2:3;var prefab=enemyPrefabs[kind];
                if(!FindEncounterPosition(prefab,marker.transform.position,marker.zone,out var safe))continue;
                if(Vector3.Distance(safe,boss.transform.position)>32||CoopWorld.AnyPosition(p=>Vector3.Distance(p,safe)<10,player.transform.position))continue;
                var e=Instantiate(prefab,safe,Quaternion.identity,enemiesRoot);var actor=e.GetComponent<VaccineEnemyActor>();actor.BossReinforcement=true;actor.Bind(this,marker.zone);
                BossReinforcementsSpawned++;break;
            }
        }
    }
}
