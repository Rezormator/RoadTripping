using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPController : MonoBehaviour
{
    public Transform cam;
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;
    public float mouseSensitivity = 0.1f;

    CharacterController cc;
    float yVel, pitch;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // огляд
        Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
        cam.localEulerAngles = new Vector3(pitch, 0f, 0f);

        // рух
        float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
        float z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
        Vector3 move = (transform.right * x + transform.forward * z).normalized;
        float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;

        // гравітація і стрибок
        if (cc.isGrounded && yVel < 0f) yVel = -2f;
        if (cc.isGrounded && kb.spaceKey.wasPressedThisFrame)
            yVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
        yVel += gravity * Time.deltaTime;

        cc.Move((move * speed + Vector3.up * yVel) * Time.deltaTime);

        if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
    }
}