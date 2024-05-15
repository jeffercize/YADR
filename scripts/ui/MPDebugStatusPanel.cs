using Godot;
using NetworkMessages;
using System;
using System.Runtime.InteropServices;

public partial class MPDebugStatusPanel : Panel
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
        ClientChatHandler.ChatMessageReceived += NetworkChatHandler_ChatMessageReceived;
	}

    private void NetworkChatHandler_ChatMessageReceived(ChatMessage message)
    {
		Label msg = new Label();
		msg.Text = message.Sender.Name + ": " + message.Message.Message;
		GetNode<VBoxContainer>("ChatPanel/output/chatLog").AddChild(msg);

    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}

	 public void onSendPressed()
	{
		ClientChatHandler.CreateAndSendChatMessage(GetNode<TextEdit>("ChatPanel/chatInput").Text);
		GetNode<TextEdit>("ChatPanel/chatInput").Text = "";

    }
}
