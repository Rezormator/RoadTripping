using UnityEngine;

public class CarDrivingService
{
    private const float DirectionChangeSpeed = 0.5f;

    private readonly ICarInput _input;
    private readonly CarConfig _config;
    private float _steer;

    public CarDrivingService(ICarInput input, CarConfig config)
    {
        _input = input;
        _config = config;
    }

    public bool IsDriven { get; set; }
    public float SpeedKmh { get; private set; }

    public float SpeedometerKmh =>
        Mathf.Clamp(SpeedKmh, 0f, Mathf.Min(_config.SpeedometerMaxKmh, _config.MaxSpeedKmh));

    public CarCommand Tick(float forwardSpeed, float deltaTime)
    {
        float throttle = IsDriven ? _input.Throttle : 0f;
        float steerInput = IsDriven ? _input.Steer : 0f;
        float speedKmh = Mathf.Abs(forwardSpeed) * 3.6f;
        SpeedKmh = speedKmh;

        float motor = 0f;
        float brake = 0f;

        if (!IsDriven)
        {
            brake = _config.ParkingBrakeTorque;
        }
        else if (throttle > 0f)
        {
            if (forwardSpeed < -DirectionChangeSpeed)
                brake = throttle * _config.BrakeTorque;
            else if (speedKmh < _config.MaxSpeedKmh)
                motor = throttle * _config.MotorTorque;
        }
        else if (throttle < 0f)
        {
            if (forwardSpeed > DirectionChangeSpeed)
                brake = -throttle * _config.BrakeTorque;
            else if (speedKmh < _config.MaxReverseSpeedKmh)
                motor = throttle * _config.MotorTorque * _config.ReverseTorqueFactor;
        }
        else
        {
            brake = _config.EngineBrakeTorque;
        }

        if (IsDriven && _input.HandbrakeHeld)
        {
            motor = 0f;
            brake = _config.HandbrakeTorque;
        }

        float speedFactor = Mathf.Clamp01(speedKmh / _config.MaxSpeedKmh);
        float steerLimit = Mathf.Lerp(_config.MaxSteerAngle, _config.HighSpeedSteerAngle, speedFactor);
        _steer = Mathf.MoveTowards(_steer, steerInput * steerLimit, _config.SteerSpeed * deltaTime);

        return new CarCommand(motor, brake, _steer);
    }
}
