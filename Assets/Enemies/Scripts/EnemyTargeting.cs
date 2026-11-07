using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace ShipEnemies
{
    // Host-owned threat memory. Damage decays, so one early hit cannot hold aggro forever.
    public sealed class EnemyTargeting:MonoBehaviour
    {
        sealed class Threat { public float damage,at; }
        readonly Dictionary<Vitality,Threat> threats=new Dictionary<Vitality,Threat>();
        EnemyBrain brain;Vitality health;float committedUntil;
        NavMeshPath path;
        void Awake(){path=new NavMeshPath();brain=GetComponent<EnemyBrain>();health=GetComponent<Vitality>();health.DamagedBy+=Record;}
        void OnDestroy(){if(health)health.DamagedBy-=Record;}
        void Record(Vitality person,float damage)
        {
            if(!person||damage<=0)return;
            float previous=RecentDamage(person);threats[person]=new Threat{damage=previous+damage,at=Time.time};
        }
        public float RecentDamage(Vitality person)=>person&&threats.TryGetValue(person,out var t)?t.damage*Mathf.Pow(.5f,(Time.time-t.at)/6):0;
        float Distance(Vitality person)
        {
            var agent=brain.Agent;if(!agent||!agent.enabled||!agent.isOnNavMesh)return float.PositiveInfinity;
            if(!agent.CalculatePath(person.transform.position,path)||path.status!=NavMeshPathStatus.PathComplete)return float.PositiveInfinity;
            float distance=0;var corners=path.corners;for(int i=1;i<corners.Length;i++)distance+=Vector3.Distance(corners[i-1],corners[i]);return distance;
        }
        public Vitality Choose(IEnumerable<Vitality> people)
        {
            var candidates=people.Where(p=>p&&p.Alive&&p.isActiveAndEnabled).Distinct().ToArray();
            if(candidates.Length==0)return null;
            var distances=candidates.ToDictionary(p=>p,Distance);var reachable=candidates.Where(p=>!float.IsPositiveInfinity(distances[p])).ToArray();
            // Before a ramp gate opens there may be no complete route. Gate logic
            // still needs a live target, but a blocked player never beats a reachable one.
            if(reachable.Length==0)return candidates.OrderBy(p=>(p.transform.position-transform.position).sqrMagnitude).First();
            if(brain.definition.kind!=EnemyKind.Regurgitator)return reachable.OrderBy(p=>distances[p]).First();
            float Score(Vitality p)=>.65f*Mathf.Clamp01(RecentDamage(p)/Mathf.Max(1,health.maximum*.08f))+.35f/(1+distances[p]/12);
            var best=reachable.OrderByDescending(Score).First();var current=brain.target;
            bool valid=current&&reachable.Contains(current);
            if(valid&&best!=current)
            {
                // Finish attack windups and commit for four seconds unless the target
                // dies or becomes unreachable. Require a clear threat advantage to switch.
                if(Time.time<committedUntil||brain.State=="Melee"||brain.State=="Ranged"||Score(best)<Score(current)*1.2f+.025f)return current;
            }
            if(best!=current)committedUntil=Time.time+4;
            foreach(var key in threats.Keys.Where(p=>!p||!p.Alive||RecentDamage(p)<.01f).ToArray())threats.Remove(key);
            return best;
        }
    }
}
