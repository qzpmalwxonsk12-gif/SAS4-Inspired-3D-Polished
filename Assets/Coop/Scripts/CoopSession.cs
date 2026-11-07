using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Collections;

namespace SASCoop
{
    [DefaultExecutionOrder(-1000)]
    public sealed class CoopSession : MonoBehaviour
    {
        public static CoopSession Instance {get;private set;}
        public static bool Online=>Instance&&Instance.net&&Instance.net.IsListening;
        public static bool Client=>Online&&!Instance.net.IsServer;
        public static bool Authority=>!Client;
        public static int SquadSize=>Instance?Instance.squadSize:1;
        public static float DifficultyFor(int count)=>count>=4?3:count==3?2.5f:count==2?2:1;
        public static float Difficulty=>DifficultyFor(SquadSize);
        public static float InitialPopulationFor(int count)=>count>=3?1.5f:count==2?1.3f:1;
        public static float ReinforcementsFor(int count)=>count>=4?1.25f:count==3?.75f:count==2?.5f:0;
        public static float PopulationFor(int count)=>InitialPopulationFor(count)+ReinforcementsFor(count);
        public NetworkManager net;
        public ISession Session {get;private set;}
        public string Status="Choose a mission and deploy.";
        public string Mission="Onslaught_Mission";
        public string Code=>Session?.Code??"";
        public bool Busy,InMission;
        public int squadSize=1;
        public int LocalRole=0;
        public readonly Dictionary<ulong,float> CarriedHealth=new Dictionary<ulong,float>();
        public readonly Dictionary<ulong,LobbyMember> Members=new Dictionary<ulong,LobbyMember>();
        bool wired,leaving;float nextLobby;
        [Serializable]public class LobbyMember{public ulong id;public int role;public bool ready;}
        [Serializable]class LobbyPacket{public string mission;public LobbyMember[] members;}
        [Serializable]class TravelPacket{public string scene;public int count;public bool train;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset()=>Instance=null;
        void Awake()
        {
            if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);
            Application.runInBackground=true;Application.targetFrameRate=60;
            if(!net){net=gameObject.AddComponent<NetworkManager>();var transport=gameObject.AddComponent<UnityTransport>();net.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,TickRate=20,ConnectionApproval=false};}
            net.OnClientDisconnectCallback+=Disconnected;net.OnClientConnectedCallback+=id=>{if(!net.IsServer&&id==net.LocalClientId)StartCoroutine(InitialReady());};SceneManager.sceneLoaded+=Loaded;
        }
        IEnumerator InitialReady(){yield return new WaitForSecondsRealtime(.5f);if(net.IsListening&&!InMission)Ready(false);}
        async Task Authenticate()
        {
            if(UnityServices.State!=ServicesInitializationState.Initialized)
            {
                var options=new InitializationOptions();
                // Separate local test processes can authenticate independently; no credentials stored here.
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-coopProfile");
                if(index>=0&&index+1<args.Length)options.SetProfile(args[index+1]);
                await UnityServices.InitializeAsync(options);
            }
            if(!AuthenticationService.Instance.IsSignedIn)await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        public async void Host(){await HostAsync();}
        public async Task HostAsync()
        {
            if(Busy||Online)return;Busy=true;Status="Creating your squad…";
            try{await Authenticate();Session=await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions{MaxPlayers=4,IsPrivate=true,Name="SAS 4 3D",SessionProperties=new Dictionary<string,SessionProperty>{{"mission",new SessionProperty(Mission)}}}.WithRelayNetwork());Wire();Status="Share the code. Choose roles and ready up.";}
            catch(Exception e){Status="Could not host: "+e.Message;Debug.LogWarning(Status);if(net.IsListening)net.Shutdown();}
            finally{Busy=false;}
        }
        public async void Join(string code){await JoinAsync(code);}
        public async Task JoinAsync(string code)
        {
            if(Busy||Online)return;if(string.IsNullOrWhiteSpace(code)){Status="Enter your friend's room code.";return;}Busy=true;Status="Joining squad…";
            try{await Authenticate();Session=await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());if(Session.Properties.TryGetValue("mission",out var p))Mission=p.Value;Wire();Status="Connected. Choose your role and ready up.";}
            catch(Exception e){Status="Could not join: "+e.Message;Debug.LogWarning(Status);if(net.IsListening)net.Shutdown();}
            finally{Busy=false;}
        }
        void Wire()
        {
            if(wired||!net.IsListening)return;wired=true;
            net.CustomMessagingManager.RegisterNamedMessageHandler("sas.lobby",(id,r)=>{if(net.IsServer)return;var p=Read<LobbyPacket>(r);Mission=p.mission;Members.Clear();foreach(var m in p.members)Members[m.id]=m;});
            net.CustomMessagingManager.RegisterNamedMessageHandler("sas.ready",(id,r)=>{if(!net.IsServer||InMission)return;var p=Read<LobbyMember>(r);p.id=id;p.role=Mathf.Clamp(p.role,0,3);Members[id]=p;});
            net.CustomMessagingManager.RegisterNamedMessageHandler("sas.travel",(id,r)=>{if(net.IsServer||id!=0)return;var p=Read<TravelPacket>(r);LoadMission(p.scene,p.count,p.train);});
            if(net.IsServer)Members[net.LocalClientId]=new LobbyMember{id=net.LocalClientId,role=LocalRole};else Ready(false);
        }
        public static T Read<T>(FastBufferReader reader){reader.ReadValueSafe(out string json);return JsonUtility.FromJson<T>(json);}
        public void Send(string name,object message,ulong recipient,NetworkDelivery delivery=NetworkDelivery.ReliableFragmentedSequenced)
        {
            if(!Online)return;var json=JsonUtility.ToJson(message);using(var writer=new FastBufferWriter(System.Text.Encoding.UTF8.GetByteCount(json)*4+64,Allocator.Temp)){writer.WriteValueSafe(json);net.CustomMessagingManager.SendNamedMessage(name,recipient,writer,delivery);}
        }
        public void Broadcast(string name,object message){if(!Online||!net.IsServer)return;foreach(var id in net.ConnectedClientsIds)if(id!=net.LocalClientId)Send(name,message,id);}
        public void Ready(bool ready)
        {
            if(!Online)return;var m=new LobbyMember{id=net.LocalClientId,role=LocalRole,ready=ready};
            if(net.IsServer)Members[m.id]=m;else Send("sas.ready",m,0);
        }
        public async void Deploy()
        {
            if(Busy||InMission)return;
            if(Online)
            {
                if(!net.IsServer)return;
                if(net.ConnectedClientsIds.Any(id=>!Members.TryGetValue(id,out var m)||!m.ready)){Status="Every connected player must choose Ready.";return;}
                if(net.ConnectedClientsIds.Count==4&&(!Members.Values.Any(m=>m.role==0)||Members.Values.Select(m=>m.role).Distinct().Count()!=4)){Status="A full squad needs one of each role, including Demo.";return;}
                Busy=true;try{Session.AsHost().IsLocked=true;await Session.AsHost().SavePropertiesAsync();}catch(Exception e){Status="Could not lock the squad: "+e.Message;Busy=false;return;}Busy=false;
                Travel(Mission,false);
            }
            else LoadMission(Mission,1,false);
        }
        public void Travel(string scene,bool train)
        {
            if(Client)return;int count=Online?net.ConnectedClientsIds.Count:1;
            Broadcast("sas.travel",new TravelPacket{scene=scene,count=count,train=train});LoadMission(scene,count,train);
        }
        void LoadMission(string scene,int count,bool train)
        {
            CarriedHealth.Clear();if(train&&CoopWorld.Instance)foreach(var p in CoopWorld.Instance.Players.Values)CarriedHealth[p.id]=p.hp;
            InMission=true;squadSize=Mathf.Clamp(count,1,4);Time.timeScale=1;
            if(train)
            {
                var p=UnityEngine.Object.FindAnyObjectByType<Vaccine.VaccineWalkthrough>();
                if(p){var t=p.GetComponent<Vaccine.VaccineBoardingTransfer>();Vaccine.VaccineRouteState.Aboard=t.train.InverseTransformPoint(p.transform.position);Vaccine.VaccineRouteState.Facing=p.transform.rotation;Vaccine.VaccineRouteState.View=p.eye.transform.localRotation;Vaccine.VaccineMission.Instance.SaveForTrain();}
                Vaccine.VaccineRouteState.Pending=true;Vaccine.VaccineRouteState.FromHelicopter=true;
            }
            else {Vaccine.VaccineRouteState.Pending=false;Vaccine.VaccineRouteState.FromHelicopter=false;}
            SceneManager.LoadSceneAsync(scene);
        }
        void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name=="MainMenu"){InMission=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;}
            if(Online)new GameObject("Cooperative mission replication").AddComponent<CoopWorld>();
        }
        void Update()
        {
            if(InMission&&UnityEngine.InputSystem.Keyboard.current?.f10Key.wasPressedThisFrame==true){Leave();return;}
            if(Online&&!wired)Wire();
            if(!Online||!net.IsServer||InMission||Time.unscaledTime<nextLobby)return;nextLobby=Time.unscaledTime+.5f;
            foreach(var id in Members.Keys.ToArray())if(!net.ConnectedClientsIds.Contains(id))Members.Remove(id);
            Broadcast("sas.lobby",new LobbyPacket{mission=Mission,members=Members.Values.ToArray()});
        }
        void Disconnected(ulong id)
        {
            Members.Remove(id);
            if(!leaving&&!net.IsServer&&id==net.LocalClientId){Status="Connection to the host ended. Please rejoin from the menu.";Leave();}
        }
        public async void Leave()
        {
            if(leaving)return;leaving=true;Busy=true;
            try{if(Session!=null)await Session.LeaveAsync();}catch(Exception e){Debug.LogWarning("Session cleanup: "+e.Message);}
            Session=null;if(net.IsListening)net.Shutdown();wired=false;Members.Clear();squadSize=1;InMission=false;Time.timeScale=1;AudioListener.pause=false;
            SceneManager.LoadScene("MainMenu");Busy=false;leaving=false;
        }
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;if(Instance==this)Instance=null;}
    }
}
