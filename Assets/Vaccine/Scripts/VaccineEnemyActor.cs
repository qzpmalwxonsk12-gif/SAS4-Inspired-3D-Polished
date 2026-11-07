using UnityEngine;
using UnityEngine.AI;
using ShipEnemies;
namespace Vaccine
{
    public sealed class VaccineEnemyActor : MonoBehaviour
    {
        public int zone=-1;public VaccineMission mission;public EnemyBrain brain;
        public bool BossReinforcement;
        public Vector3 spawnOrigin {get;private set;}
        public bool Alerted=>alerted;
        bool bound,alerted;float nextSense,nextRecovery,invalidSince;int hits;
        void Start(){if(!bound)Bind(VaccineMission.Instance,zone<0?(VaccineMission.Instance?VaccineMission.Instance.CurrentCombatZone:0):zone);}
        public void Bind(VaccineMission owner,int area)
        {
            if(bound||!owner||SASCoop.CoopSession.Client)return;bound=true;mission=owner;zone=area;brain=GetComponent<EnemyBrain>();brain.SetTarget(owner.health);
            spawnOrigin=transform.position;nextRecovery=Time.time+3;
            brain.MovementOverride=Guard;brain.Died+=Died;owner.Register(this);
        }
        bool Guard()
        {
            if(!mission||!mission.CombatActive){brain.StopMoving();return true;}
            if(Time.time>=nextSense)
            {
                nextSense=Time.time+.25f;
                bool hurt=brain.Health.Hits>hits;hits=brain.Health.Hits;
                // Entering an area wakes its entire encounter. Shooting into an
                // unentered area is also deliberate engagement; distance is irrelevant.
                if(hurt)mission.AlertArea(zone);
                alerted=BossReinforcement||mission.AreaAlerted(zone);
                if(alerted&&mission.labDistrict&&zone==10)
                    foreach(var door in mission.labDoors)
                        if(door&&!door.Open&&(door.transform.position-transform.position).sqrMagnitude<16)door.Toggle();
            }
            if(Time.time>=nextRecovery)
            {
                nextRecovery=Time.time+2;
                bool invalid=mission.Forbidden(transform.position)||!brain.Agent.isOnNavMesh;
                // A bad nav island must not keep a clearance objective alive forever.
                var area=mission.Area(zone);
                if(area&&area.Allows(transform.position)&&!mission.ReachableSpawn(brain,transform.position,zone))invalid=true;
                if(!invalid)invalidSince=0;
                else if(invalidSince==0)invalidSince=Time.time;
                else if(Time.time-invalidSince>6&&mission.RecoverEnemy(this))invalidSince=0;
            }
            if(!alerted){brain.StopMoving();brain.Play("Idle");return true;}return false;
        }
        void Died(EnemyBrain e){if(mission)mission.EnemyKilled(this);Destroy(gameObject,3.5f);}
        void OnDestroy(){if(brain){brain.Died-=Died;brain.MovementOverride=null;}if(mission)mission.Unregister(this);}
    }
}
