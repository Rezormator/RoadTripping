using UnityEngine;

public class OpenablePart : MonoBehaviour, IInteractable
{
    [SerializeField] private Vector3 hingeAxis = Vector3.up;
    [SerializeField] private float openAngle = 70f;
    [SerializeField] private float duration = 0.6f;

    private Quaternion _closedRotation;
    private float _progress;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        _closedRotation = transform.localRotation;
    }

    public void Interact()
    {
        IsOpen = !IsOpen;
    }

    private void Update()
    {
        float target = IsOpen ? 1f : 0f;
        if (Mathf.Approximately(_progress, target))
            return;

        _progress = Mathf.MoveTowards(_progress, target, Time.deltaTime / Mathf.Max(duration, 0.01f));
        float eased = Mathf.SmoothStep(0f, 1f, _progress);
        transform.localRotation = _closedRotation * Quaternion.AngleAxis(openAngle * eased, hingeAxis);
    }
}
