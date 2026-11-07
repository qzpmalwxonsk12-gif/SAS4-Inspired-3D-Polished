using UnityEngine;
using ShipHUD;
using ShipEnemies;

namespace Vaccine
{
    [DefaultExecutionOrder(300)]
    public sealed class VaccineMissionHud : MonoBehaviour
    {
        public VaccineMission mission;public HudMapData surfaceMap,undergroundMap;public HudMapGraphic mapGraphic;
        HudMapData liveMap;float next;HudMapData source;
        readonly RaycastHit[] castHits=new RaycastHit[32];
        void Start(){liveMap=Instantiate(surfaceMap);mission.hud.radar.data=liveMap;mapGraphic.data=liveMap;mission.hud.radar.maximumHeightDifference=4;}
        void LateUpdate()
        {
            var p=mission.player;
            if(!p.Captured||p.MapVisible||!mission.CombatActive||mission.Paused){if(p.prompt)p.prompt.text="";return;}

            var eye=p.eye.transform;
            VaccineInteractable bestInteract=null;
            LabSecurityPanel bestPanel=null;
            ReviewDoor bestDoor=null;
            VaccineBreakableDoor bestBreakable=null;

            // 1. Multi-hit raycast + spherecast along view ray that ignores enemies & player colliders
            int hitCount=Physics.SphereCastNonAlloc(eye.position,.38f,eye.forward,castHits,5.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float bestDist=float.PositiveInfinity;
            for(int i=0;i<hitCount;i++)
            {
                var col=castHits[i].collider;
                if(!col||col.transform.IsChildOf(p.transform)||col.GetComponentInParent<EnemyBrain>()||col.GetComponentInParent<Vitality>())continue;
                float d=castHits[i].distance;
                var vi=col.GetComponentInParent<VaccineInteractable>();
                if(vi&&d<bestDist){bestInteract=vi;bestPanel=null;bestDoor=null;bestBreakable=null;bestDist=d;continue;}
                var sp=col.GetComponentInParent<LabSecurityPanel>();
                if(sp&&d<bestDist){bestPanel=sp;bestInteract=null;bestDoor=null;bestBreakable=null;bestDist=d;continue;}
                var bd=col.GetComponentInParent<VaccineBreakableDoor>();
                if(bd&&!bd.Broken&&d<bestDist){bestBreakable=bd;bestInteract=null;bestPanel=null;bestDoor=null;bestDist=d;continue;}
                var rd=col.GetComponentInParent<ReviewDoor>();
                if(rd&&!rd.GetComponent<VaccineBreakableDoor>()&&d<bestDist){bestDoor=rd;bestInteract=null;bestPanel=null;bestBreakable=null;bestDist=d;}
            }

            // 2. Forgiving proximity + view-cone fallback so C4, Medkits, Generator, Vaccine, and Security Panel are effortless to use even in a zombie swarm
            if(!bestInteract&&!bestPanel)
            {
                float bestScore=-999f;
                foreach(var vi in FindObjectsByType<VaccineInteractable>(FindObjectsSortMode.None))
                {
                    if(!vi||!vi.gameObject.activeInHierarchy)continue;
                    Vector3 targetPos=vi.transform.position;
                    float maxRange=vi.action==VaccineUse.TailCharge?5.8f:4.6f;
                    float dist=Vector3.Distance(p.transform.position+Vector3.up*1.1f,targetPos);
                    if(dist>maxRange)continue;
                    Vector3 toTarget=(targetPos-eye.position).normalized;
                    float dot=Vector3.Dot(eye.forward,toTarget);
                    // Allow interaction if within generous cone OR standing right next to it (<2.4m)
                    if(dot<.15f&&dist>2.4f)continue;
                    float score=dot*2f-dist*.35f+(vi.action==VaccineUse.TailCharge&&mission.TailActive&&!mission.TailSealed?.8f:0);
                    if(score>bestScore){bestScore=score;bestInteract=vi;}
                }
                if(!bestInteract)
                {
                    foreach(var sp in FindObjectsByType<LabSecurityPanel>(FindObjectsSortMode.None))
                    {
                        if(!sp||!sp.gameObject.activeInHierarchy)continue;
                        float dist=Vector3.Distance(p.transform.position+Vector3.up*1.1f,sp.transform.position+Vector3.up*1.2f);
                        if(dist>4.5f)continue;
                        float dot=Vector3.Dot(eye.forward,(sp.transform.position+Vector3.up*1.2f-eye.position).normalized);
                        if(dot>.15f||dist<2.2f){bestPanel=sp;break;}
                    }
                }
            }

            bool pressed=p.Actions["Interact"].WasPressedThisFrame();
            bool held=p.Actions["Interact"].IsPressed();

            if(bestInteract)
            {
                p.prompt.text=bestInteract.Prompt;
                if(pressed||(bestInteract.action==VaccineUse.TailCharge&&held))bestInteract.Use();
            }
            else if(bestPanel)
            {
                p.prompt.text=bestPanel.gate.Unlocked?"Lab access released":"F  Release main lab door";
                if(pressed)bestPanel.Activate();
            }
            else if(bestBreakable&&!bestBreakable.Broken)
            {
                p.prompt.text="SHOOT TO BREACH / "+Mathf.CeilToInt(bestBreakable.health)+" HP";
            }
            else if(bestDoor)
            {
                p.prompt.text="F  "+(bestDoor.Open?"Close ":"Open ")+bestDoor.roomName;
                if(pressed)bestDoor.Toggle();
            }
            else p.prompt.text="";

            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.2f;var selected=p.transform.position.y<-2?undergroundMap:surfaceMap;
            if(source!=selected){source=selected;liveMap.floorVertices=source.floorVertices;liveMap.floorTriangles=source.floorTriangles;liveMap.wallQuads=source.wallQuads;}
            var center=new Vector2(p.transform.position.x,p.transform.position.z);liveMap.minimum=center-Vector2.one*35;liveMap.maximum=center+Vector2.one*35;mapGraphic.SetVerticesDirty();mission.hud.radar.RefreshNow();
        }
        void OnDestroy(){if(liveMap)Destroy(liveMap);}
    }
}
