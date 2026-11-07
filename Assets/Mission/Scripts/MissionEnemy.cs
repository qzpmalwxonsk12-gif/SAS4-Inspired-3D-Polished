using UnityEngine;
using UnityEngine.AI;
using ShipEnemies;

namespace Onslaught.Mission
{
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class MissionEnemy : MonoBehaviour
    {
        EnemyBrain brain;
        MissionDirector mission;
        DefenseGate gate;
        float nextPath,nextHit,attackStarted;
        bool attacking,impacted;
        NavMeshPath queryPath;
        float nextChoice;bool usingBreach;Vector3 breachPoint;int gateState=-1;
        public DefenseGate TargetGate=>gate;
        public bool UsingOpenBreach=>usingBreach;
        public int GateImpacts {get;private set;}
        void Awake()
        {
            queryPath=new NavMeshPath();brain=GetComponent<EnemyBrain>();mission=MissionDirector.Instance;
            if(mission!=null&&!SASCoop.CoopSession.Client){brain.SetTarget(mission.player);brain.MovementOverride=TickGate;brain.Died+=OnDeath;mission.Register(brain);}
        }
        void OnDestroy(){if(brain!=null){brain.MovementOverride=null;brain.Died-=OnDeath;}}
        void OnDeath(EnemyBrain e){if(mission!=null)mission.EnemyKilled(e);}
        bool TickGate()
        {
            var agent=brain.Agent;
            if(mission==null)return false;
            if(!mission.CombatActive){if(agent.enabled&&agent.isOnNavMesh)agent.isStopped=true;return true;}
            int state=0;for(int i=0;i<mission.gates.Length;i++)if(mission.gates[i].Broken)state|=1<<i;
            if(Time.time>=nextChoice||state!=gateState||(gate!=null&&!gate.IsOutside(transform.position)))
            {
                nextChoice=Time.time+.4f;gateState=state;ChooseRoute();
            }
            if(usingBreach){attacking=false;agent.stoppingDistance=.2f;MoveTo(breachPoint);return true;}
            if(gate==null){attacking=false;agent.stoppingDistance=Mathf.Max(.25f,brain.definition.meleeRange*.78f);return false;}
            agent.stoppingDistance=.08f;
            var dfn=brain.definition;Vector3 destination=gate.AttackPoint(transform.position,dfn.radius);
            // Keep the gate attack on this actor's side; bars can be hit along their full width.
            Vector3 delta=destination-transform.position;delta.y=0;
            if(attacking)
            {
                float elapsed=Time.time-attackStarted;
                if(!impacted&&elapsed>=dfn.meleeWindup){impacted=true;if(!gate.Broken&&delta.magnitude<1.3f){gate.Strike(dfn.meleeDamage);GateImpacts++;}}
                if(elapsed>=dfn.meleeDuration)attacking=false;
                return true;
            }
            if(delta.magnitude<.45f)
            {
                if(agent.enabled&&agent.isOnNavMesh)agent.isStopped=true;
                Vector3 facing=Vector3.left;
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(facing),dfn.turnSpeed*Time.deltaTime);
                if(Time.time>=nextHit){attacking=true;impacted=false;attackStarted=Time.time;nextHit=Time.time+Mathf.Max(dfn.meleeDuration,dfn.meleeCooldown);brain.Play("Melee",true);}
                else brain.Play("Idle");
            }
            else if(agent.enabled&&agent.isOnNavMesh)
            {
                agent.isStopped=false;
                if(Time.time>=nextPath){nextPath=Time.time+.3f;agent.SetDestination(destination);}
                brain.Play(agent.velocity.sqrMagnitude>.02f?"Move":"Idle");
            }
            return true;
        }
        float PathDistance(Vector3 point,out Vector3 destination)
        {
            var agent=brain.Agent;destination=point;
            if(!agent.enabled||!agent.isOnNavMesh)return float.PositiveInfinity;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            if(!NavMesh.SamplePosition(point,out var hit,2,filter))return float.PositiveInfinity;
            destination=hit.position;
            if(!agent.CalculatePath(destination,queryPath)||queryPath.status!=NavMeshPathStatus.PathComplete)return float.PositiveInfinity;
            var corners=queryPath.corners;float length=0;for(int i=1;i<corners.Length;i++)length+=Vector3.Distance(corners[i-1],corners[i]);return length;
        }
        void ChooseRoute()
        {
            var old=gate;gate=null;usingBreach=false;
            // Reconsider the player before any destructible obstacle, including after another gate breaks.
            if(!float.IsPositiveInfinity(PathDistance(brain.target?brain.target.transform.position:mission.player.transform.position,out _))){attacking=false;return;}
            float best=float.PositiveInfinity;
            foreach(var g in mission.gates)
                if(g.Broken&&g.IsOutside(transform.position))
                {
                    float distance=PathDistance(g.transform.position+Vector3.left*(brain.definition.radius+1.5f),out var point);
                    if(distance<best){best=distance;breachPoint=point;usingBreach=true;}
                }
            if(usingBreach){attacking=false;return;}
            best=float.PositiveInfinity;
            foreach(var g in mission.gates)
                if(!g.Broken&&g.IsOutside(transform.position))
                {
                    float distance=PathDistance(g.AttackPoint(transform.position,brain.definition.radius),out _);
                    if(distance<best){best=distance;gate=g;}
                }
            if(gate!=old)attacking=false;
        }
        void MoveTo(Vector3 point)
        {
            var a=brain.Agent;if(!a.enabled||!a.isOnNavMesh)return;a.isStopped=false;
            if(Time.time>=nextPath){nextPath=Time.time+.25f;a.SetDestination(point);}
            brain.Play(a.velocity.sqrMagnitude>.02f?"Move":"Idle");
        }
    }
}
