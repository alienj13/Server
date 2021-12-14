
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Networking;

public class Server : MonoBehaviour {
    public static Server Instance { set; get; }


    public NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
   
    private bool IsActive = false;
    private const float KeepAliveTickRate = 10.0f;
    private float LastKeepAlive;
    //private int playerCount = -1;
    public Action connectionDropped;

    //room 1 players 0 and 1
    private NetUserName player0 = null;
    private NetUserName player1 = null;
    private bool player0Slot = false;
    private bool player1Slot = false;
    private NativeList<NetworkConnection> room;

    private NetUserName player2 = null;
    private NetUserName player3 = null;
    private bool player2Slot = false;
    private bool player3Slot = false;
    private NativeList<NetworkConnection> room2;

    private bool roomactive = false;
    private bool room2active = false;

    private void Awake() {
        Instance = this;
        initialize(8007);
        RegisterEvents();
    }
    public void initialize(ushort port) {
        driver = NetworkDriver.Create();
        NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = port;
        if (driver.Bind(endpoint) != 0) {
            Debug.Log("Unable to bind on port " + endpoint.Port);
            return;
        }
        else {
            driver.Listen();
            Debug.Log("currently listening on port " + endpoint.Port);

        }
        connections = new NativeList<NetworkConnection>(2, Allocator.Persistent);
        room = new NativeList<NetworkConnection>(2, Allocator.Persistent);
        room2 = new NativeList<NetworkConnection>(2, Allocator.Persistent);
        IsActive = true;


    }

    public void ShutDown() {
        if (IsActive) {
            driver.Dispose();
            connections.Dispose();
            IsActive = false;
            room.Dispose();
            room2.Dispose();
        }
        UnRegisterEvents();
    }

    public void OnDestroy() {
        ShutDown();
    }

    public void Update() {
        if (!IsActive) {
            return;
        }
        KeepAlive();
        driver.ScheduleUpdate().Complete();
        CleanupConnections();
        AcceptNewConnections();
        UpdateMessagePump();
    }

    private void KeepAlive() {
        if (Time.time - LastKeepAlive > KeepAliveTickRate) {
            LastKeepAlive = Time.time;
            Broadcast(new NetKeepAlive());

        }
    }

    private void UpdateMessagePump() {
        DataStreamReader stream;
        for (int i = 0; i < connections.Length; i++) {
            NetworkEvent.Type cmd;
            while ((cmd = driver.PopEventForConnection(connections[i], out stream)) != NetworkEvent.Type.Empty) {
                if (cmd == NetworkEvent.Type.Data) {
                    NetUtility.OnData(stream, connections[i], this);
                }
                else if (cmd == NetworkEvent.Type.Disconnect) {
                    Debug.Log("Client disconnected from server");

                    CheckRoom1(i);
                    CheckRoom2(i);

                    //Broadcast(new NetDisconnect());

                    //ShutDown();
                    break;
                }
            }
        }
    }

    private void AcceptNewConnections() {
        NetworkConnection c;
        while ((c = driver.Accept()) != default(NetworkConnection)) {
            connections.Add(c);
        }
    }

    private void CleanupConnections() {
        for (int i = 0; i < connections.Length; i++) {
            if (!connections[i].IsCreated) {
                connections.RemoveAtSwapBack(i);
                i--;
            }
        }
    }

    public void SendToClient(NetworkConnection Connection, NetMessage msg) {
        DataStreamWriter writer;
        driver.BeginSend(Connection, out writer);
        msg.Serialize(ref writer);
        driver.EndSend(writer);
    }

    public void Broadcast(NetMessage msg) {
        for (int i = 0; i < connections.Length; i++) {
            if (connections[i].IsCreated) {
                Debug.Log($"sending {msg.Code} tp : {connections[i].InternalId}");
                SendToClient(connections[i], msg);
            }
        }
    }

