using UnityEngine;

public class PlayerMovementService
{
    private readonly PlayerMovementConfig _config;
    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;

    public float HorizontalSpeed => _horizontalVelocity.magnitude;

    public PlayerMovementService(PlayerMovementConfig config)
    {
        _config = config;
    }

    public Vector3 Tick(Vector2 moveInput, bool sprintHeld, bool jumpPressed,
                        bool isCrouching, bool isGrounded, float yaw, float deltaTime)
    {
        bool isSprinting = sprintHeld && moveInput.y > 0f && !isCrouching;

        float targetSpeed = _config.WalkSpeed;
        if (isCrouching)
            targetSpeed = _config.CrouchSpeed;
        else if (isSprinting)
            targetSpeed = _config.SprintSpeed;

        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 targetVelocity = direction * targetSpeed;

        float rate = moveInput == Vector2.zero ? _config.Deceleration : _config.Acceleration;
        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * deltaTime);
        
        if (isGrounded)
        {
            if (_verticalVelocity < 0f)
                _verticalVelocity = -2f;

            if (jumpPressed && !isCrouching)
                _verticalVelocity = Mathf.Sqrt(2f * _config.Gravity * _config.JumpHeight);
        }

        _verticalVelocity -= _config.Gravity * deltaTime;

        return _horizontalVelocity + Vector3.up * _verticalVelocity;
    }

    public void OnCeilingHit()
    {
        if (_verticalVelocity > 0f)
            _verticalVelocity = 0f;
    }
}
