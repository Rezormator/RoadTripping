using UnityEngine;

[CreateAssetMenu(menuName = "RoadTripping/Player Movement Config", fileName = "PlayerMovementConfig")]
public class PlayerMovementConfig : ScriptableObject
{
    [field: Header("Movement")]
    [field: SerializeField] public float WalkSpeed { get; private set; } = 4f;
    [field: SerializeField] public float SprintSpeed { get; private set; } = 6.5f;
    [field: SerializeField] public float Acceleration { get; private set; } = 20f;
    [field: SerializeField] public float Deceleration { get; private set; } = 25f;

    [field: Header("Jump & Gravity")]
    [field: SerializeField] public float Gravity { get; private set; } = 20f;
    [field: SerializeField] public float JumpHeight { get; private set; } = 1f;

    [field: Header("Height")]
    [field: SerializeField] public float StandHeight { get; private set; } = 1.8f;
    [field: SerializeField] public float StandCameraHeight { get; private set; } = 1.65f;

    [field: Header("Crouch")]
    [field: SerializeField] public float CrouchSpeed { get; private set; } = 2f;
    [field: SerializeField] public float CrouchHeight { get; private set; } = 1.1f;
    [field: SerializeField] public float CrouchCameraHeight { get; private set; } = 0.95f;
    [field: SerializeField] public float CrouchTransitionSpeed { get; private set; } = 6f;
    [field: SerializeField] public LayerMask ObstacleMask { get; private set; } = ~0;

    [field: Header("Look")]
    [field: SerializeField] public float MouseSensitivity { get; private set; } = 0.3f;
    [field: SerializeField] public float MinPitch { get; private set; } = -89.9f;
    [field: SerializeField] public float MaxPitch { get; private set; } = 89.9f;

    [field: Header("Seated")]
    [field: SerializeField] public float SeatedCameraHeight { get; private set; } = 0.75f;
    [field: SerializeField] public float SeatedYawLimit { get; private set; } = 110f;
    [field: SerializeField] public float SeatedMinPitch { get; private set; } = -60f;
    [field: SerializeField] public float SeatedMaxPitch { get; private set; } = 60f;

    [field: Header("Push")]
    [field: SerializeField] public float PushMultiplier { get; private set; } = 1.3f;
    [field: SerializeField] public float PushDecay { get; private set; } = 10f;
}
