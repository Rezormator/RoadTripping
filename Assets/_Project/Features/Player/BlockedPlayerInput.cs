using UnityEngine;

public class BlockedPlayerInput : IPlayerInput
{
    public Vector2 Move => Vector2.zero;
    public Vector2 Look => Vector2.zero;
    public bool JumpPressed => false;
    public bool SprintHeld => false;
    public bool CrouchHeld => false;
}
