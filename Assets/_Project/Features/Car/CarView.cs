using System;
using UnityEngine;
using Zenject;

[RequireComponent(typeof(Rigidbody))]
public class CarView : MonoBehaviour, ILoopTraveler
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

    [SerializeField] private Wheel[] wheels;
    [SerializeField] private string steeringWheelName = "SteeringWheel";
    [SerializeField] private string speedometerNeedleName = "Needle_Speedometer";

    private Rigidbody _body;
    private CarDrivingService _driving;
    private CarConfig _config;
    private float _steerAngle;
    private Transform _steeringWheel;
    private Quaternion _steeringWheelRest;
    private Transform _speedometerNeedle;
    private Quaternion _speedometerNeedleRest;

    [Inject]
    public void Construct(CarDrivingService driving, CarConfig config)
    {
        _driving = driving;
        _config = config;
    }

    private void Start()
    {
        _body = GetComponent<Rigidbody>();
        _body.centerOfMass = WheelbaseCenter() + _config.CenterOfMass;

        foreach (Wheel wheel in wheels)
            BindVisual(wheel);

        _steeringWheel = FindChild(transform, steeringWheelName);
        if (_steeringWheel != null)
            _steeringWheelRest = _steeringWheel.localRotation;

        _speedometerNeedle = FindChild(transform, speedometerNeedleName);
        if (_speedometerNeedle != null)
            _speedometerNeedleRest = _speedometerNeedle.localRotation;
    }

    private void FixedUpdate()
    {
        if (_driving == null || _body == null)
            return;

        float forwardSpeed = Vector3.Dot(_body.linearVelocity, transform.forward);
        CarCommand command = _driving.Tick(forwardSpeed, Time.fixedDeltaTime);
        _steerAngle = command.SteerAngle;

        if (command.MotorTorque != 0f)
            _body.WakeUp();

        foreach (Wheel wheel in wheels)
        {
            wheel.collider.brakeTorque = command.BrakeTorque;
            wheel.collider.motorTorque = wheel.drive ? command.MotorTorque : 0f;

            if (wheel.steer)
                wheel.collider.steerAngle = command.SteerAngle;
        }
    }

    private void LateUpdate()
    {
        if (_config == null)
            return;

        foreach (Wheel wheel in wheels)
        {
            if (wheel.visual == null)
                continue;

            wheel.collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheel.visual.SetPositionAndRotation(position + rotation * wheel.pivotOffset,
                                                rotation * wheel.rotationOffset);
        }

        if (_steeringWheel != null)
            _steeringWheel.localRotation = _steeringWheelRest *
                Quaternion.AngleAxis(-_steerAngle * _config.SteeringWheelRatio, Vector3.forward);

        if (_speedometerNeedle != null && _driving != null)
        {
            float needleAngle = (_driving.SpeedometerKmh - _config.SpeedometerModelKmh) * _config.SpeedometerDegreesPerKmh;
            _speedometerNeedle.localRotation = _speedometerNeedleRest * Quaternion.AngleAxis(needleAngle, Vector3.forward);
        }
    }

    public Vector3 Position => transform.position;

    public void Shift(Vector3 offset)
    {
        Rigidbody body = _body != null ? _body : GetComponent<Rigidbody>();
        body.position += offset;
        transform.position += offset;
    }

    private Vector3 WheelbaseCenter()
    {
        if (wheels.Length == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (Wheel wheel in wheels)
            sum += transform.InverseTransformPoint(wheel.collider.transform.position);

        Vector3 center = sum / wheels.Length;
        return new Vector3(center.x, 0f, center.z);
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
