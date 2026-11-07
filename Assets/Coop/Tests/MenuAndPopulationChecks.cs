#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Vaccine;
using ShipEnemies;
namespace SASCoop
{
    public sealed class MenuAndPopulationChecks:MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        public bool Done;public int Failed;public string Stage;
        void Check(bool value,string message){results.Add((value?"PASS ":"FAIL ")+message);if(!value)Failed++;Save();}
        void Save(){System.IO.Directory.CreateDirectory("Artifacts/Coop/MenuRefresh");System.IO.File.WriteAllText("Artifacts/Coop/MenuRefresh/checks.txt",$"Done={Done}; Failed={Failed}; Stage={Stage}\n"+string.Join("\n",results));}
        static int Reserves(VaccineMission m,int zone)=>((IEnumerable<(Vector3 pos,int kind,int zone)>)typeof(VaccineMission).GetField("reserves",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(m)).Count(r=>r.zone==zone);
        static void Place(VaccineMission m,Vector3 p){m.player.SetPose(p+Vector3.up*.08f,Quaternion.identity,Quaternion.identity);Physics.SyncTransforms();}
        static void Kill(){foreach(var e in EnemyBrain.Living.ToArray())if(e&&e.Health.Alive)e.Health.TakeDamage(100000);}
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);yield return null;
            var menu=FindAnyObjectByType<MainMenu>();Stage="Menu";
            menu.multi.onClick.Invoke();yield return null;Check(menu.onlinePanel.activeSelf,"Multiplayer reveals host/join controls");
            menu.single.onClick.Invoke();menu.missions[1].onClick.Invoke();yield return null;Check(!menu.onlinePanel.activeSelf&&CoopSession.Instance.Mission=="Vaccine_Blockout","Solo and Vaccine selection work");
            foreach(var hot in FindObjectsByType<OperatorHotspot>(FindObjectsSortMode.None))
            {
                hot.OnPointerEnter(null);Check(menu.roleDetail.text==RoleLoadout.Descriptions[hot.role],"Hover operator "+hot.role+" shows loadout");
                hot.OnPointerClick(null);hot.OnPointerExit(null);Check(CoopSession.Instance.LocalRole==hot.role&&menu.roleDetail.text==RoleLoadout.Descriptions[hot.role],"Click operator "+hot.role+" selects role");
            }
            var art=FindAnyObjectByType<SquadDiorama>();Check(art&&art.operators.All(t=>t)&&art.portraitCamera.GetComponent<Camera>().targetTexture,"Menu uses four real 3D operators and a live camera");
            for(int count=2;count<=4;count++)
            {
                Stage="Population / "+count+" players";Save();CoopSession.Instance.squadSize=count;SceneManager.LoadScene("Vaccine_Blockout");yield return null;yield return null;
                var m=VaccineMission.Instance;m.health.maximum=100000;m.health.ResetHealth();yield return new WaitForSeconds(8);
                int expectedInitial=count==2?33:38,expectedReserve=count==2?13:count==3?19:32;
                Check(m.InitialCount(10)==expectedInitial&&m.ReserveCount(10)==expectedReserve,"Courtyard budget "+count+" players: "+expectedInitial+" initial + "+expectedReserve+" reinforcements");
                Check(Reserves(m,0)==expectedReserve&&!m.AreaAlerted(0),"Safe parking does not release courtyard reserves ("+count+")");
                Place(m,new Vector3(0,0,8));float deadline=Time.time+10;while(Time.time<deadline){Kill();yield return new WaitForSeconds(.25f);}
                Check(Reserves(m,0)<expectedReserve,"Active courtyard releases reinforcements ("+count+")");
                Place(m,new Vector3(-45,0,-15));int paused=Reserves(m,0);yield return new WaitForSeconds(3);Check(Reserves(m,0)==paused,"Leaving area pauses additional arrivals ("+count+")");
                if(count==2)
                {
                    typeof(VaccineMission).GetMethod("CompleteDemolition",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,null);Place(m,new Vector3(54,0,1));yield return new WaitForSeconds(1);
                    int waiting=Reserves(m,3)+Reserves(m,4)+Reserves(m,5);deadline=Time.time+11;while(Time.time<deadline){Kill();yield return new WaitForSeconds(.25f);}
                    Check(waiting>0&&Reserves(m,3)+Reserves(m,4)+Reserves(m,5)<waiting,"Waiting-room reinforcements emerge while player stays in corridor");
                }
            }
            CoopSession.Instance.squadSize=1;SceneManager.LoadScene("Vaccine_Blockout");yield return null;yield return null;
            Done=true;Stage="Completed";Save();new GameObject("Vaccine regression checks").AddComponent<VaccineEncounterChecks>();
        }
    }
}
#endif
