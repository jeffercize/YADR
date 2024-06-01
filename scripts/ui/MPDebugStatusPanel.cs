using Godot;
using Steamworks;
using System;

public partial class MPDebugStatusPanel : Panel
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Client.ChatMessageReceived += onChatMessageReceived;
    }

    private void onChatMessageReceived(string message, ulong sender)
    {
        if (IsInsideTree() && IsVisibleInTree())
        {
            Label msg = new Label();
            msg.Text = SteamFriends.GetFriendPersonaName(new CSteamID(sender)) + ": " + message;
            GetNode<VBoxContainer>("ChatPanel/output/chatLog").AddChild(msg);
        }
    }


    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    public void onSendPressed()
    {
        if (GetNode<TextEdit>("ChatPanel/chatInput").Text != String.Empty)
        {
            Global.NetworkManager.client.outgoingFramePacket.ChatMessages.Add(Global.NetworkManager.ChatMessageConstructor(GetNode<TextEdit>("ChatPanel/chatInput").Text));
            GetNode<TextEdit>("ChatPanel/chatInput").Text = "";
        }

    }
}
