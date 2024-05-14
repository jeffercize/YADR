using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;



    public class NetworkChatHandler
    {

    public delegate void NetworkChatEventHandler(ChatMessage message);
    public static event NetworkChatEventHandler ChatMessageReceived = delegate { };

    public struct ChatMessage
    {
        public string senderName;
        public string message;

        public ChatMessage(string name, string msg) : this()
        {
            this.senderName = name;
            this.message = msg;
        }
    }

    internal static void handleChatMessage(byte[] data)
    {
       Global.debugLog("I got a chat message with data: " + data.GetStringFromUtf8());
       ChatMessageReceived.Invoke(new ChatMessage(Global.instance.clientName,data.GetStringFromUtf8()));
    }

    internal static void CreateAndSendChatMessage(string text)
    {
        if (!Global.NetworkManager.isActive) { return; }
        Global.NetworkManager.networkDebugLog("Send pressed! time to build a chat network message with data: " + text.ToUtf8Buffer().GetStringFromUtf8());
        Global.NetworkManager.client.SendSteamMessage(NetworkManager.MessageType.CHAT, text.ToUtf8Buffer());
        
    }
}

