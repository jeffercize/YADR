using Godot;
using Steamworks;

public partial class MPDebugPanel : Panel
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {

    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    public void onHostPressed()
    {

        //Global.NetworkManager.startServer();
    }

    public void onJoinPressed()
    {
        string id = GetNode<TextEdit>("JoinPanel/GridContainer/ServerIP").Text;
        string port = GetNode<TextEdit>("JoinPanel/GridContainer/ServerPort").Text;
        Global.NetworkPeer.JoinToPeer(ulong.Parse(id));

    }
}
