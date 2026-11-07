#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using ShipEnemies;
namespace Onslaught.Mission
{
    public sealed class GateRouteChecks:MonoBehaviour
    {
        public bool Done;public int Passed,Failed;string results="";
        void Check(bool value,string text){if(value)Passed++;else Failed++;results+=(value?"PASS ":"FAIL ")+text+"\n";Directory.CreateDirectory("Artifacts/Vaccine/Gameplay");File.WriteAllText("Artifacts/Vaccine/Gameplay/gate-routes.txt",$"Done: {Done}\nPassed: {Passed}; Failed: {Failed}\n"+results);}
        IEnumerator Start()
        {
            var m=MissionDirector.Instance;typeof(MissionDirector).GetField("nextSpawn",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(m,float.PositiveInfinity);
            foreach(var e in EnemyBrain.Living.ToArray())Destroy(e.gameObject);m.player.maximum=100000;m.player.ResetHealth();
            var cc=m.explorer.GetComponent<CharacterController>();cc.enabled=false;m.explorer.transform.position=new Vector3(10,.08f,5);cc.enabled=true;
            yield return new WaitForSeconds(.6f);
            Check(FindObjectsByType<Onslaught.ShipDoor>().Where(d=>d.automatic).All(d=>!d.obstacle.enabled),"Automatic internal doors remain traversable to pathfinding");
            var boss=m.Spawn(4,0);yield return new WaitForSeconds(1);
            var hook=boss.GetComponent<MissionEnemy>();Check(hook.TargetGate==m.gates[0],"Boss chooses the nearby gate by complete approach path");
            m.gates[1].Strike(100000);yield return new WaitForSeconds(1);
            Check(hook.TargetGate==null,"Boss abandons closed gate when another breach opens");
            int hits=m.gates[0].Hits;float start=Time.time;
            while(boss.transform.position.x>m.gates[0].transform.position.x-1&&Time.time-start<35)yield return null;
            Check(boss.transform.position.x<m.gates[0].transform.position.x-1,"Boss navigates through open breach toward player");
            Check(m.gates[0].Hits==hits,"Closed gate receives no attacks after accessible breach opens");
            var enemy=m.Spawn(0,0);yield return new WaitForSeconds(.8f);Check(enemy.GetComponent<MissionEnemy>().TargetGate==null,"New ordinary zombie also prioritizes available player route");
            Done=true;Check(true,"Route regression run completed");
        }
    }
}
#endif
