using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CarSeatExit : MonoBehaviour, IFallbackInteractable
{
    [SerializeField] private CarSeat seat;
    [SerializeField] private Transform exitPoint;

    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.enabled = false;
    }

    public void SetAvailable(bool available)
    {
        _collider.enabled = available;
    }

    public void Interact()
    {
        seat.Leave(exitPoint);
    }
}
