using UnityEngine;

public class PlayerLookService
{
    private readonly PlayerMovementConfig _config;

    public float Yaw { get; private set; }
    public float HeadYaw { get; private set; }
    public float Pitch { get; private set; }
    public bool IsSeated { get; private set; }

    public PlayerLookService(PlayerMovementConfig config)
    {
        _config = config;
    }

    public void SetYaw(float yaw)
    {
        Yaw = yaw;
    }

    public void EnterSeat()
    {
        IsSeated = true;
        HeadYaw = 0f;
        Pitch = 0f;
    }

    public void ExitSeat(float yaw)
    {
        IsSeated = false;
        HeadYaw = 0f;
        Yaw = yaw;
    }

    public void Tick(Vector2 lookInput)
    {
        Vector2 look = lookInput * _config.MouseSensitivity;

        if (IsSeated)
        {
            HeadYaw = Mathf.Clamp(HeadYaw + look.x, -_config.SeatedYawLimit, _config.SeatedYawLimit);
            Pitch = Mathf.Clamp(Pitch - look.y, _config.SeatedMinPitch, _config.SeatedMaxPitch);
            return;
        }

        Yaw += look.x;
        Pitch = Mathf.Clamp(Pitch - look.y, _config.MinPitch, _config.MaxPitch);
    }
}
