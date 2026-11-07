#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShipEnemies;

namespace Vaccine
{
    // Real Play-mode checks, using teleports and accelerated kills to exercise
    // progression. This is not a claim of a normal combat/balance playthrough.
    public sealed class VaccineEncounterChecks : MonoBehaviour
    {
        public bool Done;public int Passed,Failed;public string Stage;
        readonly List<string> lines=new List<string>();VaccineMission m;
        void Save(){Directory.CreateDirectory("Artifacts/Vaccine/Encounters");File.WriteAllText("Artifacts/Vaccine/Encounters/checks.txt",$"Done: {Done}\nPassed: {Passed}; Failed: {Failed}\nStage: {Stage}\n"+string.Join("\n",lines));}
        void Check(bool ok,string message){if(ok)Passed++;else Failed++;lines.Add((ok?"PASS ":"FAIL ")+message);Save();}
        void Bind(){m=VaccineMission.Instance;m.health.maximum=100000;m.health.ResetHealth();}
        void Place(Vector3 p){m.player.SetPose(p+Vector3.up*.08f,Quaternion.identity,Quaternion.identity);Physics.SyncTransforms();}
        void Kill(){foreach(var e in EnemyBrain.Living.ToArray())if(e&&e.Health.Alive)e.Health.TakeDamage(100000);}
        IEnumerator Drain(){float end=Time.time+40;while(m.ActiveCount+m.Pending>0&&Time.time<end){Kill();yield return new WaitForSeconds(.2f);}yield return null;}
        IEnumerator Audit()
        {
            Stage="Placement audit / "+SceneManager.GetActiveScene().name;Save();
            int tested=0;var problems=new List<string>();var unique=new HashSet<Vector3>();
            foreach(var p in m.spawns)
            {
                for(int i=0;i<8;i++)
                {
                    var prefab=m.enemyPrefabs[p.kind];
                    if(!m.FindEncounterPosition(prefab,p.transform.position,p.zone,out var safe,null,false))problems.Add(p.name+" no valid placement");
                    else
                    {
                        if(!m.ReachableSpawn(prefab,safe,p.zone)||m.Forbidden(safe)||!SafeEnemyPlacement.Clear(safe,prefab.definition.radius+.06f,prefab.definition.height))problems.Add(p.name+" unsafe "+safe);
                        unique.Add(safe);tested++;
                    }
                }
                yield return null;
            }
            Check(problems.Count==0,$"{tested} randomized placements checked for clearance, height, bounds and reachable anchors; "+string.Join("; ",problems.Take(12)));
            Check(unique.Count>tested*.85f,$"Random placement varies across repeated requests ({unique.Count}/{tested} distinct)");
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);Bind();yield return new WaitForSeconds(8);
            Stage="Surface area detection";
            Check(m.EncounterCount(10)==25,"Solo courtyard density is 25 (10 x 2.5)");
            Check(new[]{1,2,3,4}.Select(n=>Mathf.CeilToInt(10*m.baseDensity*SASCoop.CoopSession.InitialPopulationFor(n))+Mathf.CeilToInt(10*m.baseDensity*SASCoop.CoopSession.ReinforcementsFor(n))).SequenceEqual(new[]{25,46,57,70}),"Squad scaling remains 25 / 46 / 57 / 70");
            Check(m.ZoneRemaining(0)==25&&m.ZoneRemaining(1)==8,"Courtyard and electrical populations fully queued/spawned");
            Check(!m.AreaAlerted(0)&&!m.AreaAlerted(1),"Helicopter parking alerts neither courtyard nor electrical room");
            Check(EnemyBrain.Living.All(e=>!m.Forbidden(e.transform.position)),"No enemies in helicopter parking or sealed maintenance room");
            Place(new Vector3(0,0,8));yield return new WaitForSeconds(.8f);
            Check(EnemyBrain.Living.Where(e=>e.GetComponent<VaccineEnemyActor>().zone==0).All(e=>e.GetComponent<VaccineEnemyActor>().Alerted),"Courtyard entry alerts every courtyard enemy, including distant enemies");
            Check(!m.AreaAlerted(1)&&!m.AreaAlerted(2)&&!m.AreaAlerted(6),"Other rooms and underground remain dormant");
            foreach(var d in FindObjectsByType<VaccineBreakableDoor>())if(d.encounter==1)d.Breach();
            Place(new Vector3(-24,0,32));yield return new WaitForSeconds(.8f);Check(m.AreaAlerted(1),"Electrical-room entry alerts its encounter");
            // Remove current bodies so validation samples measure geometry, not crowd occupancy.
            yield return Drain();yield return Audit();m.Use(m.generator);yield return null;
            Check(m.PowerOn,"Generator works after electrical clearance");
            typeof(VaccineMission).GetMethod("CompleteDemolition",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,null);
            Place(new Vector3(29,0,-12));yield return new WaitForSeconds(1);
            Check(m.AreaAlerted(2)&&!m.AreaAlerted(6),"Station entry alerts station without alerting platforms");
            Place(new Vector3(54,0,1));yield return new WaitForSeconds(1);
            Check(m.waitingDoors.All(d=>d.Broken)&&new[]{3,4,5}.All(m.AreaAlerted),"Passing waiting rooms bursts all doors and alerts the ambush");
            Place(new Vector3(80,-8,10));yield return new WaitForSeconds(.5f);Check(!m.AreaAlerted(6),"Upper stair flights do not alert platforms");
            Place(new Vector3(95,-12,8));yield return new WaitForSeconds(1);Check(m.AreaAlerted(6),"Entering platform level alerts both platforms and trains");
            yield return Drain();
            Check(m.BoardingAllowed,"Increased surface, waiting-room and platform populations can all clear; boarding unlocks");
            Stage="Inaccessible-enemy recovery";Save();
            var probe=m.Spawn(new Vector3(118,-12,8),0,6);yield return null;
            if(probe){probe.Agent.Warp(new Vector3(100,-12,-7));probe.transform.position=new Vector3(100,-12,-7);Physics.SyncTransforms();}
            yield return new WaitForSeconds(12);
            Check(probe&&m.SpawnRecoveries>0&&!m.Forbidden(probe.transform.position),"A deliberately misplaced maintenance-room enemy is relocated to the public platform");
            yield return Drain();
            // Begin the actual connected train journey, retaining its normal stop logic.
            VaccineRouteState.Pending=true;VaccineRouteState.Aboard=new Vector3(118,-11.92f,21.6f);VaccineRouteState.Facing=Quaternion.identity;VaccineRouteState.View=Quaternion.identity;
            SceneManager.LoadScene("Vaccine_LabDistrict");yield return null;yield return null;Bind();
            bool[] stops=new bool[3];bool boarded=false;float deadline=Time.time+210;
            while(m.train.Touring&&Time.time<deadline)
            {
                int i=m.train.StopIndex;
                if(!m.train.Travelling&&i>=0&&i<3)
                {
                    if(!stops[i])
                    {
                        stops[i]=true;Stage="Train stop "+(i+1);Save();yield return new WaitForSeconds(8);
                        Check(m.ZoneRemaining(15)>0&&!m.train.Travelling,"Train stop "+(i+1)+" holds for increased boarding wave");
                        boarded|=EnemyBrain.Living.Any(e=>e.GetComponent<VaccineEnemyActor>().zone==15&&e.transform.position.z<25.9f);
                    }
                    yield return Drain();
                }
                yield return null;
            }
            Check(stops.All(s=>s)&&!m.train.Touring&&m.train.StopIndex==3,"All three waves clear and train reaches final station");
            Check(boarded,"Station attackers navigate into train interior");
            yield return Drain();
            yield return Audit();
            Stage="District area isolation";Place(new Vector3(20,0,55));yield return new WaitForSeconds(1);
            Check(m.AreaAlerted(9)&&new[]{10,11,12,13,16}.All(z=>!m.AreaAlerted(z)),"Subway exit alerts street only, leaving lab, boss road and landing areas dormant");
            Check(m.Remaining>m.ActiveCount+m.Pending,"Unvisited district encounters count toward extraction clearance");
            yield return Drain();
            FindAnyObjectByType<LabSecurityGate>().Unlock();foreach(var d in m.labDoors)if(!d.Open)d.Toggle();
            Place(new Vector3(69,1.88f,79));yield return new WaitForSeconds(1);Check(m.AreaAlerted(10)&&!m.AreaAlerted(16),"Lab interior alerts separately from road beyond truck");
            m.Use(m.vaccineCases.First(v=>v.gameObject.activeSelf));yield return Drain();
            Place(new Vector3(90,0,48));yield return new WaitForSeconds(2);
            Check(m.AreaAlerted(16)&&m.BossSpawned,"Passing truck activates separate road encounter and boss");yield return Drain();
            foreach(var zone in new[]{12,11,13})
            {
                Place(zone==12?new Vector3(120,0,20):zone==11?new Vector3(90,0,20):new Vector3(140,0,70));yield return new WaitForSeconds(1);
                Check(m.AreaAlerted(zone),"Area "+zone+" alerts only after entry");yield return Drain();
            }
            Check(m.Remaining==0&&m.BossDefeated&&m.VaccineRecovered,"All district encounters clear without inaccessible enemies");
            yield return new WaitForSeconds(8);Check(m.extractionCraft.activeSelf,"Extraction arrives after every encounter is cleared");
            Place(m.extractionCraft.transform.position+Vector3.up*.42f);yield return new WaitForSeconds(11);Check(m.Victory,"Extraction completes mission");
            Done=true;Stage="Completed";Save();
        }
    }
}
#endif
