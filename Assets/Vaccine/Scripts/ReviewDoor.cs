using UnityEngine;

namespace Vaccine
{
    // Temporary inspection interaction. Combat damage and encounter triggers come after layout approval.
    public sealed class ReviewDoor : MonoBehaviour
    {
        public Transform leaf;
        public string roomName;
        public bool Open { get; private set; }
        public void Toggle(){if(SASCoop.CoopWorld.Request("door",this))return;Open=!Open;}
        void Update() => leaf.localRotation = Quaternion.RotateTowards(leaf.localRotation,
            Quaternion.Euler(0, Open ? -100 : 0, 0), 180 * Time.deltaTime);
    }
}
