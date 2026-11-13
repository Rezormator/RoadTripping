using UnityEngine;

public class CameraEffectsView : MonoBehaviour
{
    public void Apply(Vector3 offset)
    {
        transform.localPosition = offset;
    }
}
