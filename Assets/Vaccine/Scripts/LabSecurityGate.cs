using UnityEngine;

namespace Vaccine
{
    public sealed class LabSecurityGate : MonoBehaviour
    {
        public Transform left, right;
        public bool Unlocked { get; private set; }
        public float OpenAmount { get; private set; }
        Vector3 leftClosed,rightClosed;
        void Awake(){leftClosed=left.localPosition;rightClosed=right.localPosition;}
        public void Unlock()=>Unlocked=true;
        void Update()
        {
            OpenAmount=Mathf.MoveTowards(OpenAmount,Unlocked?1:0,Time.deltaTime);
            left.localPosition=leftClosed+Vector3.left*1.85f*OpenAmount;
            right.localPosition=rightClosed+Vector3.right*1.85f*OpenAmount;
        }
    }
}
