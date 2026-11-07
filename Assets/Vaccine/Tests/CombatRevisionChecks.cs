#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using ShipEnemies;
using SASCoop;
namespace Vaccine
{
    public sealed class CombatRevisionChecks:MonoBehaviour
    {
        public bool Done;public int Passed,Failed;public string Stage;
        readonly List<string> lines=new List<string>();VaccineMission m;
        void Check(bool ok,string message){if(ok)Passed++;else Failed++;lines.Add((ok?"PASS ":"FAIL ")+message);Save();}
        void Save(){System.IO.Directory.CreateDirectory("Artifacts/CombatRevision");System.IO.File.WriteAllText("Artifacts/CombatRevision/checks.txt",$"Done={Done}; Passed={Passed}; Failed={Failed}; Stage={Stage}\n"+string.Join("\n",lines));}
        void Place(Vector3 p){m.player.SetPose(p+Vector3.up*.1f,Quaternion.identity,Quaternion.identity);Physics.SyncTransforms();}
        Vitality Person(string name,Vector3 position){var hp=new GameObject(name).AddComponent<Vitality>();hp.maximum=160;hp.ResetHealth();NavMesh.SamplePosition(position,out var hit,3,NavMesh.AllAreas);hp.transform.position=hit.position;return hp;}
        IEnumerator Start()
        {
            m=VaccineMission.Instance;m.health.maximum=100000;m.health.ResetHealth();Stage="Visible ship-area populations";yield return new WaitForSeconds(15);
            foreach(int z in new[]{11,12,13})Check(m.AreaPrepared(z)&&!m.AreaAlerted(z)&&EnemyBrain.Living.Any(e=>e.GetComponent<VaccineEnemyActor>().zone==z),"Ship area "+z+" contains visible dormant residents before entry");
            Check(m.Pending==0,"All initial district residents can spawn without blocking the combat cap");
            Stage="Medical supplies";
            var kit=FindObjectsByType<VaccineInteractable>(FindObjectsSortMode.None).First(i=>i.medical&&i.medicalKey=="lab");var patient=Person("Medkit patient",new Vector3(68,1.88f,79));
            Check(!m.UseMedical(kit,patient,42)&&!MedicalSupplies.Used("lab",42),"Full health does not consume a medkit use");
            patient.TakeDamage(100);Check(m.UseMedical(kit,patient,42)&&patient.Current==140,"Medkit restores 80 health for requesting player");
            Check(!m.UseMedical(kit,patient,42),"Same player cannot repeatedly consume a medkit");
            m.health.TakeDamage(100);float old=m.health.Current;Check(m.UseMedical(kit,m.health,0)&&m.health.Current==old+80,"Teammate keeps an independent medkit use");
            var snapshot=JsonUtility.FromJson<CoopWorld.WorldFrame>(JsonUtility.ToJson(new CoopWorld.WorldFrame{medical=MedicalSupplies.Snapshot()}));MedicalSupplies.Reset();MedicalSupplies.Apply(snapshot.medical);
            Check(MedicalSupplies.Used("lab",42)&&MedicalSupplies.Used("lab",0),"Medkit uses survive a co-op state-packet round trip");
            Object.Destroy(patient.gameObject);
            m.health.InvulnerableUntil=Time.time+1000;old=m.health.Current;foreach(var e in EnemyBrain.Living.ToArray())e.Health.TakeDamage(99999);
            Check(m.health.Current==old,"Killing infected no longer grants automatic health");
            Stage="Target selection";
            var boss=m.Spawn(new Vector3(110,0,48),4,14);var normal=m.Spawn(new Vector3(110,0,52),0,16);yield return null;
            Check(boss&&normal&&boss.Agent.speed==5&&normal.Agent.speed==5,"Boss and shambler match Vaccine base walking speed");
            // Isolate scoring from melee windups, which deliberately defer switching.
            boss.enabled=false;boss.StopMoving();normal.enabled=false;normal.StopMoving();
            var near=Person("Near target",boss.transform.position+Vector3.left*6);var far=Person("Damage dealer",boss.transform.position+Vector3.right*15);var unreachable=Person("Unreachable target",new Vector3(8,-12,30));unreachable.transform.position=new Vector3(1000,0,1000);
            var choose=boss.GetComponent<EnemyTargeting>();var team=new[]{near,far,unreachable};var selected=choose.Choose(team);boss.SetTarget(selected);
            Check(selected==near,"Boss begins with the closest reachable player");
            boss.Health.TakeDamage(boss.Health.maximum*.12f,DamageKind.Physical,far);
            Check(choose.RecentDamage(far)>0&&choose.Choose(team)==near,"Damage creates threat without breaking the initial four-second commitment");
            yield return new WaitForSeconds(4.2f);selected=choose.Choose(team);boss.SetTarget(selected);Check(selected==far,"Boss switches to a strong recent damage dealer after commitment");
            normal.Health.TakeDamage(1,DamageKind.Physical,far);Check(normal.GetComponent<EnemyTargeting>().Choose(team)==near,"Normal zombie chooses nearest reachable player regardless of damage threat");
            far.TakeDamage(999);Check(choose.Choose(team)==near,"Dead high-threat target is dropped immediately");
            Check(choose.Choose(new[]{unreachable,near})==near,"Unreachable player never displaces a reachable target");
            Check(CoopRevival.FindPosition(new Vector3(30,0,52),near,out var revive)&&Mathf.Abs(revive.y)<.3f,"Revive sampling finds clear floor on the correct level");
            boss.Health.TakeDamage(99999);normal.Health.TakeDamage(99999);Object.Destroy(near.gameObject);Object.Destroy(far.gameObject);Object.Destroy(unreachable.gameObject);
            // Reload to remove the deliberately spawned test boss and its completion flag.
            DontDestroyOnLoad(gameObject);UnityEngine.SceneManagement.SceneManager.LoadScene("Vaccine_LabDistrict");yield return null;yield return null;m=VaccineMission.Instance;m.health.maximum=100000;m.health.ResetHealth();
            Stage="Boss reinforcement lifecycle";Place(new Vector3(20,0,55));yield return new WaitForSeconds(1);Place(new Vector3(90,0,48));yield return new WaitForSeconds(12);
            Check(m.BossSpawned&&!m.BossDefeated&&m.BossReinforcementsSpawned>0,"Vaccine boss fight adds random surrounding attackers");
            Check(EnemyBrain.Living.Where(e=>e.GetComponent<VaccineEnemyActor>().BossReinforcement).All(e=>e.GetComponent<VaccineEnemyActor>().Alerted&&!m.Forbidden(e.transform.position)),"Boss additions are alerted and spawned in valid areas");
            var actualBoss=EnemyBrain.Living.First(e=>e.definition.kind==EnemyKind.Regurgitator);actualBoss.Health.TakeDamage(99999);int total=m.BossReinforcementsSpawned;yield return new WaitForSeconds(6);
            Check(m.BossDefeated&&m.BossReinforcementsSpawned==total,"Boss death stops new reinforcements");
            Done=true;Stage="Complete";Save();
        }
    }
}
#endif
