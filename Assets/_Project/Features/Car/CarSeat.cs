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
    private CarDrivingService _driving;
    private Collider _collider;

    [Inject]
    public void Construct(IPlayerSeating seating, CarDrivingService driving)
    {
        _seating = seating;
        _driving = driving;
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
        if (isDriverSeat)
            _driving.IsDriven = driving;
    }

    private bool IsDoorOpen()
    {
        return door == null || door.IsOpen;
    }
}
