using UnityEngine;
using UnityEngine.InputSystem;
using ShipEnemies;
using ShipHUD;

namespace Onslaught.Mission
{
    [DefaultExecutionOrder(100)]
    public sealed class PlayerWeapons : MonoBehaviour
    {
        public ExplorerController explorer;
        public Vaccine.VaccineWalkthrough vaccine;
        Camera Eye=>vaccine?vaccine.eye:explorer.eye;
        Transform Owner=>vaccine?vaccine.transform:explorer.transform;
        bool Captured=>vaccine?vaccine.Captured:explorer.Captured;
        bool MapVisible=>vaccine?vaccine.MapVisible:explorer.MapVisible;
        bool ControllerEnabled=>vaccine?vaccine.enabled:explorer.enabled;
        Vector2 MoveInput=>vaccine?vaccine.Actions["Move"].ReadValue<Vector2>():explorer.Actions.FindAction("Move").ReadValue<Vector2>();
        public Vitality health;
        public ShipHud hud;
        public WeaponSpec[] specifications;
        public Transform mount;
        public GameObject[] models;
        public Transform[] slides,magazines,muzzles;
        public GameObject flash,impact;
        public LineRenderer tracer;
        public AudioSource audioSource;
        public CanvasGroup hitMarker;
        public UnityEngine.UI.Graphic hitGraphic;
        public int gateMovementLayer=8;
        public InputActionMap Actions {get;private set;}
        public int Selected {get;private set;}=1;
        public int Rounds=>rounds[Selected];
        public int Reserve=>reserves[Selected];
        public bool Reloading {get;private set;}
        public int ShotsFired {get;private set;}
        public int Hits {get;private set;}
        public bool Locked {get;set;}
        readonly int[] rounds=new int[2],reserves=new int[2];
        readonly RaycastHit[] shotHits=new RaycastHit[16];
        readonly Vector3[] slidePositions=new Vector3[2],magazinePositions=new Vector3[2];
        float nextShot,reloadStarted,switchUntil,flashUntil,impactUntil,hitUntil,kick,slideKick;
        Vector3 mountPosition;
        Quaternion mountRotation;
        HudShape weaponIcon;
        bool initialized;
        public WeaponSpec Current=>specifications[Selected];
        void Awake()
        {
            Actions=new InputActionMap("Combat");
            Actions.AddAction("Fire",InputActionType.Button,"<Mouse>/leftButton");
            Actions.AddAction("Aim",InputActionType.Button,"<Mouse>/rightButton");
            Actions.AddAction("Reload",InputActionType.Button,"<Keyboard>/r");
            Actions.AddAction("Switch",InputActionType.Button,"<Keyboard>/e");
        }
        void OnEnable()=>Actions.Enable();
        void OnDisable(){Actions?.Disable();CancelReload();if(flash!=null)flash.SetActive(false);if(tracer!=null)tracer.enabled=false;}
        void OnDestroy()=>Actions?.Dispose();
        void Start()
        {
            SASCoop.RoleLoadout.Apply(this);
            mountPosition=mount.localPosition;mountRotation=mount.localRotation;
            weaponIcon=hud.transform.Find("Safe area/Weapon/Rifle schematic").GetComponent<HudShape>();
            for(int i=0;i<2;i++){rounds[i]=specifications[i].magazineSize;reserves[i]=specifications[i].startingReserve;slidePositions[i]=slides[i].localPosition;magazinePositions[i]=magazines[i].localPosition;}
            initialized=true;Select(1);flash.SetActive(false);impact.SetActive(false);tracer.enabled=false;
        }
        public void Select(int index)
        {
            if(index<0||index>=2)return;CancelReload();Selected=index;switchUntil=Time.time+.28f;
            for(int i=0;i<models.Length;i++)models[i].SetActive(i==Selected);
            UpdateHud();
        }
        void UpdateHud()
        {
            hud.firingMode=Current.penetration>1?"SHOTGUN / PENETRATING":Selected==0?"PISTOL / SEMI AUTO":SASCoop.RoleLoadout.Selected==0?"SMG / AUTOMATIC":"RIFLE / AUTOMATIC";
            hud.SetWeapon(Current.displayName,rounds[Selected],Current.magazineSize,reserves[Selected]);
            if(weaponIcon!=null){weaponIcon.style=Current.automatic?HudShape.Style.Weapon:HudShape.Style.Pistol;weaponIcon.SetVerticesDirty();}
        }
        public void AddAmmo(int pistol,int rifle)
        {reserves[0]=Mathf.Min(specifications[0].maximumReserve,reserves[0]+pistol);reserves[1]=Mathf.Min(specifications[1].maximumReserve,reserves[1]+rifle);UpdateHud();}
        public void CancelReload(){Reloading=false;if(hud!=null)hud.SetReload(false,0);}
        public bool BeginReload()
        {
            if(Locked||!health.Alive||Reloading||Rounds>=Current.magazineSize||Reserve<=0||Time.time<switchUntil)return false;
            SASCoop.CoopWorld.Request("reload",weapon:Selected);
            Reloading=true;reloadStarted=Time.time;hud.SetReload(true,0);
            if(Current.reload!=null)audioSource.PlayOneShot(Current.reload,.5f);return true;
        }
        void Update()
        {
            if(!initialized)return;
            bool active=!Locked&&health.Alive&&ControllerEnabled&&Captured&&!MapVisible&&Time.timeScale>0;
            mount.gameObject.SetActive(!Locked&&!MapVisible&&health.Alive);
            if(active)
            {
                if(Actions["Switch"].WasPressedThisFrame())Select(1-Selected);
                if(Actions["Reload"].WasPressedThisFrame())BeginReload();
                bool fire=Current.automatic?Actions["Fire"].IsPressed():Actions["Fire"].WasPressedThisFrame();
                if(fire)TryFire();
            }
            if(Reloading)
            {
                float progress=(Time.time-reloadStarted)/Current.reloadSeconds;hud.SetReload(true,progress);
                if(progress>=1){int count=Mathf.Min(Current.magazineSize-Rounds,Reserve);rounds[Selected]+=count;reserves[Selected]-=count;CancelReload();UpdateHud();}
            }
            flash.SetActive(Time.time<flashUntil && active);tracer.enabled=Time.time<flashUntil && !Locked;
            impact.SetActive(Time.time<impactUntil && !Locked);hitMarker.alpha=Time.time<hitUntil?1:0;
        }
        public bool TryFire()
        {
            if(!initialized||Locked||!health.Alive||!Captured||MapVisible||Reloading||Time.time<nextShot||Time.time<switchUntil||Time.timeScale<=0)return false;
            if(Rounds<=0){BeginReload();return false;}
            rounds[Selected]--;ShotsFired++;nextShot=Time.time+1/Current.shotsPerSecond;
            var eye=Eye.transform;Vector3 direction=eye.forward;
            bool aimed=Actions["Aim"].IsPressed();float spread=aimed?.0008f:.003f;
            Vector2 noise=Random.insideUnitCircle*spread;direction=(direction+eye.right*noise.x+eye.up*noise.y).normalized;
            var ray=new Ray(eye.position,direction);Vector3 end=ray.GetPoint(Current.range);
            if(SASCoop.CoopWorld.Request("shot",weapon:Selected,origin:ray.origin,direction:ray.direction))
            {if(TryGetShotHit(ray,out var clientHit)){end=clientHit.point;var enemy=clientHit.collider.GetComponentInParent<EnemyBrain>();if(enemy){Hits++;hitUntil=Time.time+.14f;hitGraphic.color=HudDrawing.Paper;}}}
            else
            {
                int before=EnemyBrain.Living.Count;end=SASCoop.CoopWorld.Hit(ray,Current.damage,Current.penetration,Owner);
                if(TryGetShotHit(ray,out var hit)&&hit.collider.GetComponentInParent<EnemyBrain>()){Hits++;hitUntil=Time.time+.14f;hitGraphic.color=EnemyBrain.Living.Count<before?HudDrawing.Red:HudDrawing.Paper;}
                SASCoop.CoopWorld.LocalShot(ray.origin,end);
            }
            impact.transform.position=end;impactUntil=Time.time+.09f;
            flash.transform.SetPositionAndRotation(muzzles[Selected].position,muzzles[Selected].rotation);flashUntil=Time.time+.045f;
            tracer.SetPosition(0,muzzles[Selected].position);tracer.SetPosition(1,end);tracer.enabled=true;
            kick=Mathf.Min(4,kick+Current.recoilDegrees);slideKick=1;
            if(Current.shot!=null)audioSource.PlayOneShot(Current.shot,.65f);UpdateHud();return true;
        }
        bool TryGetShotHit(Ray ray,out RaycastHit closest)
        {
            int mask=Physics.DefaultRaycastLayers&~(1<<gateMovementLayer);
            int count=Physics.RaycastNonAlloc(ray,shotHits,Current.range,mask,QueryTriggerInteraction.Ignore);
            var hits=shotHits;
            // NonAlloc results are unordered and may omit the nearest hit when full.
            // Keep normal shots allocation-free, but never shoot through cover on overflow.
            if(count==shotHits.Length)
            {
                hits=Physics.RaycastAll(ray,Current.range,mask,QueryTriggerInteraction.Ignore);
                count=hits.Length;
            }
            closest=default;
            float distance=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var candidate=hits[i];
                // Movement can put the camera behind its own physics capsule for this query.
                // Exclude the owner here; its collider must still receive enemy attacks.
                if(candidate.collider.transform.IsChildOf(Owner)||candidate.distance>=distance)continue;
                closest=candidate;distance=candidate.distance;
            }
            return distance<float.PositiveInfinity;
        }
        void LateUpdate()
        {
            if(!initialized||Locked||Time.timeScale<=0)return;
            kick=Mathf.MoveTowards(kick,0,Time.deltaTime*9);slideKick=Mathf.MoveTowards(slideKick,0,Time.deltaTime*14);
            bool aiming=Actions["Aim"].IsPressed()&&!Reloading&&Captured;
            Eye.fieldOfView=Mathf.Lerp(Eye.fieldOfView,aiming?58:75,Time.deltaTime*12);
            float progress=Reloading?Mathf.Clamp01((Time.time-reloadStarted)/Current.reloadSeconds):0;
            float dip=Reloading?Mathf.Sin(progress*Mathf.PI):Time.time<switchUntil?.65f:0;
            Vector2 input=MoveInput;float bob=input.magnitude*Mathf.Sin(Time.time*10)*.006f;
            mount.localPosition=mountPosition+new Vector3(aiming?-.12f:0,bob-dip*.18f,-kick*.015f);
            mount.localRotation=mountRotation*Quaternion.Euler(-kick*2+dip*24,dip*-8,dip*15);
            for(int i=0;i<2;i++){slides[i].localPosition=slidePositions[i]-Vector3.forward*(i==Selected?slideKick*.045f:0);magazines[i].localPosition=magazinePositions[i]-Vector3.up*(i==Selected?dip*.2f:0);}
            if(Captured&&!MapVisible)Eye.transform.localRotation*=Quaternion.Euler(-kick*.35f,0,0);
        }
        public int[] SaveAmmunition()=>new[]{Selected,rounds[0],rounds[1],reserves[0],reserves[1]};
        public void RestoreAmmunition(int[] data){if(data==null||data.Length!=5)return;for(int i=0;i<2;i++){rounds[i]=Mathf.Clamp(data[i+1],0,specifications[i].magazineSize);reserves[i]=Mathf.Clamp(data[i+3],0,specifications[i].maximumReserve);}Select(data[0]);}
    }
}
