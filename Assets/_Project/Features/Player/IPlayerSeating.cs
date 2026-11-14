using UnityEngine;

public interface IPlayerSeating
{
    bool IsSeated { get; }
    void SitIn(Transform seat);
    void StandUp(Vector3 position, float yaw);
}
