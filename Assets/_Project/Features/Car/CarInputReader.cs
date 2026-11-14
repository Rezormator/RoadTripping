using UnityEngine;
using UnityEngine.InputSystem;

public class CarInputReader : ICarInput
{
    private readonly InputAction _move;
    private readonly InputAction _handbrake;

    public CarInputReader()
    {
        _move = InputSystem.actions.FindAction("Move");
        _handbrake = InputSystem.actions.FindAction("Jump");
    }

    public float Throttle => _move.ReadValue<Vector2>().y;
    public float Steer => _move.ReadValue<Vector2>().x;
    public bool HandbrakeHeld => _handbrake.IsPressed();
}
