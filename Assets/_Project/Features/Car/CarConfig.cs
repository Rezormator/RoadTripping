using UnityEngine;

[CreateAssetMenu(menuName = "RoadTripping/Car Config", fileName = "CarConfig")]
public class CarConfig : ScriptableObject
{
    [field: Header("Engine")]
    [field: SerializeField] public float MotorTorque { get; private set; } = 600f;
    [field: SerializeField] public float ReverseTorqueFactor { get; private set; } = 0.5f;
    [field: SerializeField] public float MaxSpeedKmh { get; private set; } = 90f;
    [field: SerializeField] public float MaxReverseSpeedKmh { get; private set; } = 20f;

    [field: Header("Brakes")]
    [field: SerializeField] public float BrakeTorque { get; private set; } = 3000f;
    [field: SerializeField] public float EngineBrakeTorque { get; private set; } = 150f;
    [field: SerializeField] public float HandbrakeTorque { get; private set; } = 5000f;
    [field: SerializeField] public float ParkingBrakeTorque { get; private set; } = 5000f;

    [field: Header("Steering")]
    [field: SerializeField] public float MaxSteerAngle { get; private set; } = 30f;
    [field: SerializeField] public float HighSpeedSteerAngle { get; private set; } = 10f;
    [field: SerializeField] public float SteerSpeed { get; private set; } = 90f;
    [field: SerializeField] public float SteeringWheelRatio { get; private set; } = -6f;

    [field: Header("Speedometer")]
    [field: SerializeField] public float SpeedometerMaxKmh { get; private set; } = 150f;
    [field: SerializeField] public float SpeedometerModelKmh { get; private set; } = 70f;
    [field: SerializeField] public float SpeedometerDegreesPerKmh { get; private set; } = 1.8f;

    [field: Header("Body")]
    [field: SerializeField] public Vector3 CenterOfMass { get; private set; } = new Vector3(0f, 0.45f, 0f);
}
