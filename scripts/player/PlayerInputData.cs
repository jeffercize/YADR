using Godot;

public class PlayerInputData
{
    public float mouseSens = 0.1f;

    public Vector2 direction = new(0f, 0f);
    public Vector2 lookDelta = new(0f, 0f);

    public bool fire = false;
    public bool jump = false;
    public bool sprint = false;
    public bool aim = false;
    public bool inventory = false;
    public bool menu = false;
    public bool crouch = false;
    public bool prone = false;
    public bool sneak = false;
}