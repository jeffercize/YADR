using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


//This Node is only created on the host machine, and is the authority on all things.
public partial class GameServer : Node

{

    public List<String> connectedPlayerIDs = new List<String>();
   


    public override void _Ready()
    {

    }

    public override void _Process(double delta)
    {
       
    }

    public override void _PhysicsProcess(double delta)
    {
        
    }



}
     