using Google.Protobuf;
using NetworkMessages;

/// <summary>
/// Mostly? static class to handle client-side chat
/// </summary>
public class ClientChatHandler
{

    public delegate void NetworkChatEventHandler(ChatMessage message);
    /// <summary>
    /// Local (non-networked) event that fires when a chat message is received from the network
    /// </summary>
    public static event NetworkChatEventHandler ChatMessageReceived = delegate { };

    public static void handleChatMessage(byte[] data)
    {
        if (!Global.NetworkManager.isActive) { return; }

        //Use protobuf parsing to convert from raw byte array to structured object. thanks protobuf!
        ChatMessage msg = ChatMessage.Parser.ParseFrom(data);

        //Send out a local event with the chat message, any objects that want a chat message can grab this and display it.
        ChatMessageReceived.Invoke(msg);
    }

    /// <summary>
    /// Create a fully featured chat message protobuf from text and also sends it off to networking to be delivered to the server. Infers that the sender is the account being used by this game instance.
    /// TODO: wacky string formatting probably makes this blow up
    /// </summary>
    /// <param name="text"></param>
    public static void CreateAndSendChatMessage(string text)
    {
        if (!Global.NetworkManager.isActive) { return; }

        ChatMessage msg = GenerateChatMessage(Global.instance.clientName, (long)Global.instance.clientID, text);

        //Use protobuf serializing to convert from structured object to raw byte array. thanks protobuf!
        msg.ToByteArray();

        //Sends the raw byte array, along with the message type (we know it's chat cause we're the chat handler) to the server.
        NetworkManager.SendSteamMessage(NetworkManager.MessageType.CHAT_BASIC, Global.NetworkManager.client.connectionToServer, msg.ToByteArray());
    }

    /// <summary>
    /// Create a fully featured chat message protobuf from its constituent peices
    /// </summary>
    /// <param name="senderName"></param>
    /// <param name="senderID"></param>
    /// <param name="message"></param>
    /// <param name="useIP"></param>
    /// <returns></returns>
    public static ChatMessage GenerateChatMessage(string senderName, long senderID, string message, bool useIP = false)
    {
        ChatMessage chatMessage = new();

        Identity id = new Identity();
        id.Name = senderName;
        id.SteamID = senderID;
        chatMessage.Sender = id;

        ChatString chatString = new ChatString();
        chatString.Message = message;
        chatMessage.Message = chatString;

        return chatMessage;
    }
}

