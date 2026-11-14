public readonly struct CarCommand
{
    public readonly float MotorTorque;
    public readonly float BrakeTorque;
    public readonly float SteerAngle;

    public CarCommand(float motorTorque, float brakeTorque, float steerAngle)
    {
        MotorTorque = motorTorque;
        BrakeTorque = brakeTorque;
        SteerAngle = steerAngle;
    }
}
