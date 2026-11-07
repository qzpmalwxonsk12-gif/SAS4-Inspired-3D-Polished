using System.Linq;
using UnityEngine;

namespace Vaccine
{
    // World-space encounter boundaries are authored in the Editor, independently
    // of enemy sensing and NavMesh sampling. Height separates the two floors.
    public sealed class VaccineEncounterArea : MonoBehaviour
    {
        public int zone;
        public string displayName;
        public Bounds[] entry = new Bounds[0], placement = new Bounds[0];
        public Vector3[] reachableAnchors = new Vector3[0];
        public float scatter = 5;
        public bool enterToPopulate = true;
        public bool Contains(Vector3 point) => entry.Any(b => b.Contains(point));
        public bool Allows(Vector3 point) => placement.Any(b => b.Contains(point));
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1,.65f,.1f,.65f);
            foreach (var b in entry) Gizmos.DrawWireCube(b.center,b.size);
            Gizmos.color = Color.cyan;
            foreach (var b in placement) Gizmos.DrawWireCube(b.center,b.size);
            foreach (var p in reachableAnchors) Gizmos.DrawWireSphere(p,.4f);
        }
    }
}
