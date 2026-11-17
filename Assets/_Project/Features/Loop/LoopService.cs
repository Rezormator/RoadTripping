using UnityEngine;

public class LoopService
{
    public int Lap { get; private set; }

    public void OnSectorCrossed(int direction)
    {
        Lap += direction;
        Debug.Log($"[Loop] Lap {Lap}");
    }
}
