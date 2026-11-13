using UnityEngine;

public class PlayerBodyView : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int CrouchHash = Animator.StringToHash("Crouch");

    [SerializeField] private Animator animator;
    [SerializeField] private bool hideHead = true;

    private Transform _head;
    private bool _hasSpeed;
    private bool _hasCrouch;

    private void Awake()
    {
        if (animator == null)
            return;

        if (animator.isHuman)
            _head = animator.GetBoneTransform(HumanBodyBones.Head);

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == SpeedHash) _hasSpeed = true;
            if (parameter.nameHash == CrouchHash) _hasCrouch = true;
        }
    }

    public void Apply(float speed, float crouchBlend)
    {
        if (animator == null)
            return;

        if (_hasSpeed) animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
        if (_hasCrouch) animator.SetFloat(CrouchHash, crouchBlend);
    }

    private void LateUpdate()
    {
        if (hideHead && _head != null)
            _head.localScale = Vector3.zero;
    }
}
