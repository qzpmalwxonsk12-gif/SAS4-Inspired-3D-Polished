using UnityEngine;

namespace Vaccine
{
    // Shared by intact and damaged carriages. The sensor covers both sides of the doorway.
    public sealed class SubwayTrainDoor : MonoBehaviour
    {
        public Transform actor, leftPanel, rightPanel;
        public float travel = 1.26f, speed = 1.8f, holdSeconds = 1.2f;
        public Vector3 sensor = new Vector3(2.5f, 2.2f, 3.5f);
        public float OpenAmount { get; private set; }
        [System.NonSerialized] public bool routeOpen, routeLocked;
        Vector3 leftClosed, rightClosed;
        float holdUntil;
        readonly Collider[] contacts=new Collider[32];

        void Awake()
        {
            leftClosed = leftPanel.localPosition; rightClosed = rightPanel.localPosition;
            if (!actor) { var p = FindAnyObjectByType<VaccineWalkthrough>(); if (p) actor = p.transform; }
        }
        void Update()
        {
            if (!actor) return;
            Vector3 p = transform.InverseTransformPoint(actor.position + Vector3.up * .9f);
            bool near = Mathf.Abs(p.x) < sensor.x && Mathf.Abs(p.y) < sensor.y && Mathf.Abs(p.z) < sensor.z;
            if(!near&&!routeLocked){int count=Physics.OverlapBoxNonAlloc(transform.position,sensor,contacts,transform.rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);for(int i=0;i<count;i++)if(contacts[i].GetComponentInParent<ShipEnemies.Vitality>()){near=true;break;}}
            if (near) holdUntil = Time.time + holdSeconds;
            OpenAmount = Mathf.MoveTowards(OpenAmount, !routeLocked && (routeOpen || near || Time.time < holdUntil) ? 1 : 0, speed * Time.deltaTime);
            leftPanel.localPosition = leftClosed + Vector3.left * (travel * OpenAmount);
            rightPanel.localPosition = rightClosed + Vector3.right * (travel * OpenAmount);
        }
    }
}
