using UnityEngine;
using UnityEngine.AI;
using ShipEnemies;
namespace SASCoop
{
    public static class CoopRevival
    {
        public static bool FindPosition(Vector3 origin,Vitality person,out Vector3 result)
        {
            for(int i=0;i<33;i++)
            {
                float angle=i*Mathf.PI*.618f,radius=i==0?0:.8f+(i%8)*.4f;
                var candidate=origin+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius;
                Vector3 floor;
                if(NavMesh.SamplePosition(candidate,out var hit,1,NavMesh.AllAreas)&&Mathf.Abs(hit.position.y-origin.y)<1.2f)floor=hit.position;
                else if(Physics.Raycast(candidate+Vector3.up,Vector3.down,out var contact,2.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&contact.normal.y>.65f&&!contact.collider.GetComponentInParent<Vitality>())floor=contact.point;
                else continue;
                var p=floor+Vector3.up*.08f;bool clear=true;
                foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.35f,p+Vector3.up*1.45f,.3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    var hp=c.GetComponentInParent<Vitality>();if(hp&&(hp==person||!hp.Alive))continue;
                    clear=false;break;
                }
                if(clear){result=p;return true;}
            }
            result=origin;return false;
        }
    }
}
