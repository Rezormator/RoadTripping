using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : IPlayerInput
{
    private readonly InputAction _move;
    private readonly InputAction _look;
    private readonly InputAction _jump;
    private readonly InputAction _sprint;
    private readonly InputAction _crouch;
    private readonly InputAction _interact;

    public PlayerInputReader()
    {
        _move = InputSystem.actions.FindAction("Move");
        _look = InputSystem.actions.FindAction("Look");
        _jump = InputSystem.actions.FindAction("Jump");
        _sprint = InputSystem.actions.FindAction("Sprint");
        _crouch = InputSystem.actions.FindAction("Crouch");
        _interact = InputSystem.actions.FindAction("Interact");
    }

    public Vector2 Move => _move.ReadValue<Vector2>();
    public Vector2 Look => _look.ReadValue<Vector2>();
    public bool JumpPressed => _jump.WasPressedThisFrame();
    public bool SprintHeld => _sprint.IsPressed();
    public bool CrouchHeld => _crouch.IsPressed();
    public bool InteractPressed => _interact.WasPressedThisFrame();
}
