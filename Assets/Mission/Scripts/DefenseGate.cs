using UnityEngine;
using UnityEngine.AI;
using ShipEnemies;
using ShipHUD;

namespace Onslaught.Mission
{
    [RequireComponent(typeof(Vitality))]
    public sealed class DefenseGate : MonoBehaviour
    {
        public string displayName;
        public float width, height=4.05f;
        public Transform bars, blastShutter;
        public BoxCollider barrier;
        public NavMeshObstacle obstacle;
        public AudioSource audioSource;
        public AudioClip clang, collapse;
        public TMPro.TMP_Text status;
        public HudShape healthBar;
        public Vitality Health {get;private set;}
        public bool Broken {get;private set;}
        public bool Sealed {get;private set;}
        public int Hits {get;private set;}
        public static readonly System.Collections.Generic.List<DefenseGate> Active=new System.Collections.Generic.List<DefenseGate>();
        float brokenAt, nextSound;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetRegistry()=>Active.Clear();
        void Awake(){Health=GetComponent<Vitality>();Health.Died+=Break;Health.Damaged+=OnDamage;}
        void OnEnable()=>Active.Add(this);
        void OnDisable()=>Active.Remove(this);
        void OnDestroy(){if(Health!=null){Health.Died-=Break;Health.Damaged-=OnDamage;}}
        void Start()=>RefreshUI();
        public Vector3 AttackPoint(Vector3 actor,float radius)
        {
            Vector3 p=transform.InverseTransformPoint(actor);
            p.x=Mathf.Clamp(p.x,-width*.5f+radius+.12f,width*.5f-radius-.12f);
            p.y=0; p.z=Mathf.Sign(p.z==0?1:p.z)*(radius+.45f);
            return transform.TransformPoint(p);
        }
        public bool IsOutside(Vector3 p)=>p.x>transform.position.x+.05f;
        public void Strike(float amount){if(Broken||Sealed)return;Hits++;Health.TakeDamage(amount);}
        void OnDamage(float damage)
        {
            RefreshUI();if(Time.time>=nextSound){nextSound=Time.time+.18f;if(audioSource!=null&&clang!=null)audioSource.PlayOneShot(clang,.35f);}
        }
        public void ReplicateState(float hp,bool broken){if(broken&&!Broken)Break();Health.Replicate(hp,Health.maximum);RefreshUI();}
        void Break()
        {
            if(Broken)return;Broken=true;brokenAt=Time.time;barrier.enabled=false;obstacle.enabled=false;
            foreach(var c in bars.GetComponentsInChildren<Collider>())c.enabled=false;
            if(audioSource!=null&&collapse!=null)audioSource.PlayOneShot(collapse,.7f);RefreshUI();
        }
        void Update()
        {
            if(Broken && bars!=null)
            {
                float p=Mathf.SmoothStep(0,1,(Time.time-brokenAt)/.65f);
                bars.localRotation=Quaternion.Euler(82*p,0,7*p);bars.localPosition=Vector3.down*(height*.45f*p);
                if(p>=1)bars.gameObject.SetActive(false);
            }
        }
        public void Seal(float fraction)
        {
            if(blastShutter==null)return;
            blastShutter.gameObject.SetActive(true);blastShutter.localPosition=Vector3.up*(height*.5f+(height+.3f)*(1-Mathf.Clamp01(fraction)));
            if(fraction>=1){Sealed=true;barrier.enabled=true;RefreshUI();}
        }
        public void RefreshUI()
        {
            if(status!=null){status.text=Sealed?displayName+"  /  SEALED":Broken?displayName+"  /  BREACHED":displayName+"  /  "+Mathf.CeilToInt(Health.Current)+" HP";status.color=Broken&&!Sealed?HudDrawing.Red:HudDrawing.Paper;}
            if(healthBar!=null){healthBar.SetFill(Sealed?1:Health.Current/Mathf.Max(1,Health.maximum));healthBar.color=Sealed?HudDrawing.Cyan:Health.Current<Health.maximum*.3f?HudDrawing.Red:HudDrawing.Amber;}
        }
    }
}
