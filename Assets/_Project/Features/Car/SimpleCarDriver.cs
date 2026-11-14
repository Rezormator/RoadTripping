using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class SimpleCarDriver : MonoBehaviour
{
    [Serializable]
    private class Wheel
    {
        public WheelCollider collider;
        public string visualName;
        public bool steer;
        public bool drive;

        [NonSerialized] public Transform visual;
        [NonSerialized] public Quaternion rotationOffset;
        [NonSerialized] public Vector3 pivotOffset;
    }

    private const float DirectionChangeSpeed = 0.5f;

    [SerializeField] private Wheel[] wheels;

    [Header("Engine")]
    [SerializeField] private float motorTorque = 600f;
    [SerializeField] private float reverseTorqueFactor = 0.5f;
    [SerializeField] private float maxSpeedKmh = 90f;
    [SerializeField] private float maxReverseSpeedKmh = 20f;

    [Header("Brakes")]
    [SerializeField] private float brakeTorque = 3000f;
    [SerializeField] private float engineBrakeTorque = 150f;
    [SerializeField] private float handbrakeTorque = 5000f;
    [SerializeField] private float parkingBrakeTorque = 5000f;

    [Header("Steering")]
    [SerializeField] private float maxSteerAngle = 30f;
    [SerializeField] private float highSpeedSteerAngle = 10f;
    [SerializeField] private float steerSpeed = 90f;
    [SerializeField] private string steeringWheelName = "SteeringWheel";
    [SerializeField] private float steeringWheelRatio = -6f;

    [Header("Body")]
    [SerializeField] private Vector3 centerOfMass = new Vector3(0f, 0.45f, 0f);

    private Rigidbody _body;
    private InputAction _move;
    private InputAction _handbrake;
    private float _steer;
    private Transform _steeringWheel;
    private Quaternion _steeringWheelRest;

    public bool IsDriven { get; set; }

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _body.centerOfMass = centerOfMass;
        _move = InputSystem.actions.FindAction("Move");
        _handbrake = InputSystem.actions.FindAction("Jump");

        foreach (Wheel wheel in wheels)
            BindVisual(wheel);

        _steeringWheel = FindChild(transform, steeringWheelName);
        if (_steeringWheel != null)
            _steeringWheelRest = _steeringWheel.localRotation;
    }

    private void FixedUpdate()
    {
        Vector2 input = IsDriven ? _move.ReadValue<Vector2>() : Vector2.zero;
        float throttle = input.y;
        float forwardSpeed = Vector3.Dot(_body.linearVelocity, transform.forward);
        float speedKmh = Mathf.Abs(forwardSpeed) * 3.6f;

        float motor = 0f;
        float brake = 0f;

        if (!IsDriven)
        {
            brake = parkingBrakeTorque;
        }
        else if (throttle > 0f)
        {
            if (forwardSpeed < -DirectionChangeSpeed)
                brake = throttle * brakeTorque;
            else if (speedKmh < maxSpeedKmh)
                motor = throttle * motorTorque;
        }
        else if (throttle < 0f)
        {
            if (forwardSpeed > DirectionChangeSpeed)
                brake = -throttle * brakeTorque;
            else if (speedKmh < maxReverseSpeedKmh)
                motor = throttle * motorTorque * reverseTorqueFactor;
        }
        else
        {
            brake = engineBrakeTorque;
        }

        if (IsDriven && _handbrake.IsPressed())
        {
            motor = 0f;
            brake = handbrakeTorque;
        }

        if (motor != 0f)
            _body.WakeUp();

        float speedFactor = Mathf.Clamp01(speedKmh / maxSpeedKmh);
        float steerLimit = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedFactor);
        _steer = Mathf.MoveTowards(_steer, input.x * steerLimit, steerSpeed * Time.fixedDeltaTime);

        foreach (Wheel wheel in wheels)
        {
            wheel.collider.brakeTorque = brake;
            wheel.collider.motorTorque = wheel.drive ? motor : 0f;

            if (wheel.steer)
                wheel.collider.steerAngle = _steer;
        }
    }

    private void LateUpdate()
    {
        foreach (Wheel wheel in wheels)
        {
            if (wheel.visual == null)
                continue;

            wheel.collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheel.visual.SetPositionAndRotation(position + rotation * wheel.pivotOffset,
                                                rotation * wheel.rotationOffset);
        }

        if (_steeringWheel != null)
            _steeringWheel.localRotation = _steeringWheelRest * Quaternion.AngleAxis(-_steer * steeringWheelRatio, Vector3.forward);
    }

    private void BindVisual(Wheel wheel)
    {
        wheel.visual = FindChild(transform, wheel.visualName);
        if (wheel.visual == null)
            return;

        Quaternion toWheelSpace = Quaternion.Inverse(wheel.collider.transform.rotation);
        Vector3 meshCenter = wheel.visual.GetComponentInChildren<Renderer>().bounds.center;

        wheel.rotationOffset = toWheelSpace * wheel.visual.rotation;
        wheel.pivotOffset = toWheelSpace * (wheel.visual.position - meshCenter);
    }

    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Trim() == name)
                return child;

            Transform found = FindChild(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
