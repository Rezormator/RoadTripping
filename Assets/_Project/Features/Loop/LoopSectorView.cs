using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class LoopSectorView : MonoBehaviour
{
    private LoopService _loop;
    private LoopConfig _config;
    private List<ILoopTraveler> _travelers;

    [Inject]
    public void Construct(LoopService loop, LoopConfig config, List<ILoopTraveler> travelers)
    {
        _loop = loop;
        _config = config;
        _travelers = travelers;
    }

    private void FixedUpdate()
    {
        if (_travelers == null)
            return;

        Vector3 axis = transform.forward;
        float length = _config.SectorLength;
        bool shifted = false;

        foreach (ILoopTraveler traveler in _travelers)
        {
            float along = Vector3.Dot(traveler.Position - transform.position, axis);

            int direction = 0;
            if (along > length)
                direction = 1;
            else if (along < 0f)
                direction = -1;

            if (direction == 0)
                continue;

            traveler.Shift(-direction * length * axis);
            _loop.OnSectorCrossed(direction);
            shifted = true;
        }

        if (shifted)
            Physics.SyncTransforms();
    }

    private void OnDrawGizmos()
    {
        if (_config == null)
            return;

        Gizmos.color = Color.yellow;
        Vector3 start = transform.position;
        Vector3 end = start + transform.forward * _config.SectorLength;
        Gizmos.DrawLine(start + transform.right * -6f, start + transform.right * 6f);
        Gizmos.DrawLine(end + transform.right * -6f, end + transform.right * 6f);
    }
}
