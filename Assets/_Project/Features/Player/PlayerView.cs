using UnityEngine;
using Zenject;

[RequireComponent(typeof(CharacterController))]
public class PlayerView : MonoBehaviour, IPlayerSeating
{
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private CameraEffectsView cameraEffects;
    [SerializeField] private PlayerBodyView body;

    private readonly Collider[] _overlaps = new Collider[8];
    private CharacterController _controller;
    private PlayerService _player;
    private LayerMask _obstacleMask;

    public bool IsSeated => _player != null && _player.IsSeated;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _controller.detectCollisions = false;
    }

    [Inject]
    public void Construct(PlayerService player, PlayerMovementConfig config)
    {
        _player = player;
        _obstacleMask = config.ObstacleMask;
        _player.SetYaw(transform.eulerAngles.y);
    }

    public void SitIn(Transform seat)
    {
        _controller.enabled = false;
        transform.SetParent(seat, true);
        transform.SetPositionAndRotation(seat.position, seat.rotation);
        _player.EnterSeat();
    }

    public void StandUp(Vector3 position, float yaw)
    {
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        _controller.enabled = true;
        _player.ExitSeat(yaw);
    }

    private void Update()
    {
        if (_player == null)
            return;

        float deltaTime = Time.deltaTime;

        if (_player.IsSeated)
            UpdateSeated(deltaTime);
        else
            UpdateOnFoot(deltaTime);

        Vector3 cameraPosition = cameraRoot.localPosition;
        cameraPosition.y = _player.CameraHeight;
        cameraRoot.localPosition = cameraPosition;

        if (cameraEffects != null)
            cameraEffects.Apply(_player.CameraOffset);

        if (body != null)
            body.Apply(_player.HorizontalSpeed, _player.CrouchBlend);
    }

    private void UpdateSeated(float deltaTime)
    {
        _player.Tick(false, true, deltaTime);
        cameraRoot.localRotation = Quaternion.Euler(_player.Pitch, _player.HeadYaw, 0f);
    }

    private void UpdateOnFoot(float deltaTime)
    {
        DetectPush(out Vector3 pushVelocity, out Vector3 pushOut);
        if (pushVelocity != Vector3.zero)
            _player.AddPush(pushVelocity);
        if (pushOut != Vector3.zero)
            _controller.Move(pushOut);

        _player.Tick(_controller.isGrounded, CanStand(), deltaTime);

        transform.rotation = Quaternion.Euler(0f, _player.Yaw, 0f);
        cameraRoot.localRotation = Quaternion.Euler(_player.Pitch, 0f, 0f);

        _controller.height = _player.Height;
        _controller.center = new Vector3(0f, _player.Height / 2f, 0f);

        CollisionFlags flags = _controller.Move(_player.Velocity * deltaTime);
        if ((flags & CollisionFlags.Above) != 0)
            _player.OnCeilingHit();
    }

    private void DetectPush(out Vector3 pushVelocity, out Vector3 pushOut)
    {
        pushVelocity = Vector3.zero;
        pushOut = Vector3.zero;

        float bodyRadius = _controller.radius + _controller.skinWidth;
        float radius = bodyRadius + 0.05f;
        Vector3 center = transform.position + Vector3.up * (_controller.height / 2f);
        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * Mathf.Max(radius, _controller.height - radius);

        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlaps,
                                                   _obstacleMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = _overlaps[i];
            Rigidbody body = other.attachedRigidbody;
            if (body == null)
                continue;

            Vector3 away = Vector3.zero;
            float distance = float.MaxValue;
            foreach (Vector3 sample in new[] { bottom, center, top })
            {
                Vector3 offset = sample - other.ClosestPoint(sample);
                offset.y = 0f;
                if (offset.magnitude < distance)
                {
                    distance = offset.magnitude;
                    away = offset;
                }
            }

            if (away.sqrMagnitude < 0.0001f)
            {
                away = center - body.worldCenterOfMass;
                away.y = 0f;
                distance = 0f;
            }

            away.Normalize();
            float speed = Vector3.Dot(body.GetPointVelocity(center), away);
            if (speed > 0f)
                pushVelocity += away * speed;

            float overlap = bodyRadius - distance;
            if (overlap > 0f && overlap > Vector3.Dot(pushOut, away))
                pushOut += away * (overlap - Vector3.Dot(pushOut, away));
        }
    }

    private bool CanStand()
    {
        float distance = _player.StandHeight - _controller.height;
        if (distance <= 0f)
            return true;

        float radius = _controller.radius;
        Vector3 origin = transform.position + Vector3.up * (_controller.height - radius);

        return !Physics.SphereCast(origin, radius * 0.95f, Vector3.up, out _, distance,
                                   _obstacleMask, QueryTriggerInteraction.Ignore);
    }
}
