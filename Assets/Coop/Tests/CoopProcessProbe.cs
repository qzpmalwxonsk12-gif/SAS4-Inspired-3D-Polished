#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using ShipEnemies;
using Vaccine;
using Onslaught.Mission;
namespace SASCoop
{
    // Explicit opt-in, local development probe used for two-process transport tests.
    public sealed class CoopProcessProbe:MonoBehaviour
    {
        [Serializable]class Command{public int sequence;public string action,code;public Vector3 position,target;public int role;}
        [Serializable]class Report{public string scene,status,code,position,objective,gates,medical;public bool online,client,power,tail,controller;public int frame,enemies,players,shots,hits;public float hp,trainX;}
        string path;int last=-1;float next;bool shooting;Vector3 aim;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Init()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-coopProbe");if(i<0||i+1>=args.Length)return;var go=new GameObject("Co-op development probe");DontDestroyOnLoad(go);go.AddComponent<CoopProcessProbe>().path=args[i+1];}
        void Update()
        {
            var s=CoopSession.Instance;if(!s||string.IsNullOrEmpty(path))return;
            var v=VaccineMission.Instance;var m=MissionDirector.Instance;var w=v?v.weapons:m?m.weapons:null;var player=v?v.player.transform:m?m.explorer.transform:null;var eye=v?v.player.eye:m?m.explorer.eye:null;
            if(shooting&&w&&eye){eye.transform.LookAt(aim);if(v)v.player.Capture(true);else m.explorer.Capture(true);w.TryFire();}
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;
            try
            {
                if(File.Exists(path))
                {
                    var c=JsonUtility.FromJson<Command>(File.ReadAllText(path));if(c.sequence!=last)
                    {
                        last=c.sequence;
                        switch(c.action)
                        {
                            case "join":s.LocalRole=c.role;s.Join(c.code);break;
                            case "host":s.Host();break;
                            case "ready":s.LocalRole=c.role;s.Ready(true);break;
                            case "place":if(player){var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.position=c.position;cc.enabled=true;}shooting=false;break;
                            case "shoot":shooting=true;aim=c.target;break;
                            case "stop":shooting=false;break;
                            case "power":if(v)v.generator.Use();break;
                            case "medkit":var kit=FindObjectsByType<VaccineInteractable>().FirstOrDefault(i=>i.medical&&i.medicalKey==c.code);if(kit)kit.Use();break;
                            case "security":var p=FindAnyObjectByType<LabSecurityPanel>();if(p)p.Activate();break;
                            case "reload":if(w)w.BeginReload();break;
                            case "leave":s.Leave();break;
                            case "quit":Application.Quit();break;
                        }
                    }
                }
                File.WriteAllText(path+".report",JsonUtility.ToJson(new Report{medical=string.Join(",",MedicalSupplies.Snapshot().Where(e=>e.users.Contains(MedicalSupplies.LocalId)).Select(e=>e.key)),controller=v?v.player.enabled:m&&m.explorer.enabled,gates=m?string.Join(",",m.gates.Select(g=>g.Broken+"/barrier="+g.barrier.enabled)):"",scene=SceneManager.GetActiveScene().name,status=s.Status,code=s.Code,online=CoopSession.Online,client=CoopSession.Client,frame=Time.frameCount,enemies=EnemyBrain.Living.Count,players=CoopWorld.Instance?CoopWorld.Instance.Players.Count:s.Members.Count,position=player?player.position.ToString():"",hp=w?w.health.Current:0,shots=w?w.ShotsFired:0,hits=w?w.Hits:0,power=v&&v.PowerOn,tail=v&&v.TailSealed,trainX=v&&v.train?v.train.train.position.x:0,objective=v?v.hud.missionObjective.text:m?m.hud.missionObjective.text:""},true));
            }
            catch(Exception e){Debug.LogWarning("Probe: "+e.Message);}
        }
    }
}
#endif
