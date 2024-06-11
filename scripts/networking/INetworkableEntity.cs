public partial interface INetworkableEntity
{
    public ulong entityID { get; set; }

    public void AssignEntityID();
}

