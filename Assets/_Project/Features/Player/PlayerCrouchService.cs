using UnityEngine;

public class PlayerCrouchService
{
    private readonly PlayerMovementConfig _config;

    public bool IsCrouching { get; private set; }
    public float Blend { get; private set; }

    public float StandHeight => _config.StandHeight;
    public float Height => Mathf.Lerp(_config.StandHeight, _config.CrouchHeight, Blend);
    public float CameraHeight => Mathf.Lerp(_config.StandCameraHeight, _config.CrouchCameraHeight, Blend);

    public PlayerCrouchService(PlayerMovementConfig config)
    {
        _config = config;
    }

    public void Tick(bool crouchHeld, bool canStand, float deltaTime)
    {
        IsCrouching = crouchHeld || (IsCrouching && !canStand);

        float target = IsCrouching ? 1f : 0f;
        Blend = Mathf.MoveTowards(Blend, target, _config.CrouchTransitionSpeed * deltaTime);
    }
}
