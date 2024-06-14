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
        foreach (ulong id in handshake.Peers)
        {
            if (id == Global.clientID)
            {
                continue;
            }
            SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(id);
            if (!remotePeers.Contains(identity))
            {
                Global.debugLog("Adding secondary peer: " + id);
                remotePeers.Add(identity);
            }

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
        else if (privacyMode==GamePrivacyMode.OFFLINE)
        {
            SteamNetworkingMessages.CloseSessionWithUser(ref param.m_identityRemote);
            return;
        }
        else if (privacyMode==GamePrivacyMode.FRIENDS)
        {
            if (SteamFriends.GetFriendRelationship(param.m_identityRemote.GetSteamID()) == EFriendRelationship.k_EFriendRelationshipFriend)
            {
                AcceptSessionWithUser(ref param.m_identityRemote);
                remotePeers.Add(param.m_identityRemote);
                Global.debugLog("Accepting session request and adding remote peer: " + param.m_identityRemote);
                Handshake handshake = new Handshake() { };
                handshake.Sender = Global.clientID;
                handshake.Tick = Global.getTick();
                handshake.Timestamp = Time.GetUnixTimeFromSystem();
                foreach (SteamNetworkingIdentity i in remotePeers)
                {
                    handshake.Peers.Add(i.GetSteamID64());
                }
                SendMessageToPeer(param.m_identityRemote, new Handshake() {  });
            }
        }
        else if (privacyMode == GamePrivacyMode.PUBLIC)
        {
            AcceptSessionWithUser(ref param.m_identityRemote);
            remotePeers.Add(param.m_identityRemote);
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
        SendMessageToPeer(identity, new Handshake() { }, HANDSHAKE_CHANNEL);
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
}
