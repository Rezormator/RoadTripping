using UnityEngine;

public class PlayerService
{
    private readonly IPlayerInput _input;
    private readonly PlayerLookService _look;
    private readonly PlayerCrouchService _crouch;
    private readonly PlayerMovementService _movement;
    private readonly CameraBobService _bob;

    public PlayerService(IPlayerInput input, PlayerLookService look, PlayerCrouchService crouch,
                         PlayerMovementService movement, CameraBobService bob)
    {
        _input = input;
        _look = look;
        _crouch = crouch;
        _movement = movement;
        _bob = bob;
    }

    public Vector3 Velocity { get; private set; }

    public float Yaw => _look.Yaw;
    public float Pitch => _look.Pitch;

    public float Height => _crouch.Height;
    public float StandHeight => _crouch.StandHeight;
    public float CameraHeight => _crouch.CameraHeight;
    public float CrouchBlend => _crouch.Blend;

    public float HorizontalSpeed => _movement.HorizontalSpeed;
    public Vector3 CameraOffset => _bob.Offset;

    public void Tick(bool isGrounded, bool canStand, float deltaTime)
    {
        _look.Tick(_input.Look);
        _crouch.Tick(_input.CrouchHeld, canStand, deltaTime);
        Velocity = _movement.Tick(_input.Move, _input.SprintHeld, _input.JumpPressed,
                                  _crouch.IsCrouching, isGrounded, _look.Yaw, deltaTime);
        _bob.Tick(_movement.HorizontalSpeed, isGrounded, deltaTime);
    }

    public void SetYaw(float yaw) => _look.SetYaw(yaw);

    public void OnCeilingHit() => _movement.OnCeilingHit();
}
