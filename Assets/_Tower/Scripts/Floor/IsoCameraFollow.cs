using UnityEngine;

// Orbit follow: LEFT-DRAG = yaw (free) + small pitch (±range) that springs back to basePitch on release. Scroll = zoom.
public class IsoCameraFollow : MonoBehaviour
{
    public Transform target;
    public float lookHeight = 1.1f;
    public float distance = 14f;
    public float yaw = 200f;
    public float basePitch = 25f;          // 2026-07-12: 35 -> 25. Lowered 10deg (flatter, more cinematic).
    public float pitchRange = 10f;         // how far pitch can move while dragging (deg)
    public float yawSensitivity = 4f;      // 2026-07-12: 7.5 -> 4 (rotation was still too fast). mouse delta drives it directly
    public float pitchSensitivity = 1.6f;  // kept at yawSensitivity * 0.4
    public float pitchReturnLerp = 6f;     // spring back to basePitch on release
    public float followLerp = 12f;
    public float zoomSpeed = 4f;
    public float minDistance = 6f;
    public float maxDistance = 26f;

    private float pitch;

    void Start()
    {
        if (target == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) target = p.transform;
        }
        pitch = basePitch;
        Apply(true);
    }

    void Apply(bool snap)
    {
        if (target == null) return;
        Vector3 focus = target.position + Vector3.up * lookHeight;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = focus + rot * new Vector3(0f, 0f, -distance);
        transform.position = snap ? desired : Vector3.Lerp(transform.position, desired, followLerp * Time.unscaledDeltaTime);
        transform.rotation = Quaternion.LookRotation(focus - transform.position);
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (Input.GetMouseButton(0))
        {
            yaw += Input.GetAxis("Mouse X") * yawSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * pitchSensitivity;
            pitch = Mathf.Clamp(pitch, basePitch - pitchRange, basePitch + pitchRange);
        }
        else
        {
            pitch = Mathf.Lerp(pitch, basePitch, pitchReturnLerp * Time.unscaledDeltaTime);
        }
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
        Apply(false);
    }
}
