using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using ShipEnemies;
using SASCoop;

namespace Vaccine
{
    public sealed partial class VaccineMission
    {
        public VaccineEncounterArea[] encounterAreas = new VaccineEncounterArea[0];
        public Bounds[] forbiddenSpawns = new Bounds[0];
        public float baseDensity = 2.5f;
        readonly HashSet<int> alertedAreas = new HashSet<int>();
        float nextAreaCheck;
        public int SpawnRecoveries { get; private set; }
        readonly List<(Vector3 pos,int kind,int zone)> reserves=new List<(Vector3,int,int)>();
        readonly Dictionary<int,float> reinforcementTimers=new Dictionary<int,float>();
        public int InitialCount(int markers)=>Mathf.CeilToInt(markers*baseDensity*CoopSession.InitialPopulationFor(CoopSession.SquadSize));
        public int ReserveCount(int markers)=>Mathf.CeilToInt(markers*baseDensity*CoopSession.ReinforcementsFor(CoopSession.SquadSize));
        public int EncounterCount(int markers) => InitialCount(markers)+ReserveCount(markers);
        void ReleaseReinforcements()
        {
            foreach(int zone in reserves.Select(r=>r.zone).Distinct().ToArray())
            {
                var area=Area(zone);
                // Waiting rooms are part of the corridor ambush; players need not
                // step inside each tiny room to finish releasing its attackers.
                bool present=zone==15?train&&!train.Travelling:area&&CoopWorld.AnyPosition(p=>area.Contains(p)||(zone>=3&&zone<=5&&Area(2)&&Area(2).Contains(p)),player.transform.position);
                if(!AreaAlerted(zone)||!present)continue;
                if(!reinforcementTimers.TryGetValue(zone,out float due)){reinforcementTimers[zone]=Time.time+6;continue;}
                if(Time.time<due||EngagedCount+queue.Count>=maximumAlive)continue;
                int index=reserves.FindIndex(r=>r.zone==zone);queue.Enqueue(reserves[index]);reserves.RemoveAt(index);reinforcementTimers[zone]=Time.time+1.5f;
            }
        }
        public bool AreaAlerted(int zone) => alertedAreas.Contains(zone);
        public bool AreaPrepared(int zone) => activated.Contains(zone);
        public VaccineEncounterArea Area(int zone) => encounterAreas.FirstOrDefault(a => a && a.zone == (zone==14?16:zone));
        public void AlertArea(int zone) => alertedAreas.Add(zone);
        public bool Forbidden(Vector3 point) => forbiddenSpawns.Any(b => b.Contains(point));

        void UpdateEncounterAreas()
        {
            if (Time.time < nextAreaCheck) return;
            nextAreaCheck = Time.time + .2f;
            foreach (var area in encounterAreas)
            {
                if (!area || (!labDistrict && area.zone >= 2 && area.zone <= 6 && !TailSealed)) continue;
                if (labDistrict && (train.Touring || (area.zone != 8 && !labStarted))) continue;
                if (!CoopWorld.AnyPosition(area.Contains, player.transform.position)) continue;
                AlertArea(area.zone);
                if (area.enterToPopulate) ActivateZone(area.zone);
            }
        }

        // Unvisited district encounters still count toward extraction. Otherwise a
        // player could clear the street and leave with an untouched parking lot.
        int UnpreparedDistrictCount => !labDistrict || !labStarted ? 0 :
            spawns.GroupBy(p=>p.zone).Where(g=>g.Key>=9 && !activated.Contains(g.Key)).Sum(g=>EncounterCount(g.Count()));
        string NextDistrictSweep => spawns.Select(p=>p.zone).Distinct().Where(z=>z>=9 && (!activated.Contains(z)||ZoneRemaining(z)>0))
            .Select(z=>Area(z)).Where(a=>a).Select(a=>a.displayName).FirstOrDefault() ?? "district";

        public bool ReachableSpawn(EnemyBrain prefab, Vector3 point, int zone)
        {
            if (Forbidden(point)) return false;
            var area = Area(zone);
            var filter = new NavMeshQueryFilter { agentTypeID=prefab.GetComponent<NavMeshAgent>().agentTypeID, areaMask=NavMesh.AllAreas };
            var path = new NavMeshPath();
            if (zone == 15)
            {
                if (!train || train.Travelling) return false;
                var p = train.train.InverseTransformPoint(point);
                if (p.x < 98 || p.x > 155 || p.z < 17.5f || p.z > 34 || Mathf.Abs(p.y+12) > .7f) return false;
                return NavMesh.CalculatePath(point, train.train.TransformPoint(new Vector3(118,-12,21.6f)),filter,path)
                    && path.status == NavMeshPathStatus.PathComplete;
            }
            if (!area || !area.Allows(point)) return false;
            foreach (var anchor in area.reachableAnchors)
                if (NavMesh.SamplePosition(anchor,out var hit,.65f,filter) && Mathf.Abs(hit.position.y-anchor.y)<.7f &&
                    NavMesh.CalculatePath(point,hit.position,filter,path) && path.status==NavMeshPathStatus.PathComplete) return true;
            return false;
        }

        // Randomize both the marker order and the position around each marker.
        // Validate body clearance, floor height, encounter bounds and a complete
        // route to an accessible anchor before an enemy is ever instantiated.
        public bool FindEncounterPosition(EnemyBrain prefab, Vector3 requested, int zone, out Vector3 safe,
            Transform ignore=null, bool avoidPlayers=true)
        {
            var area=Area(zone);
            var alternatives=spawns.Where(p=>p.zone==zone).Select(p=>p.transform.position).ToArray();
            float scatter=area?area.scatter:zone==15?2.3f:1;
            bool Accept(Vector3 p) => ReachableSpawn(prefab,p,zone) && (!avoidPlayers ||
                !CoopWorld.AnyPosition(person=>Mathf.Abs(person.y-p.y)<3 && (person-p).sqrMagnitude<9,player.transform.position));
            for(int attempt=0;attempt<12;attempt++)
            {
                var origin=attempt<4||alternatives.Length==0?requested:alternatives[UnityEngine.Random.Range(0,alternatives.Length)];
                var offset=UnityEngine.Random.insideUnitCircle*scatter;
                if(SafeEnemyPlacement.Find(prefab,origin+new Vector3(offset.x,0,offset.y),out safe,1.2f,ignore,false,Accept)) return true;
            }
            // A cramped room can fill temporarily. Try every authored anchor next;
            // the queue retries later when bodies move, never inside a locked room.
            foreach(var origin in alternatives.Concat(area?area.reachableAnchors:new Vector3[0]))
                if(SafeEnemyPlacement.Find(prefab,origin,out safe,2,ignore,false,Accept)) return true;
            safe=requested;return false;
        }

        public bool RecoverEnemy(VaccineEnemyActor actor)
        {
            var e=actor.brain;
            if(!FindEncounterPosition(e,actor.spawnOrigin,actor.zone,out var safe,e.transform)) return false;
            if(!e.Agent.enabled)e.Agent.enabled=true;
            if(!e.Agent.Warp(safe))return false;
            e.Agent.ResetPath();SpawnRecoveries++;return true;
        }
    }
}
