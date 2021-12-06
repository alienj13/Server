
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public class Server : MonoBehaviour
{
    public static Server Instance { set; get; }


    public NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    private NativeList<NetworkConnection> room;
    private bool IsActive = false;
    private const float KeepAliveTickRate = 10.0f;
    private float LastKeepAlive;
    //private int playerCount = -1;
    public Action connectionDropped;
    private NetUserName player0 = null;
    private NetUserName player1 = null;
    private bool player0Slot = false;
    private bool player1Slot = false;

    private Boolean roomactive = false;

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
        IsActive = true;
     

    }

    public void ShutDown() {
        if (IsActive) {
            driver.Dispose();
            connections.Dispose();
            IsActive = false;
            room.Dispose();
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
            room.Clear();
            player0 = null;
            player1 = null;
            player0Slot = false;
            player1Slot = false;
            roomactive = false;
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
            room.Clear();
            player0 = null;
            player1 = null;
            player0Slot = false;
            player1Slot = false;
            roomactive = false;
        }
    }

    private void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn) {
        NetMakeMove mm = msg as NetMakeMove;

        Broadcast(mm);
    }

    private void OnWelcomeServer(NetMessage msg, NetworkConnection cnn) {
        NetWelcome nw = msg as NetWelcome;
      //  playerCount++;
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

        //room.Add(cnn);
        //nw.AssignedTeam = ++playerCount;
        SendToClient(cnn, nw);

    }
    private void RegisterEvents() {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.S_USERNAME += OnUserNameServer;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
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


        if (player0 != null && player1 != null && !roomactive) {
            if (room[0] != null && room[1] != null) {
                Debug.Log($"{player1.PlayerName} and {player0.PlayerName} are in a room");
                SendToClient(room[1], player0);
                SendToClient(room[0], player1);
                NetStartGame ng = new NetStartGame();
                SendToClient(room[0], ng);
                SendToClient(room[1], ng);
                roomactive = true;
            }
        }
    }

    private void UnRegisterEvents() {
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.S_WELCOME -= OnWelcomeServer;
    }

}
    

