using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;



    public class NetworkInputHandler
    {

    public delegate void NetworkInputEventHandler();
    public static event NetworkInputEventHandler NetworkInputEvent = delegate { };


}

