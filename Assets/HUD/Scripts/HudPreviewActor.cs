using UnityEngine;
using ShipEnemies;

namespace ShipHUD
{
    // Only used in HUDPreview: animate a short clear route without running combat or baking another NavMesh.
    public sealed class HudPreviewActor : MonoBehaviour
    {
        public Vector3 start, end;
        public float speed=1;
        public EnemyBrain brain;
        bool toEnd=true;
        void Update()
        {
            if(brain!=null&&brain.Health!=null&&!brain.Health.Alive)return;
            Vector3 target=toEnd?end:start,delta=target-transform.position;
            if(delta.sqrMagnitude<.01f){toEnd=!toEnd;return;}
            transform.position=Vector3.MoveTowards(transform.position,target,speed*Time.deltaTime);
            if(delta.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*6);
            if(brain!=null)brain.Play("Move");
        }
    }
}
