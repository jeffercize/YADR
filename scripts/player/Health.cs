using NetworkMessages;
using System;

public class Health
{
    internal void FromNetworkMessage(PlayerHealth playerhealth)
    {
        
    }

    internal PlayerHealth ToNetworkMessage()
    {
        return new PlayerHealth();
    }
}