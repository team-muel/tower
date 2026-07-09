using Tower.Core;
using UnityEngine;

// WASD (camera-relative) + right-click move-to, faces movement, snaps to ground so you can walk up slopes.
public class PlayerIsoController : MonoBehaviour
{
    public float moveSpeed = 6f;
    public float turnSpeed = 12f;
    public float arriveDistance = 0.25f;
    public float groundSnapUp = 3f;
    public float groundSnapDown = 8f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private bool normalizeSpeed = true;
    [SerializeField, Min(0f)] private float speedDamping = 0.12f;

    private Transform camT;
    private Camera cam;
    private Vector3 destination;
    private bool hasDestination;
    private float fallbackY;
    private Vector3 _prevPos;
    private int _speedHash;
    private bool _animatorReady;

    void Start()
    {
        var camGo = GameObject.Find("PlayerCamera");
        if (camGo != null) { camT = camGo.transform; cam = camGo.GetComponent<Camera>(); }
        fallbackY = transform.position.y;
        _prevPos = transform.position;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        _speedHash = Animator.StringToHash(speedParameter);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == _speedHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                _animatorReady = true;
                break;
            }
        }

        if (!_animatorReady)
        {
            Debug.LogWarning(
                $"PlayerIsoController disabled Animator driving because float parameter '{speedParameter}' was not found.",
                this);
        }
    }

    void Update()
    {
        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (camT != null)
        {
            fwd = camT.forward; fwd.y = 0; fwd.Normalize();
            right = camT.right; right.y = 0; right.Normalize();
        }
        float h = 0f, v = 0f;
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;
        if (Input.GetKey(KeyCode.D)) h += 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        Vector3 wasd = fwd * v + right * h;

        if (Input.GetMouseButtonDown(1) && cam != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 600f)) { destination = hit.point; hasDestination = true; }
        }

        Vector3 move = Vector3.zero;
        if (wasd.sqrMagnitude > 0.01f) { hasDestination = false; move = wasd.normalized; }
        else if (hasDestination)
        {
            Vector3 to = destination - transform.position; to.y = 0f;
            if (to.magnitude <= arriveDistance) hasDestination = false;
            else move = to.normalized;
        }

        Vector3 pos = transform.position;
        if (move.sqrMagnitude > 0.01f)
        {
            pos += move * moveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), turnSpeed * Time.deltaTime);
        }

        Vector3 probe = new Vector3(pos.x, pos.y + groundSnapUp, pos.z);
        var hits = Physics.RaycastAll(probe, Vector3.down, groundSnapUp + groundSnapDown);
        float best = float.NegativeInfinity; bool found = false;
        foreach (var hh in hits)
        {
            if (hh.collider.transform.root == transform.root) continue;
            if (hh.point.y > best) { best = hh.point.y; found = true; }
        }
        pos.y = found ? best : fallbackY;
        transform.position = pos;

        if (_animatorReady)
        {
            float planar = PlayerLocomotion.PlanarSpeed(_prevPos, transform.position, Time.deltaTime);
            float value = normalizeSpeed ? PlayerLocomotion.SpeedFactor(planar, moveSpeed) : planar;
            animator.SetFloat(_speedHash, value, speedDamping, Time.deltaTime);
        }
        _prevPos = transform.position;
    }
}
