using UnityEngine;
using TMPro;

namespace Vaccine
{
    [DefaultExecutionOrder(150)]
    public sealed class LabSecurityPanel : MonoBehaviour
    {
        public VaccineWalkthrough player;
        public LabSecurityGate gate;
        public TMP_Text status;
        public void Activate(){if(SASCoop.CoopWorld.Request("security",this))return;gate.Unlock();status.text="LAB ACCESS\nRELEASED";status.color=new Color(.45f,1,.65f);}
        void LateUpdate()
        {
            if(player.MapVisible||!player.Captured)return;
            if(Physics.Raycast(player.eye.transform.position,player.eye.transform.forward,out var hit,3.5f)&&hit.collider.GetComponentInParent<LabSecurityPanel>()==this)
            {
                player.prompt.text=gate.Unlocked?"Lab access released":(player.missionMode?"F":"E")+"  Release main lab door";
                if(player.Actions["Interact"].WasPressedThisFrame())Activate();
            }
        }
    }
}
