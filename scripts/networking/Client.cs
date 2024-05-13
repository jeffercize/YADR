using Godot;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Godot.HttpRequest;
using static NetworkManager;


    public partial class Client: Node
    {

     HSteamNetConnection connectionToServer;

    protected Callback<SteamNetConnectionStatusChangedCallback_t> SteamNetConnectionStatusChange;


    public Client(HSteamNetConnection connectionToServer)
    {
        this.connectionToServer = connectionToServer;
    }
    public Client() { }




    public override void _Ready()
    {
        SteamNetConnectionStatusChange = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(onSteamNetConnectionStatusChange);
    }

    private void onSteamNetConnectionStatusChange(SteamNetConnectionStatusChangedCallback_t param)
    {
        throw new NotImplementedException();
    }

    public override void _Process(double delta)
    {
        IntPtr[] messages = new IntPtr[100];
        SteamNetworkingSockets.ReceiveMessagesOnConnection(connectionToServer, messages, 100);
        foreach (IntPtr msg in messages)
            {
            if (msg == IntPtr.Zero) { continue; }
            handleNetworkData(Marshal.PtrToStructure<SteamNetworkingMessage_t>(msg));
            Global.NetworkManager.networkDebugLog("Client got a message.");
        }
        
    }



    private void handleNetworkData(SteamNetworkingMessage_t message)
    {
        byte[] data;
        handleNetworkData(NetworkManager.deconstructSteamNetworkingMessage(message, out data), data);

    }

    private void handleNetworkData(MessageType type, byte[] data)
    {

        switch (type)
        {
            case MessageType.CHAT:
                Global.NetworkManager.networkDebugLog("Client - Chat Message Received.");
                NetworkChatHandler.handleChatMessage(data);
                break;
            default:
                break;
        }
    }

    public void SendSteamMessage(MessageType type, byte[] data)
    {

        long result = new();
        byte[] newData = new byte[data.Length+1];
        Buffer.BlockCopy(data,0,newData, 1, data.Length);
        newData[0] = (byte)type;
        IntPtr ptr;
        unsafe
        {
            fixed (byte* p = newData)
            {
                ptr =(IntPtr)p;
            }
        }

        Marshal.Copy(newData, 0, ptr, newData.Length);
        SteamNetworkingSockets.SendMessageToConnection(connectionToServer,  ptr, (uint)newData.Length, NetworkManager.k_nSteamNetworkingSend_ReliableNoNagle,out result);

        /*
        Global.NetworkManager.networkDebugLog("Client - Just sent a message, lets check our work.");
        Global.NetworkManager.networkDebugLog("     Byte[] array is " + newData.Length +" long. Compared to original data length of: " + data.Length + "   [0] should be the type, the rest is the payload.");
        Global.NetworkManager.networkDebugLog("     Type should be the first byte of our new array, testing: " + (MessageType)newData[0]);
        byte[] debugData = new byte[newData.Length-1];
        Buffer.BlockCopy(newData, 1, debugData, 0, newData.Length-1);
        Global.NetworkManager.networkDebugLog("     Let's copy out +1 offset to length bytes and check it for text: " + debugData.GetStringFromUtf8());

        Global.NetworkManager.networkDebugLog("     Ok time to test pointer dereference.");
        byte[] derefData = new byte[newData.Length];
        Marshal.Copy(ptr, derefData, 0, newData.Length);
        MessageType debugType = (MessageType)derefData[0];
        Global.NetworkManager.networkDebugLog("         intptr dereference test - type: " + debugType);
        byte[] debugData2 = new byte[derefData.Length-1];
        Buffer.BlockCopy(derefData, 1, debugData2, 0, derefData.Length-1);
        Global.NetworkManager.networkDebugLog("         intptr dereference test - data: " + debugData2.GetStringFromUtf8());
        */
    }



}