    private void CheckRoom1(int i) {
        if (room[0] == connections[i]) {
            connections[i] = default(NetworkConnection);
            connectionDropped?.Invoke();
            for (int j = 0; j < connections.Length; j++) {
                if (room[1] == connections[j]) {
                    SendToClient(room[1], new NetDisconnect());
                    connections[j] = default(NetworkConnection);
                    connectionDropped?.Invoke();
                }
            }
            ClearRoom1();
        }
        else if (room[1] == connections[i]) {
            connections[i] = default(NetworkConnection);
            connectionDropped?.Invoke();
            for (int j = 0; j < connections.Length; j++) {
                if (room[0] == connections[j]) {
                    SendToClient(room[0], new NetDisconnect());
                    connections[j] = default(NetworkConnection);
                    connectionDropped?.Invoke();
                }
            }
            ClearRoom1();
        }
    }


    private void CheckRoom2(int i) {
        if (room2[0] == connections[i]) {
            connections[i] = default(NetworkConnection);
            connectionDropped?.Invoke();
            for (int j = 0; j < connections.Length; j++) {
                if (room2[1] == connections[j]) {
                    SendToClient(room2[1], new NetDisconnect());
                    connections[j] = default(NetworkConnection);
                    connectionDropped?.Invoke();
                }
            }
            ClearRoom2();
        }
        else if (room2[1] == connections[i]) {
            connections[i] = default(NetworkConnection);
            connectionDropped?.Invoke();
            for (int j = 0; j < connections.Length; j++) {
                if (room2[0] == connections[j]) {
                    SendToClient(room2[0], new NetDisconnect());
                    connections[j] = default(NetworkConnection);
                    connectionDropped?.Invoke();
                }
            }
            ClearRoom2();
        }
    }


    public void ClearRoom1() {
        room.Clear();
        player0 = null;
        player1 = null;
        player0Slot = false;
        player1Slot = false;
        roomactive = false;
     
        
    }

    public void ClearRoom2() {
        room2.Clear();
        player2 = null;
        player3 = null;
        player2Slot = false;
        player3Slot = false;
        room2active = false;


    }

    private void RegisterEvents() {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.S_USERNAME += OnUserNameServer;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.S_QUEEN += OnQueenHealthServer;
       
    }

    private void OnQueenHealthServer(NetMessage msg, NetworkConnection cnn) {
        NetQueen nq = msg as NetQueen;
        Debug.Log(nq.QueenHealth);
        if (nq.QueenHealth<100) {
            if (room[0] == cnn) {
               
                Debug.Log($"{player1.PlayerName} has lost");
                ToLoserServer(room[1]);
                StartCoroutine(RetrieveExperience($"{player0.PlayerName}", cnn));
            }
            else if (room[1] == cnn) {
                
                Debug.Log($"{player0.PlayerName} has lost");
                ToLoserServer(room[0]);
                StartCoroutine(RetrieveExperience($"{player1.PlayerName}", cnn));

            }

            else if (room2[0] == cnn) {

                Debug.Log($"{player3.PlayerName} has lost");
                ToLoserServer(room2[1]);
                StartCoroutine(RetrieveExperience($"{player2.PlayerName}", cnn));
            }
            else if (room2[1] == cnn) {

                Debug.Log($"{player2.PlayerName} has lost");
                ToLoserServer(room2[0]);
                StartCoroutine(RetrieveExperience($"{player3.PlayerName}", cnn));

            }
        }
    }

    private void ToWinnerServer(NetworkConnection cnn, int rank, int experience) {
        NetWinner nw = new NetWinner();
        nw.rank = rank;
        nw.experience = experience;

        SendToClient(cnn,nw);
    
    }
    private void ToLoserServer(NetworkConnection cnn) {
        NetLoser nl = new NetLoser();
        SendToClient(cnn,nl);
        
    }

