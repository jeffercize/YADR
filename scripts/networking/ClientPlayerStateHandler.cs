using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetworkMessages;
using static NetworkManager;
using Google.Protobuf;

public class ClientPlayerStateHandler
{
    public delegate void NetworkPlayerStatePositionEventHandler(PlayerStatePositionMessage message);
    public static event NetworkPlayerStatePositionEventHandler NetworkPlayerStatePositionEvent = delegate { };

    public static void CreateAndSendPlayerPositionMessage(Player player)
    {
        PlayerStatePositionMessage message = new PlayerStatePositionMessage();
        
        Identity identity = new Identity();
        identity.SteamID = (long)player.clientID;
        identity.Name = "";
        message.PlayerIdentity = identity;

        Position position = new Position();
        position.X = player.GlobalPosition.X;
        position.Y = player.GlobalPosition.Y;
        position.Z = player.GlobalPosition.Z;
        message.Position = position;

        NetworkManager.SendSteamMessage(Global.NetworkManager.client.connectionToServer, MessageType.PlayerStatePosition, message);
        //Global.NetworkManager.server.BroadcastMessageWithExclusion((long)Global.instance.clientID,WrapMessage(MessageType.PlayerStatePosition,message));

    }

    public static void handlePlayerStatePositionMessage(PlayerStatePositionMessage message)
    {
        NetworkPlayerStatePositionEvent.Invoke(message);
    }
}

