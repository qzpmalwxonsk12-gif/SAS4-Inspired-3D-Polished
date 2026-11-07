using UnityEngine;
using UnityEngine.AI;

namespace ShipEnemies
{
    // NavMesh sampling alone does not guarantee that a body fits: props may be
    // added after a bake, and SamplePosition can select the top of a cabinet.
    public sealed class SafeEnemyPlacement : MonoBehaviour
    {
        static readonly Collider[] overlaps = new Collider[96];
        EnemyBrain brain;
        Vector3 previous;
        float nextCheck, stalled;
        public int Recoveries { get; private set; }
        public static bool Clear(Vector3 p, float radius, float height, Transform ignore=null, bool actors=true)
        {
            radius=Mathf.Max(.15f,radius);height=Mathf.Max(height,radius*2+.1f);
            int n=Physics.OverlapCapsuleNonAlloc(p+Vector3.up*(radius+.12f),p+Vector3.up*(height-radius),radius,overlaps,Physics.DefaultRaycastLayers&~(1<<8),QueryTriggerInteraction.Ignore);
            if(n==overlaps.Length)return false;
            for(int i=0;i<n;i++)
            {
                var c=overlaps[i];
                if(ignore&&c.transform.IsChildOf(ignore))continue;
                if(!actors&&c.GetComponentInParent<Vitality>())continue;
                // Small floor tolerances must not turn a flat deck into an obstruction.
                if(c.bounds.max.y<=p.y+.1f)continue;
                return false;
            }
            return true;
        }
        public static bool Find(EnemyBrain prefab,Vector3 requested,out Vector3 position,float search=5,Transform ignore=null,bool connected=false,System.Func<Vector3,bool> accept=null)
        {
            var agent=prefab.GetComponent<NavMeshAgent>();
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=NavMesh.AllAreas};
            var path=connected?new NavMeshPath():null;
            for(int i=0;i<97;i++)
            {
                float r=i==0?0:search*Mathf.Sqrt(i/96f),angle=i*2.399963f;
                var candidate=requested+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r);
                if(!NavMesh.SamplePosition(candidate,out var hit,.65f,filter)||Mathf.Abs(hit.position.y-requested.y)>.7f)continue;
                if(!Clear(hit.position,prefab.definition.radius+.06f,prefab.definition.height,ignore))continue;
                if(accept!=null&&!accept(hit.position))continue;
                if(connected&&(!NavMesh.CalculatePath(requested,hit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete))continue;
                position=hit.position;return true;
            }
            position=requested;return false;
        }
        void Start(){brain=GetComponent<EnemyBrain>();previous=transform.position;nextCheck=Time.time+1;}
        void Update()
        {
            if(!brain||brain.displayOnly||!brain.Health.Alive||!brain.Agent.enabled||!brain.Agent.isOnNavMesh||Time.time<nextCheck)return;
            nextCheck=Time.time+1;
            bool embedded=!Clear(transform.position,brain.definition.radius*.8f,brain.definition.height,transform,false);
            bool trying=!brain.Agent.isStopped&&brain.Agent.hasPath&&brain.Agent.remainingDistance>brain.Agent.stoppingDistance+.8f;
            stalled=(embedded||(trying&&(transform.position-previous).sqrMagnitude<.025f))?stalled+1:0;previous=transform.position;
            if(stalled<4)return;
            var actor=GetComponent<Vaccine.VaccineEnemyActor>();
            System.Func<Vector3,bool> allowed=actor&&actor.mission?p=>!actor.mission.Forbidden(p):null;
            if(Find(brain,transform.position,out var safe,2.8f,transform,true,allowed)){brain.Agent.Warp(safe);Recoveries++;}
            brain.Agent.ResetPath();stalled=0;
        }
    }
}
