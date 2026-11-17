using UnityEngine;

public interface ILoopTraveler
{
    Vector3 Position { get; }
    void Shift(Vector3 offset);
}