    private void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn) {
        NetMakeMove mm = msg as NetMakeMove;

        if (cnn == room[0]) {
            SendToClient(room[1], mm);
        }
        else if(cnn == room[1])  {
            SendToClient(room[0], mm);
        }

        else if (cnn == room2[0]) {
            SendToClient(room2[1], mm);
        }
        else if (cnn == room2[1]) {
            SendToClient(room2[0], mm);
        }



        //Broadcast(mm);
    }

    private void OnWelcomeServer(NetMessage msg, NetworkConnection cnn) {
        NetWelcome nw = msg as NetWelcome;



        if (player0 != null && player0Slot) {
            if (player1 == null && player1Slot) {
                room.Add(cnn);
                nw.AssignedTeam = 1;
                player1Slot = true;
            }
        }

        if (player2 != null && player2Slot) {
            if (player3 == null && player3Slot) {
                room2.Add(cnn);
                nw.AssignedTeam = 1;
                player3Slot = true;
            }
        }


        if (player0 == null && !player0Slot) {
            room.Add(cnn);
            nw.AssignedTeam = 0;
            player0Slot = true;
        }
        else if (player1 == null && !player1Slot) {
            room.Add(cnn);
            nw.AssignedTeam = 1;
            player1Slot = true;
        }

        else if (player2 == null && !player2Slot) {
            room2.Add(cnn);
            nw.AssignedTeam = 0;
            player2Slot = true;
        }
        else if (player3 == null && !player3Slot) {
            room2.Add(cnn);
            nw.AssignedTeam = 1;
            player3Slot = true;
        }




        //room.Add(cnn);
        //nw.AssignedTeam = ++playerCount;
        SendToClient(cnn, nw);

    }
    

    private void OnUserNameServer(NetMessage msg, NetworkConnection cnn) {
        NetUserName un = msg as NetUserName;
        Debug.Log(un.PlayerName);


        if (player0 == null && player0Slot) {
            this.player0 = un;
            Debug.Log($"{player0.PlayerName} has connected");
        }
        else if (player1 == null && player1Slot) {
            this.player1 = un;
            Debug.Log($"{player1.PlayerName} has connected");

        }


        else if (player2 == null && player2Slot) {
            this.player2 = un;
            Debug.Log($"{player2.PlayerName} has connected");
        }
        else if (player3 == null && player3Slot) {
            this.player3 = un;
            Debug.Log($"{player3.PlayerName} has connected");

        }


        if (player0 != null && player1 != null && !roomactive) {
            if (room[0] != null && room[1] != null) {
                Debug.Log($"{player1.PlayerName} and {player0.PlayerName} are in room1");
                SendToClient(room[1], player0);
                SendToClient(room[0], player1);
                NetStartGame ng = new NetStartGame();
                SendToClient(room[0], ng);
                SendToClient(room[1], ng);
                roomactive = true;
            }
        }

        else if (player2 != null && player3 != null && !room2active) {
            if (room2[0] != null && room2[1] != null) {
                Debug.Log($"{player3.PlayerName} and {player2.PlayerName} are in  room2");
                SendToClient(room2[1], player2);
                SendToClient(room2[0], player3);
                NetStartGame ng = new NetStartGame();
                SendToClient(room2[0], ng);
                SendToClient(room2[1], ng);
                room2active = true;
            }
        }

    }

    private void UnRegisterEvents() {
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.S_WELCOME -= OnWelcomeServer;
       
    }


    public IEnumerator RetrieveExperience(string user, NetworkConnection cnn) {

        WWWForm form = new WWWForm();
        form.AddField("username", user);

        using (UnityWebRequest www = UnityWebRequest.Post("https://xenoregistertest.000webhostapp.com/RetrieveExperience.php", form)) {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ProtocolError || www.result == UnityWebRequest.Result.ConnectionError) {

                Debug.Log(www.error);
                StartCoroutine(RetrieveExperience(user, cnn));
            }
            else {
                Debug.Log(www.downloadHandler.text);
                int experience = int.Parse( www.downloadHandler.text);
                if (experience >= 150) {
                   StartCoroutine(UpdateRank(user,cnn));
                    if (room[0] == cnn || room[1] == cnn) {
                        for (int i = 0; i < connections.Length; i++) {
                            if (room[0] == connections[i]) {
                                connections[i] = default(NetworkConnection);
                                connectionDropped?.Invoke();
                                Debug.Log("remove player 0");
                                connections.RemoveAt(i);
                            }
                            if (room[1] == connections[i]) {
                                connections[i] = default(NetworkConnection);
                                connectionDropped?.Invoke();
                                Debug.Log("remove player 1");
                                connections.RemoveAt(i);
                            }
                        }
                        ClearRoom1();
                        CleanupConnections();

                    }
                   else if (room2[0] == cnn || room2[1] == cnn) {
                        for (int i = 0; i < connections.Length; i++) {
                            if (room2[0] == connections[i]) {
                                connections[i] = default(NetworkConnection);
                                connectionDropped?.Invoke();
                                Debug.Log("remove player 0");
                                connections.RemoveAt(i);
                            }
                            if (room2[1] == connections[i]) {
                                connections[i] = default(NetworkConnection);
                                connectionDropped?.Invoke();
                                Debug.Log("remove player 1");
                                connections.RemoveAt(i);
                            }
                        }
                        ClearRoom2();
                        CleanupConnections();

                    }
                }
                else {
                    StartCoroutine(UpdateExperience(user));
                    ToWinnerServer(cnn, 0, 50);
                    if (room[0] == cnn || room[1] == cnn) {
                        for (int i = 0; i < connections.Length; i++) {
                            if (room[0] == connections[i]) {
                                connections[i] = default(NetworkConnection);
                                connectionDropped?.Invoke();
                                Debug.Log("remove player 0");
                                connections.RemoveAt(i);
                            }
                            if (room[1] == connections[i]) {
                                connections[i] = default(NetworkConnection);
                                connectionDropped?.Invoke();
                                Debug.Log("remove player 1");
                                connections.RemoveAt(i);
                            }
                        }
                        ClearRoom1();
                        CleanupConnections();
                    }

                    else if (room2[0] == cnn || room2[1] == cnn) {
                        for (int i = 0; i < connections.Length; i++) {
                            if (room2[0] == connections[i]) {
                                connections[i] = default(NetworkConnection);
                                connectionDropped?.Invoke();
                                Debug.Log("remove player 0");
                                connections.RemoveAt(i);
                            }
                            if (room2[1] == connections[i]) {
                                connections[i] = default(NetworkConnection);
                                connectionDropped?.Invoke();
                                Debug.Log("remove player 1");
                                connections.RemoveAt(i);
                            }
                        }
                        ClearRoom2();
                        CleanupConnections();
                    }
                }     
            }
        }
    }

    public IEnumerator UpdateRank(string user, NetworkConnection cnn) {

        WWWForm form = new WWWForm();
        form.AddField("username", user);

        using (UnityWebRequest www = UnityWebRequest.Post("https://xenoregistertest.000webhostapp.com/UpdateRank.php", form)) {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ProtocolError || www.result == UnityWebRequest.Result.ConnectionError) {

                StartCoroutine(UpdateRank(user,cnn));
            }
            else {
                Debug.Log(www.downloadHandler.text);
                int rank = int.Parse(www.downloadHandler.text);
                //Net message
                ToWinnerServer(cnn,rank,0);

            }
        }
    }

    public IEnumerator UpdateExperience(string user) {

        WWWForm form = new WWWForm();
        form.AddField("username", user);

        using (UnityWebRequest www = UnityWebRequest.Post("https://xenoregistertest.000webhostapp.com/UpdateExperience.php", form)) {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ProtocolError || www.result == UnityWebRequest.Result.ConnectionError) {

                StartCoroutine(UpdateExperience(user));
            }
            else {
                Debug.Log(www.downloadHandler.text);
            }
        }
    }





}

