using UnityEngine;
using UnityEngine.AI;
using ShipEnemies;
namespace Vaccine
{
    public sealed class VaccineBreakableDoor : MonoBehaviour
    {
        public ReviewDoor door;public NavMeshObstacle obstacle;public float health=68;public int encounter=-1;
        public bool Broken{get;private set;}
        public void Strike(float damage){if(Broken)return;health-=damage;if(health<=0)Breach();}
        public void Breach()
        {
            if(Broken)return;Broken=true;door.leaf.gameObject.SetActive(false);door.enabled=false;if(obstacle)obstacle.enabled=false;
            if(VaccineMission.Instance&&SASCoop.CoopSession.Authority)VaccineMission.Instance.DoorBreached(encounter);
        }
    }
}
