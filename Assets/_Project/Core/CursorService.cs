using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

public class CursorService : ICursorService, IInitializable, ITickable
{
    private readonly InputAction _toggle;

    public CursorService()
    {
        _toggle = InputSystem.actions.FindAction("ToggleCursor");
    }

    public bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

    public void Initialize()
    {
        SetLocked(true);
    }

    public void Tick()
    {
        if (_toggle.WasPressedThisFrame())
            SetLocked(!IsLocked);
    }

    private void SetLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
