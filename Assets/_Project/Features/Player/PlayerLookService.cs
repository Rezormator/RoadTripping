using UnityEngine;

public class PlayerLookService
{
    private readonly PlayerMovementConfig _config;

    public float Yaw { get; private set; }
    public float Pitch { get; private set; }

    public PlayerLookService(PlayerMovementConfig config)
    {
        _config = config;
    }

    public void SetYaw(float yaw)
    {
        Yaw = yaw;
    }

    public void Tick(Vector2 lookInput)
    {
        Vector2 look = lookInput * _config.MouseSensitivity;
        Yaw += look.x;
        Pitch = Mathf.Clamp(Pitch - look.y, _config.MinPitch, _config.MaxPitch);
    }
}
