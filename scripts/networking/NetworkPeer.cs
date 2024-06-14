using Godot;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Steamworks.SteamNetworkingMessages;
using Google.Protobuf;
using NetworkMessages;

public partial class NetworkPeer : Node
    {
    int nMaxCommandMessagesPerFrame = 100;
    int nMaxChatMessagesPerFrame = 100;
    int nMaxStateMessagesPerFrame = 100;
    int nMaxHandshakeMessagesPerFrame = 100;


    List<SteamNetworkingIdentity> remotePeers = new List<SteamNetworkingIdentity>();

    protected Callback<SteamNetworkingMessagesSessionRequest_t> MessageRequest;

    public const int k_nSteamNetworkingSend_NoNagle = 1;
    public const int k_nSteamNetworkingSend_NoDelay = 4;
    public const int k_nSteamNetworkingSend_Unreliable = 0;
    public const int k_nSteamNetworkingSend_Reliable = 8;
    public const int k_nSteamNetworkingSend_UnreliableNoNagle = k_nSteamNetworkingSend_Unreliable | k_nSteamNetworkingSend_NoNagle;
    public const int k_nSteamNetworkingSend_UnreliableNoDelay = k_nSteamNetworkingSend_Unreliable | k_nSteamNetworkingSend_NoDelay | k_nSteamNetworkingSend_NoNagle;
    public const int k_nSteamNetworkingSend_ReliableNoNagle = k_nSteamNetworkingSend_Reliable | k_nSteamNetworkingSend_NoNagle;

    public const int STATE_CHANNEL = 0;
    public const int CHAT_CHANNEL = 1;
    public const int COMMAND_CHANNEL = 2;
    public const int HANDSHAKE_CHANNEL = 3;
    public enum GamePrivacyMode { NONE = 0, OFFLINE = 1, PRIVATE = 2, FRIENDS = 3, PUBLIC = 4 };
    public GamePrivacyMode privacyMode = GamePrivacyMode.FRIENDS;

    public delegate void StateMessageReceived(State state);
    public static event StateMessageReceived StateMessageReceivedEvent;

    public delegate void ChatMessageReceived(Chat chat);
    public static event ChatMessageReceived ChatMessageReceivedEvent;

    public delegate void CommandMessageReceived(Command command);
    public static event CommandMessageReceived CommandMessageReceivedEvent;

    public delegate void HandshakeMessageReceived(Handshake handshake);
    public static event HandshakeMessageReceived HandshakeMessageReceivedEvent;


    public override void _Ready()
    {
        MessageRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnMessageRequest);
        HandshakeMessageReceivedEvent += OnHandshakeMessageReceived;
    }

    private void OnHandshakeMessageReceived(Handshake handshake)
    {
        Global.debugLog("Handshake received from: " + handshake.Sender);
        SteamNetworkingIdentity id = new SteamNetworkingIdentity();
        id.SetSteamID64(handshake.Sender);
        switch (handshake.Status)
        {
            case "JoinRequest":
                Global.debugLog("Join request received from: " + handshake.Sender);
                remotePeers.Add(id);
                HandshakePeer(id, "JoinAccepted");
                break;
            case "JoinAccepted":
                Global.debugLog("Join request accepted by: " + handshake.Sender);
                remotePeers.Add(id);
                HandshakePeer(id, "PeersRequest");
                break;
            case "PeersRequest":
                Global.debugLog("Peers list request received from: " + handshake.Sender);
                Handshake handshake1 = new Handshake() { Sender = Global.clientID, Timestamp = Time.GetUnixTimeFromSystem(), Tick = Global.getTick(), Status = "PeersList" };
                foreach (var remotePeer in remotePeers)
                {
                    handshake1.Peers.Add(remotePeer.GetSteamID64());
                }
                SendMessageToPeer(id, handshake1, HANDSHAKE_CHANNEL);
                break;
            case "PeersList":
                Global.debugLog("Peers list (total peers: " + handshake.Peers.Count+" ) received from: " + handshake.Sender);
                foreach (ulong peer in handshake.Peers)
                {
                    SteamNetworkingIdentity id2 = new SteamNetworkingIdentity();
                    id2.SetSteamID64(peer);
                    if (!remotePeers.Contains(id2) && peer != Global.clientID)
                    {
                        JoinToPeer(peer);
                    }
                }
                break;
            default:
                break;
        }
    }

    private void OnMessageRequest(SteamNetworkingMessagesSessionRequest_t param)
    {
        Global.debugLog("Connection request from: " + param.m_identityRemote.GetSteamID64());
        if (param.m_identityRemote.GetSteamID64() == Global.clientID)
        {
            Global.debugLog("Connection request from self, Rejected");
            return;
        }
        else if (remotePeers.Contains(param.m_identityRemote))
        {
            Global.debugLog("Already connected to this peer.");
            AcceptSessionWithUser(ref param.m_identityRemote);
            return;
        }
        else if (privacyMode==GamePrivacyMode.OFFLINE || privacyMode==GamePrivacyMode.NONE)
        {
            Global.debugLog("In offline mode, rejecting session.");
            SteamNetworkingMessages.CloseSessionWithUser(ref param.m_identityRemote);
            return;
        }
        else if (privacyMode==GamePrivacyMode.FRIENDS)
        {
            if (SteamFriends.GetFriendRelationship(param.m_identityRemote.GetSteamID()) == EFriendRelationship.k_EFriendRelationshipFriend)
            {
                AcceptSessionWithUser(ref param.m_identityRemote);
                Global.debugLog("Accepting session request from friend: " + param.m_identityRemote.GetSteamID64());
            }
        }
        else if (privacyMode == GamePrivacyMode.PUBLIC)
        {
            Global.debugLog("Accepting connection request, public mode.");
            AcceptSessionWithUser(ref param.m_identityRemote);
        }
    }

    public override void _Process(double delta)
    {
  
        nint[] stateMessages = new nint[nMaxStateMessagesPerFrame];
        for (int i = 0; i < ReceiveMessagesOnChannel(STATE_CHANNEL, stateMessages, nMaxStateMessagesPerFrame); i++)
        {
            SteamNetworkingMessage_t steamMsg = SteamNetworkingMessage_t.FromIntPtr(stateMessages[i]); //Converts the message to a C# object
            State state = State.Parser.ParseFrom(IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize));
            StateMessageReceivedEvent?.Invoke(state);
            SteamNetworkingMessage_t.Release(stateMessages[i]);
        }

        nint[] chatMessages = new nint[nMaxChatMessagesPerFrame];
        for (int i = 0; i < ReceiveMessagesOnChannel(CHAT_CHANNEL, chatMessages, nMaxChatMessagesPerFrame); i++)
        {
            Global.debugLog("Chat message received.");
            SteamNetworkingMessage_t steamMsg = SteamNetworkingMessage_t.FromIntPtr(chatMessages[i]); //Converts the message to a C# object
            Chat chat = Chat.Parser.ParseFrom(IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize));
            ChatMessageReceivedEvent?.Invoke(chat);
            SteamNetworkingMessage_t.Release(chatMessages[i]);
        }

        nint[] commandMessages = new nint[nMaxCommandMessagesPerFrame];
        for (int i = 0; i < ReceiveMessagesOnChannel(COMMAND_CHANNEL, commandMessages, nMaxCommandMessagesPerFrame); i++)
        {
            SteamNetworkingMessage_t steamMsg = SteamNetworkingMessage_t.FromIntPtr(commandMessages[i]); //Converts the message to a C# object
            Command command = Command.Parser.ParseFrom(IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize));
            CommandMessageReceivedEvent?.Invoke(command);
            SteamNetworkingMessage_t.Release(commandMessages[i]);
        }

        nint[] handshakeMessages = new nint[nMaxHandshakeMessagesPerFrame];
        for (int i = 0; i < ReceiveMessagesOnChannel(HANDSHAKE_CHANNEL, handshakeMessages, nMaxHandshakeMessagesPerFrame); i++)
        {
            SteamNetworkingMessage_t steamMsg = SteamNetworkingMessage_t.FromIntPtr(handshakeMessages[i]); //Converts the message to a C# object
            Handshake handshake = Handshake.Parser.ParseFrom(IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize));
            HandshakeMessageReceivedEvent?.Invoke(handshake);
            SteamNetworkingMessage_t.Release(handshakeMessages[i]);
        }

    }


    public void JoinToPeer(ulong id)
    {
        SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
        identity.SetSteamID64(id);
        JoinToPeer(identity);
    }

    private void JoinToPeer(SteamNetworkingIdentity identity)
    {
        SendMessageToPeer(identity, new Handshake() { Sender=Global.clientID,Timestamp=Time.GetUnixTimeFromSystem(),Tick=Global.getTick(),Status="JoinRequest" }, HANDSHAKE_CHANNEL);
    }

    public static byte[] IntPtrToBytes(IntPtr ptr, int cbSize)
    {
        byte[] retval = new byte[cbSize];
        Marshal.Copy(ptr, retval, 0, cbSize);
        return retval;
    }

    public void SendMessageToPeer(SteamNetworkingIdentity remotePeer, IMessage message, int channel = STATE_CHANNEL, int sendFlags = k_nSteamNetworkingSend_ReliableNoNagle)
    {
        byte[] data = message.ToByteArray();
        nint ptr = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, ptr, data.Length);
        int size = data.Length;
        EResult result = SendMessageToUser(ref remotePeer, ptr, (uint)data.Length, sendFlags, channel);
    }

    public void MessageAllPeers(IMessage message, int channel = STATE_CHANNEL, int sendFlags = k_nSteamNetworkingSend_ReliableNoNagle)
    {
        foreach (SteamNetworkingIdentity i in remotePeers)
        {
            SendMessageToPeer(i, message, channel, sendFlags);
        }
    }

    public void ChatAllPeers(string message)
    {
        Chat chat = new Chat() { Message = message, Sender = Global.clientID };
        ChatMessageReceivedEvent?.Invoke(chat);
        MessageAllPeers(chat, CHAT_CHANNEL);
    }

    public void CommandAllPeers(string command, List<string> commandParams)
    {
        Command cmd = new Command() { Command_ = command, Sender = Global.clientID };
        foreach(string param in commandParams)
        {
            cmd.Params.Add(param);
        }
        CommandMessageReceivedEvent?.Invoke(cmd);
        MessageAllPeers(cmd, COMMAND_CHANNEL);
    }

    public void HandshakePeer(SteamNetworkingIdentity remotePeer, string status)
    {
        Handshake handshake = new Handshake() { Sender = Global.clientID, Timestamp = Time.GetUnixTimeFromSystem(), Tick = Global.getTick(), Status = status };
        SendMessageToPeer(remotePeer, handshake, HANDSHAKE_CHANNEL);
    }

    public void HandshakeAllPeers(string status)
    {
        foreach (SteamNetworkingIdentity i in remotePeers)
        {
            HandshakePeer(i, status);
        }
    }
}
