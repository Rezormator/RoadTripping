using UnityEngine;
using Zenject;

public class CursorLocker : IInitializable
{
    public void Initialize()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }
}
