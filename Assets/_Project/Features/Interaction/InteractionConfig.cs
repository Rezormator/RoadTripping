using UnityEngine;

[CreateAssetMenu(menuName = "RoadTripping/Interaction Config", fileName = "InteractionConfig")]
public class InteractionConfig : ScriptableObject
{
    [field: SerializeField] public float Distance { get; private set; } = 2.5f;
    [field: SerializeField] public LayerMask Mask { get; private set; } = ~0;
    [field: SerializeField] public float SurfaceTolerance { get; private set; } = 0.15f;
}
