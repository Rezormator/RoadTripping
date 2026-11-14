using UnityEngine;

public class PlayerService
{
    private readonly IPlayerInput _input;
    private readonly PlayerLookService _look;
    private readonly PlayerCrouchService _crouch;
    private readonly PlayerMovementService _movement;
    private readonly CameraBobService _bob;
    private readonly PlayerMovementConfig _config;

    public PlayerService(IPlayerInput input, PlayerLookService look, PlayerCrouchService crouch,
                         PlayerMovementService movement, CameraBobService bob, PlayerMovementConfig config)
    {
        _input = input;
        _look = look;
        _crouch = crouch;
        _movement = movement;
        _bob = bob;
        _config = config;
    }

    public Vector3 Velocity { get; private set; }
    public bool IsSeated => _look.IsSeated;

    public float Yaw => _look.Yaw;
    public float HeadYaw => _look.HeadYaw;
    public float Pitch => _look.Pitch;

    public float Height => _crouch.Height;
    public float StandHeight => _crouch.StandHeight;
    public float CameraHeight => IsSeated ? _config.SeatedCameraHeight : _crouch.CameraHeight;
    public float CrouchBlend => _crouch.Blend;

    public float HorizontalSpeed => _movement.HorizontalSpeed;
    public Vector3 CameraOffset => _bob.Offset;

    public void Tick(bool isGrounded, bool canStand, float deltaTime)
    {
        _look.Tick(_input.Look);

        if (IsSeated)
        {
            Velocity = Vector3.zero;
            _bob.Tick(0f, false, deltaTime);
            return;
        }

        _crouch.Tick(_input.CrouchHeld, canStand, deltaTime);
        Velocity = _movement.Tick(_input.Move, _input.SprintHeld, _input.JumpPressed,
                                  _crouch.IsCrouching, isGrounded, _look.Yaw, deltaTime);
        _bob.Tick(_movement.HorizontalSpeed, isGrounded, deltaTime);
    }

    public void SetYaw(float yaw) => _look.SetYaw(yaw);

    public void EnterSeat()
    {
        _movement.Stop();
        _crouch.Reset();
        _look.EnterSeat();
        Velocity = Vector3.zero;
    }

    public void ExitSeat(float yaw) => _look.ExitSeat(yaw);

    public void OnCeilingHit() => _movement.OnCeilingHit();

    public void AddPush(Vector3 velocity) => _movement.AddPush(velocity);
}
