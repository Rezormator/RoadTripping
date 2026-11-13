using UnityEngine;

public class CameraBobService
{
    private readonly CameraBobConfig _config;
    private float _phase;
    private float _intensity;

    public Vector3 Offset { get; private set; }

    public CameraBobService(CameraBobConfig config)
    {
        _config = config;
    }

    public void Tick(float speed, bool isGrounded, float deltaTime)
    {
        float target = isGrounded && _config.ReferenceSpeed > 0f ? speed / _config.ReferenceSpeed : 0f;
        _intensity = Mathf.MoveTowards(_intensity, target, _config.BlendSpeed * deltaTime);

        _phase += speed * _config.Frequency * deltaTime;

        float x = Mathf.Cos(_phase) * _config.Amplitude * 0.5f;
        float y = Mathf.Sin(_phase * 2f) * _config.Amplitude;
        Offset = new Vector3(x, y, 0f) * _intensity;
    }
}
