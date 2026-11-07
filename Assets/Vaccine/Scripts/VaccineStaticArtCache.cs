using UnityEngine;

namespace Vaccine
{
    // Saved authoring references allow Editor batches to be reversed without losing props/colliders.
    public sealed class VaccineStaticArtCache : MonoBehaviour
    {
        [HideInInspector] public MeshRenderer[] sources;
        [HideInInspector] public GameObject[] generatedGroups;
    }
}
