using UnityEngine;

public interface IPlayerInput
{
    Vector2 Move { get; }
    Vector2 Look { get; }
    bool JumpPressed { get; }
    bool SprintHeld { get; }
    bool CrouchHeld { get; }
    bool InteractPressed { get; }
}
