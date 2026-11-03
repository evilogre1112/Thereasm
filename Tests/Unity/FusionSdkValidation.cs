using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if THEREASM_FUSION
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
#endif

// Runs in an isolated Unity project. Creates an ephemeral private room and shuts it down.
[InitializeOnLoad]
public static class FusionSdkValidation
{
    const string Request="Thereasm.FusionSdkValidation";
    static int assertions;
    [Serializable] class Report { public bool passed; public int assertions; public string message, region; }
    static FusionSdkValidation()
    {
        EditorApplication.playModeStateChanged += change =>
        { if(change==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Request,false)) RunLive(); };
    }
    static void Expect(bool value,string message) { assertions++; if(!value)throw new Exception(message); }
    public static void Run()
    {
#if THEREASM_FUSION
        try
        {
            PhotonFusionSetup.Configure();
            Expect(typeof(INetworkRunnerCallbacks).IsAssignableFrom(typeof(OnlineSession)),"Game adapter is not compiled with Fusion SDK.");
            var config=AssetDatabase.LoadAssetAtPath<PhotonOnlineConfig>("Assets/Networking/PhotonOnlineConfig.asset");
            Expect(config!=null,"Missing saved Photon config.");
            Expect(PhotonAppSettings.Global.AppSettings.AppIdFusion==config.appIdFusion,"SDK App ID does not match the saved config.");
            Expect(PhotonAppSettings.Global.AppSettings.FixedRegion==config.region,"SDK region does not match.");
            Expect(PhotonAppSettings.Global.AppSettings.AppVersion==config.appVersion,"SDK app version does not match.");
            SessionState.SetBool(Request,true);
            SessionState.SetInt(Request+".Assertions",assertions);
            var empty=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            const string scenePath="Assets/Scenes/FusionSdkTestEmpty.unity";
            EditorSceneManager.SaveScene(empty,scenePath);
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=scenePath)
                .Concat(new[]{new EditorBuildSettingsScene(scenePath,true)}).ToArray();
            SessionState.SetInt(Request+".Scene",EditorBuildSettings.scenes.Count(s=>s.enabled)-1);
            EditorApplication.isPlaying=true;
        }
        catch(Exception e) { Finish(false,e.ToString()); }
#else
        Finish(false,"THEREASM_FUSION is not enabled. Import the SDK and run PhotonFusionSetup.Configure first.");
