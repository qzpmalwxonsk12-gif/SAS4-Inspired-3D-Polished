#if UNITY_EDITOR
using UnityEngine;
namespace Vaccine
{
    // Retain the old entry point for the Editor menu/scripts; validation now uses the revised route.
    public sealed class LabDistrictChecks : MonoBehaviour
    {
        public bool Done => runner && runner.Done;
        public int Passed => runner ? runner.Passed : 0;
        public int Failed => runner ? runner.Failed : 0;
        public string Stage => runner ? runner.Stage : "Starting";
        VaccineConnectedRouteChecks runner;
        void Start(){runner=new GameObject("Connected Vaccine route checks").AddComponent<VaccineConnectedRouteChecks>();}
    }
}
#endif
