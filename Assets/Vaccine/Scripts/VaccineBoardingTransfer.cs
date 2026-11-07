using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vaccine
{
    public static class VaccineRouteState
    {
        public static bool Pending, FromHelicopter;
        public static Vector3 Aboard;
        public static Quaternion Facing, View;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){Pending=false;FromHelicopter=false;}
    }

    public sealed class VaccineBoardingTransfer : MonoBehaviour
    {
        public VaccineWalkthrough player;
        public Transform train;
        bool leaving;
        void Update()
        {
            if(SASCoop.CoopSession.Client||leaving||player.MapVisible)return;
            if(VaccineMission.Instance&&!VaccineMission.Instance.BoardingAllowed)return;
            Vector3 p=train.InverseTransformPoint(player.transform.position);
            if(p.x>100.5f&&p.x<153.5f&&p.z>18&&p.z<25.2f&&p.y>-12.3f&&p.y<-9)
                {if(SASCoop.CoopSession.Online){if(SASCoop.CoopWorld.AllPosition(point=>{var q=train.InverseTransformPoint(point);return q.x>100.5f&&q.x<153.5f&&q.z>18&&q.z<25.2f&&q.y>-12.3f&&q.y<-9;},player.transform.position)){leaving=true;SASCoop.CoopSession.Instance.Travel("Vaccine_LabDistrict",true);}}else StartCoroutine(Depart(p));}
        }
        IEnumerator Depart(Vector3 aboard)
        {
            leaving=true;VaccineRouteState.Pending=true;VaccineRouteState.FromHelicopter=true;
            if(VaccineMission.Instance)VaccineMission.Instance.SaveForTrain();
            VaccineRouteState.Aboard=aboard;VaccineRouteState.Facing=player.transform.rotation;VaccineRouteState.View=player.eye.transform.localRotation;
            player.enabled=false;player.area.text="VACCINE / DOORS CLOSING / DEPARTING";player.prompt.text="";
            var sensor=new GameObject("Departure closed-door sensor").transform;sensor.position=Vector3.down*1000;
            foreach(var d in train.GetComponentsInChildren<SubwayTrainDoor>())d.actor=sensor;
            yield return new WaitForSeconds(2);
            var load=SceneManager.LoadSceneAsync("Vaccine_LabDistrict");
            if(load==null){Debug.LogError("Vaccine lab scene is unavailable in the build scene list.");player.enabled=true;leaving=false;VaccineRouteState.Pending=false;}
        }
    }
}