#endif
    }
    static async void RunLive()
    {
#if THEREASM_FUSION
        OnlineSession session=null;
        NetworkRunner guest=null;
        bool passed=false; string outcome="";
        try
        {
            assertions=SessionState.GetInt(Request+".Assertions",0);
            var config=AssetDatabase.LoadAssetAtPath<PhotonOnlineConfig>("Assets/Networking/PhotonOnlineConfig.asset");
            // Multiple peers are enabled only in this isolated test's in-memory config.
            NetworkProjectConfig.Global.PeerMode=NetworkProjectConfig.PeerModes.Multiple;
            session=new GameObject("Validation Host Session").AddComponent<OnlineSession>();
            typeof(OnlineSession).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(session,config);
            string room="THRQA-"+Guid.NewGuid().ToString("N").Substring(0,8).ToUpperInvariant();
            session.CreateRoom(room);
            await WaitUntil(()=>!session.Busy,45,"Host connection timed out.");
            Expect(session.Connected,"Host could not create room: "+session.Status);
            var host=(NetworkRunner)typeof(OnlineSession).GetField("runner",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(session);
            // Multiple-peer scene managers stay busy until they have a scene root.
            // This empty network scene exists only in the isolated validation project.
            host.LoadScene(SceneRef.FromIndex(SessionState.GetInt(Request+".Scene",0)),UnityEngine.SceneManagement.LoadSceneMode.Additive);
            await WaitUntil(()=>!host.GetComponent<NetworkSceneManagerDefault>().IsBusy,15,"Validation host scene did not finish loading.");
            Debug.Log("FUSION CHECK: host created private room.");
            guest=new GameObject("Validation Guest Runner").AddComponent<NetworkRunner>();
            guest.ProvideInput=false;
            var observer=new GuestObserver(); guest.AddCallbacks(observer);
            var app=PhotonAppSettings.Global.AppSettings.GetCopy();
            var result=await guest.StartGame(new StartGameArgs {
                GameMode=Fusion.GameMode.Client,SessionName=room,PlayerCount=2,IsVisible=false,
                EnableClientSessionCreation=false,CustomPhotonAppSettings=app,
                Scene=new NetworkSceneInfo(),SceneManager=guest.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                Config=NetworkProjectConfig.Global
            });
            Expect(result.Ok,"Guest could not join: "+result.ShutdownReason);
            Debug.Log("FUSION CHECK: guest joined; host active="+host.ActivePlayers.Count()+", lobby count="+session.PlayerCount+", guest active="+guest.ActivePlayers.Count());
            await WaitUntil(()=>session.PlayerCount==2,10,"Host did not observe both players; active="+host.ActivePlayers.Count()+", lobby="+session.PlayerCount);
            Expect(guest.ActivePlayers.Count()==2,"Guest does not see two players.");
            await WaitUntil(()=>observer.role==1,10,"Guest did not receive the assigned role.");
            Send(guest,"role","0",1);
            await WaitUntil(()=>OnlineSession.LocalRole==DefaultOwner.Player2&&observer.role==0,10,"Live role swap did not synchronize.");
            Expect(OnlineSession.LocalRole==DefaultOwner.Player2,"Host role did not swap.");
            bool probeReceived=false;
            session.GuestMessage+=(kind,payload)=>{ if(kind=="probe"&&payload=="guest-to-host")probeReceived=true; };
            Send(guest,"probe","guest-to-host",2);
            await WaitUntil(()=>probeReceived,10,"Guest reliable message did not reach the game adapter.");
            Expect(probeReceived,"Reliable guest-to-host data failed.");
            typeof(OnlineSession).GetProperty("MatchStarted").GetSetMethod(true).Invoke(session,new object[]{true});
            session.SendSnapshot("host-to-guest");
            await WaitUntil(()=>observer.snapshot=="host-to-guest",10,"Host reliable message did not reach guest.");
            Expect(observer.snapshot=="host-to-guest","Reliable host-to-guest data failed.");
            passed=true; outcome="Fusion SDK compiled; the saved App ID created/joined a private two-player room in asia, synchronized roles, and exchanged reliable data both ways.";
        }
        catch(Exception e){outcome=e.ToString();}
        finally
        {
            if(guest!=null)await guest.Shutdown();
            if(session!=null)session.Leave();
            await Task.Delay(500);
        }
        Finish(passed,outcome);
#else
        await Task.CompletedTask;
        Finish(false,"Fusion adapter is not enabled.");
#endif
    }
#if THEREASM_FUSION
    [Serializable] class Envelope { public string kind,payload; public int protocol=1; }
    [Serializable] class Lobby { public int revision,hostRole; }
    static void Send(NetworkRunner runner,string kind,string payload,int key)
    { runner.SendReliableDataToServer(ReliableKey.FromInts(0x515354,key,0,0),Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope{kind=kind,payload=payload}))); }
    static async Task WaitUntil(Func<bool> condition,int seconds,string error)
    {
        double until=EditorApplication.timeSinceStartup+seconds;
        while(!condition()) { if(EditorApplication.timeSinceStartup>until)throw new Exception(error);await Task.Delay(100); }
    }
    class GuestObserver:INetworkRunnerCallbacks
    {
        public int role=-1;public string snapshot;
        void Received(NetworkRunner runner, byte[] data)
        {
            var envelope=JsonUtility.FromJson<Envelope>(Encoding.UTF8.GetString(data));
            if(envelope.kind=="lobby")
            {
                var state=JsonUtility.FromJson<Lobby>(envelope.payload);role=1-state.hostRole;
                Send(runner,"role_ack",state.revision.ToString(),100+state.revision);
            }
            if(envelope.kind=="snapshot")snapshot=envelope.payload;
        }
#if THEREASM_FUSION_21
        public void OnReliableDataReceived(NetworkRunner r,PlayerRef p,ReliableKey k,ReadOnlySpan<byte> data){Received(r,data.ToArray());}
#else
        public void OnReliableDataReceived(NetworkRunner r,PlayerRef p,ReliableKey k,ArraySegment<byte> data){Received(r,data.ToArray());}
#endif
        public void OnPlayerJoined(NetworkRunner r,PlayerRef p){}
        public void OnPlayerLeft(NetworkRunner r,PlayerRef p){}
        public void OnInput(NetworkRunner r,NetworkInput i){}
        public void OnInputMissing(NetworkRunner r,PlayerRef p,NetworkInput i){}
        public void OnShutdown(NetworkRunner r,ShutdownReason s){}
        public void OnConnectedToServer(NetworkRunner r){}
        public void OnDisconnectedFromServer(NetworkRunner r,NetDisconnectReason s){}
        public void OnConnectRequest(NetworkRunner r,NetworkRunnerCallbackArgs.ConnectRequest q,byte[] t){q.Refuse();}
        public void OnConnectFailed(NetworkRunner r,NetAddress a,NetConnectFailedReason s){}
        public void OnUserSimulationMessage(NetworkRunner r,SimulationMessagePtr m){}
        public void OnSessionListUpdated(NetworkRunner r,List<SessionInfo> s){}
        public void OnCustomAuthenticationResponse(NetworkRunner r,Dictionary<string,object> d){}
        public void OnHostMigration(NetworkRunner r,HostMigrationToken t){}
        public void OnReliableDataProgress(NetworkRunner r,PlayerRef p,ReliableKey k,float v){}
        public void OnSceneLoadDone(NetworkRunner r){}
        public void OnSceneLoadStart(NetworkRunner r){}
        public void OnObjectEnterAOI(NetworkRunner r,NetworkObject o,PlayerRef p){}
        public void OnObjectExitAOI(NetworkRunner r,NetworkObject o,PlayerRef p){}
    }
#endif
    static void Finish(bool passed,string message)
    {
        SessionState.SetBool(Request,false);
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"FusionSdkValidationResult.json"),
            JsonUtility.ToJson(new Report{passed=passed,assertions=assertions,message=message,region="asia"},true));
        Debug.Log("FUSION VALIDATION: "+message);EditorApplication.Exit(passed?0:1);
    }
}
