using UnityEngine;
namespace Vaccine
{
    public sealed class VaccineInteractable : MonoBehaviour
    {
        public VaccineUse action;public string label;public float readyAt;
        public bool medical;public string medicalKey;
        public string Prompt=>VaccineMission.Instance?VaccineMission.Instance.InteractionPrompt(this):"";
        public void Use(){if(SASCoop.CoopWorld.Request("use",this))return;if(VaccineMission.Instance)VaccineMission.Instance.Use(this);}
    }
}
