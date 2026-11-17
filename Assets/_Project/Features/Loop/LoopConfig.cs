using UnityEngine;

[CreateAssetMenu(menuName = "RoadTripping/Loop Config", fileName = "LoopConfig")]
public class LoopConfig : ScriptableObject
{
    [field: SerializeField] public float SectorLength { get; private set; } = 120f;
}
