using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Vaccine
{
    // Environment preview only. Encounter pacing and multiplayer are separate mission systems.
    [DefaultExecutionOrder(200)]
    public sealed class LabDistrictReview : MonoBehaviour
    {
        public VaccineWalkthrough player;
        public Transform train;
        public GameObject[] stations;
        public SubwayTrainDoor[] trainDoors;
        public float stopSeconds=8, travelSpeed=28;
        public bool Touring { get; private set; }
        public int StopIndex { get; private set; }=3;
        public bool Travelling { get; private set; }
        public float Remaining { get; private set; }
        public System.Func<bool> DepartureAllowed;
        public System.Action<int> ReachedStop;
        readonly float[] stops={-600,-450,-300,-120};
        CharacterController body;
        Vector3 mapPosition;
        float mapSize;
        Transform sensor;
        Transform trainRoofs;
        void Start()
        {
            body=player.GetComponent<CharacterController>();mapPosition=player.overview.transform.position;mapSize=player.overview.orthographicSize;trainRoofs=train.Find("Train roofs");
            sensor=new GameObject("Train door preview sensor").transform;sensor.SetParent(transform);sensor.position=new Vector3(0,-1000,0);
            Station(3);SetDoorActors(player.transform);
            if(VaccineRouteState.Pending)
            {
                VaccineRouteState.Pending=false;BeginJourney(VaccineRouteState.Aboard,VaccineRouteState.Facing,VaccineRouteState.View);
            }
        }
        void SetDoorActors(Transform actor){foreach(var door in trainDoors)door.actor=door.name=="End automatic door"?player.transform:actor;}
        void Place(Vector3 point){body.enabled=false;player.transform.position=point;body.enabled=true;Physics.SyncTransforms();}
        void Station(int index){for(int i=0;i<stations.Length;i++)stations[i].SetActive(i==index);}
        public static Vector3 ClampTrainLocal(Vector3 p, bool touring)
        {
            if (touring)
            {
                p.x = Mathf.Clamp(p.x, 100.6f, 153f);
                p.y = Mathf.Clamp(p.y, -11.92f, -9.5f);
                bool inGangway1 = p.x > 116.75f && p.x < 118.55f;
                bool inGangway2 = p.x > 135.05f && p.x < 136.85f;
                if (inGangway1 || inGangway2)
                    p.z = Mathf.Clamp(p.z, 20.55f, 22.65f);
                else
                    p.z = Mathf.Clamp(p.z, 18.0f, 25.2f);
                return p;
            }
            // Even when not touring (e.g. final station stop or platform exploration), prevent getting stuck in exterior inter-car notches or rear track pit
            if (p.y < -8.5f && p.x >= 99.5f && p.x <= 154.5f)
            {
                bool inGangway1 = p.x > 116.75f && p.x < 118.55f;
                bool inGangway2 = p.x > 135.05f && p.x < 136.85f;
                if ((inGangway1 || inGangway2) && p.z > 17.2f && p.z < 25.8f && (p.z < 20.5f || p.z > 22.7f))
                {
                    p.z = Mathf.Clamp(p.z, 20.55f, 22.65f);
                    p.y = Mathf.Max(p.y, -11.92f);
                }
                else if (p.z > 13.5f && p.z < 17.85f)
                {
                    p.z = 18.25f;
                    p.y = -11.92f;
                }
            }
            return p;
        }
        public void BeginTour()
        {
            player.SetMap(false);Touring=true;StopIndex=0;Travelling=false;Remaining=stopSeconds;
            train.position=new Vector3(stops[0],0,0);Station(0);SetDoorActors(sensor);
            Place(new Vector3(-492,-11.92f,21.6f));player.transform.rotation=Quaternion.Euler(0,90,0);player.eye.transform.localRotation=Quaternion.identity;player.Capture(true);
        }
        public void BeginJourney(Vector3 aboard,Quaternion facing,Quaternion view)
        {
            player.SetMap(false);Touring=true;StopIndex=-1;Travelling=true;Remaining=0;
            train.position=new Vector3(-700,0,0);Station(0);SetDoorActors(sensor);
            aboard = ClampTrainLocal(aboard, true);
            player.SetPose(train.TransformPoint(aboard),facing,view);player.Capture(true);
        }
        public void EndTour()
        {
            Touring=false;Travelling=false;StopIndex=3;train.position=new Vector3(stops[3],0,0);Station(3);SetDoorActors(player.transform);player.ResetPosition();
        }
        void LateUpdate()
        {
            trainRoofs.gameObject.SetActive(!player.MapVisible);
            if(SASCoop.CoopSession.Client)return;
            if(!player.missionMode&&Keyboard.current!=null&&Keyboard.current.tKey.wasPressedThisFrame)BeginTour();
            if(!player.missionMode&&Keyboard.current!=null&&Keyboard.current.enterKey.wasPressedThisFrame)
            {
                if(VaccineRouteState.FromHelicopter){VaccineRouteState.FromHelicopter=false;SceneManager.LoadScene("Vaccine_Blockout");return;}
                if(Touring)EndTour();
            }
            if(Touring)
            {
                if(!Travelling){Remaining=Mathf.Max(0,Remaining-Time.deltaTime);if(Remaining<=0&&StopIndex<3&&(DepartureAllowed==null||DepartureAllowed())){Travelling=true;stations[StopIndex+1].SetActive(true);SetDoorActors(sensor);}}
                else
                {
                    var previous=train.position;float x=Mathf.MoveTowards(previous.x,stops[StopIndex+1],travelSpeed*Time.deltaTime);
                    train.position=new Vector3(x,0,0);Place(player.transform.position+train.position-previous);
                    if(Mathf.Abs(x-stops[StopIndex+1])<.01f){StopIndex++;Station(StopIndex);Remaining=stopSeconds;Travelling=false;if(StopIndex==3){Touring=false;SetDoorActors(player.transform);}ReachedStop?.Invoke(StopIndex);}
                }
                if(Touring){var p=ClampTrainLocal(train.InverseTransformPoint(player.transform.position),true);Place(train.TransformPoint(p));}
                sensor.position=Travelling?new Vector3(0,-1000,0):new Vector3(train.position.x+104.5f,-11.92f,26);
            }
            else if(player.transform.position.y<-8.5f)
            {
                var p= train.InverseTransformPoint(player.transform.position);
                var safe = ClampTrainLocal(p, false);
                if ((safe - p).sqrMagnitude > .0001f) Place(train.TransformPoint(safe));
            }
            bool below=player.transform.position.y<-1.5f;
            foreach(var door in trainDoors){bool side=door.name=="Side automatic door";door.routeLocked=side&&Touring&&Travelling;door.routeOpen=side&&Touring&&!Travelling;}
            player.overview.transform.position=Touring?new Vector3(train.position.x+127,100,22):below?new Vector3(13,100,28):mapPosition;
            player.overview.orthographicSize=Touring||below?35:mapSize;
            string zone=Touring?(Travelling?"TRAIN / NEXT STOP "+(StopIndex+2):"STOP "+(StopIndex+1)+" / REMAIN ABOARD  "+Mathf.CeilToInt(Remaining)+"s"):below?"LAB STATION / EXIT TO STREET":player.transform.position.z>60?"RESEARCH COMPLEX":player.transform.position.z<34?"LANDING AND PARKING AREAS":"LAB DISTRICT";
            player.area.text="VACCINE / "+zone+(player.MapVisible?" / PLAN":"");
        }
        public void ApplyCoop(Vector3 position,bool touring,bool travelling,int stop,float remaining)
        {
            var delta=position-train.position;
            if(Touring||touring)Place(player.transform.position+delta);
            train.position=position;Touring=touring;Travelling=travelling;StopIndex=stop;Remaining=remaining;Station(Mathf.Clamp(stop,0,3));
            foreach(var door in trainDoors){bool side=door.name=="Side automatic door";door.routeLocked=side&&touring&&travelling;door.routeOpen=side&&touring&&!travelling;}
            if(touring){var p=ClampTrainLocal(train.InverseTransformPoint(player.transform.position),true);Place(train.TransformPoint(p));}
            player.area.text="VACCINE / "+(touring?(travelling?"TRAIN / NEXT STATION":"STATION / DEFEND TRAIN"):player.transform.position.y<-1.5f?"LAB STATION":"LAB DISTRICT");
        }
        void OnDestroy(){if(sensor)Destroy(sensor.gameObject);}
    }
}
