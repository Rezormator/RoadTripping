using UnityEngine;
using Zenject;

[RequireComponent(typeof(Collider))]
public class CarSeat : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform sitPoint;
    [SerializeField] private OpenablePart door;
    [SerializeField] private CarSeatExit exit;
    [SerializeField] private bool isDriverSeat;

    private IPlayerSeating _seating;
    private Collider _collider;
    private SimpleCarDriver _car;

    [Inject]
    public void Construct(IPlayerSeating seating)
    {
        _seating = seating;
    }

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _car = GetComponentInParent<SimpleCarDriver>();
    }

    public void Interact()
    {
        if (_seating == null || _seating.IsSeated || !IsDoorOpen())
            return;

        _seating.SitIn(sitPoint);
        _collider.enabled = false;
        exit.SetAvailable(true);
        SetDriving(true);
    }

    public void Leave(Transform exitPoint)
    {
        if (!IsDoorOpen())
            return;

        SetDriving(false);
        _seating.StandUp(exitPoint.position, exitPoint.eulerAngles.y);
        _collider.enabled = true;
        exit.SetAvailable(false);
    }

    private void SetDriving(bool driving)
    {
        if (isDriverSeat && _car != null)
            _car.IsDriven = driving;
    }

    private bool IsDoorOpen()
    {
        return door == null || door.IsOpen;
    }
}
