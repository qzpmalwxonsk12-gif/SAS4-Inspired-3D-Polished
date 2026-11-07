using UnityEngine;
using UnityEngine.AI;
using ShipEnemies;
namespace Vaccine
{
    public sealed class VaccineDoorObstacle : MonoBehaviour
    {
        public ReviewDoor door;public LabSecurityGate gate;public NavMeshObstacle obstacle;
        void Update(){if(obstacle)obstacle.enabled=door?!door.Open:gate&&!gate.Unlocked;}
    }
}
