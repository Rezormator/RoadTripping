using UnityEngine;

[CreateAssetMenu(menuName = "RoadTripping/Camera Bob Config", fileName = "CameraBobConfig")]
public class CameraBobConfig : ScriptableObject
{
    [field: SerializeField] public float Amplitude { get; private set; } = 0.04f;
    [field: SerializeField] public float Frequency { get; private set; } = 1.4f;
    [field: SerializeField] public float ReferenceSpeed { get; private set; } = 4f;
    [field: SerializeField] public float BlendSpeed { get; private set; } = 6f;
}
