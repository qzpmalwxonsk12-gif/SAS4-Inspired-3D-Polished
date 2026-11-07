using UnityEngine;
using UnityEngine.AI;

namespace Onslaught
{
    public sealed class ShipDoor : MonoBehaviour
    {
        public bool automatic;
        public Transform panel;
        public BoxCollider barrier;
        public NavMeshObstacle obstacle;
        public float width = 4, height = 3.5f;
        public bool RequestedOpen { get; private set; }
        public bool IsOpen => amount >= .99f;
        public bool Occupied { get; private set; }
        float amount, nextScan, holdUntil;
        bool nearActor;
        readonly Collider[] overlaps = new Collider[64];
        public void Request(bool open) { RequestedOpen = open; }
        public void Toggle() { if(SASCoop.CoopWorld.Request("shipdoor",this))return;Request(!RequestedOpen); }
        bool Scan(float depth)
        {
            int count = Physics.OverlapBoxNonAlloc(transform.position + Vector3.up * height / 2,
                new Vector3(width / 2 + .1f, height / 2, depth), overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (overlaps[i].GetComponentInParent<ShipEnemies.Vitality>() != null || overlaps[i].GetComponentInParent<CharacterController>() != null || overlaps[i].GetComponentInParent<NavMeshAgent>() != null)
                    return true;
            return false;
        }
        void Update()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + .08f;
                Occupied = Scan(.85f);
                nearActor = automatic && Scan(3.2f);
                if (nearActor) holdUntil = Time.time + 1.2f;
            }
            bool desired = automatic ? nearActor || Time.time < holdUntil : RequestedOpen;
            // Never start closing on a character standing in the opening.
            if (Occupied && amount > .01f) desired = true;
            amount = Mathf.MoveTowards(amount, desired ? 1 : 0, Time.deltaTime * 1.4f);
            panel.localPosition = Vector3.up * (height / 2 + (height + .25f) * amount);
            barrier.enabled = !IsOpen;
            // Sensor doors are traversable routes even while visually closed. Carving them
            // made agents choose long detours instead of approaching the nearest sensor.
            obstacle.enabled = !automatic && !IsOpen;
        }
        public float DistanceTo(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            local.x = Mathf.Clamp(local.x, -width / 2, width / 2); local.y = Mathf.Clamp(local.y, 0, height); local.z = 0;
            return Vector3.Distance(point, transform.TransformPoint(local));
        }
    }
}
