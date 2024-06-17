using Google.Protobuf;
using NetworkMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public interface INetObject<T>
{
    public ulong entityID { get; set; }
    public ulong ownerID { get; set; }
    public ulong type { get; set;}

    public T desiredState { get; set; }

    public void IterativeSync();

    public void HardSync();

    public void FromNetworkMessage<T>(T message);

    public T ToNetworkMessage<T>();


    }

