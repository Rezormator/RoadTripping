using UnityEngine;
using Zenject;

[RequireComponent(typeof(Collider))]
public class CarSeat : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform sitPoint;
    [SerializeField] private OpenablePart door;
    [SerializeField] private CarSeatExit exit;

    private IPlayerSeating _seating;
    private Collider _collider;

    [Inject]
    public void Construct(IPlayerSeating seating)
    {
        _seating = seating;
    }

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    public void Interact()
    {
        if (_seating == null || _seating.IsSeated || !IsDoorOpen())
            return;

        _seating.SitIn(sitPoint);
        _collider.enabled = false;
        exit.SetAvailable(true);
    }

    public void Leave(Transform exitPoint)
    {
        if (!IsDoorOpen())
            return;

        _seating.StandUp(exitPoint.position, exitPoint.eulerAngles.y);
        _collider.enabled = true;
        exit.SetAvailable(false);
    }

    private bool IsDoorOpen()
    {
        return door == null || door.IsOpen;
    }
}
