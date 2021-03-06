using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine;
public enum OpCode {
    KEEP_ALIVE = 1,
    WELCOME = 2,
    START_GAME = 3,
    MAKE_MOVE = 4,
    QUEEN = 5,
    CLIENT_DISCONNECT = 6,
    USERNAME = 7,
    LOSER = 8,
    WINNER = 9

}
public static class NetUtility {

    public static void OnData(DataStreamReader stream, NetworkConnection cnn, Server server = null) {
        NetMessage msg = null;
        var opCode = (OpCode)stream.ReadByte();

        switch (opCode) {
            case OpCode.KEEP_ALIVE: msg = new NetKeepAlive(stream); break;
            case OpCode.WELCOME: msg = new NetWelcome(stream); break;
            case OpCode.START_GAME: msg = new NetStartGame(stream); break;
            case OpCode.MAKE_MOVE: msg = new NetMakeMove(stream); break;
            case OpCode.QUEEN: msg = new NetQueen(stream); break;
            case OpCode.CLIENT_DISCONNECT: msg = new NetDisconnect(stream); break;
            case OpCode.USERNAME: msg = new NetUserName(stream); break;
            case OpCode.LOSER: msg = new NetLoser(stream); break;
            case OpCode.WINNER: msg = new NetWinner(stream); break;
            default:
                Debug.Log("message recieved had no opcode");
                break;
        }

        if (server != null) {
            msg.RecievedOnServer(cnn);
        }
        else {
            msg.RecievedOnClient();
        }
    }


    public static Action<NetMessage> C_KEEP_ALIVE;
    public static Action<NetMessage> C_WELCOME;
    public static Action<NetMessage> C_START_GAME;
    public static Action<NetMessage> C_MAKE_MOVE;
    public static Action<NetMessage> C_QUEEN;
    public static Action<NetMessage> C_REMATCH;
    public static Action<NetMessage> C_CLIENT_DISCONNECT;
    public static Action<NetMessage> C_USERNAME;
    public static Action<NetMessage> C_LOSER;
    public static Action<NetMessage> C_WINNER;
    public static Action<NetMessage, NetworkConnection> S_KEEP_ALIVE;
    public static Action<NetMessage, NetworkConnection> S_WELCOME;
    public static Action<NetMessage, NetworkConnection> S_START_GAME;
    public static Action<NetMessage, NetworkConnection> S_MAKE_MOVE;
    public static Action<NetMessage, NetworkConnection> S_QUEEN;
    public static Action<NetMessage, NetworkConnection> S_REMATCH;
    public static Action<NetMessage, NetworkConnection> S_CLIENT_DISCONNECT;
    public static Action<NetMessage, NetworkConnection> S_USERNAME;
    public static Action<NetMessage, NetworkConnection> S_LOSER;
    public static Action<NetMessage, NetworkConnection> S_WINNER;

}
