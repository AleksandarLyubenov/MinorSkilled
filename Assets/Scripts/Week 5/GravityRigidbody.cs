using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GravityRigidbody : MonoBehaviour
{
    [Tooltip("Scales gravity for this body (1 = normal).")]
    public float gravityScale = 1f;

    [Header("Velocity Handling")]
    [Tooltip("When gravity switches, remove the component of velocity into the NEW gravity direction (prevents 'snap slam').")]
    public bool clearVelocityIntoGravityOnSwitch = true;

    [Header("Orientation")]
    [Tooltip("If true, rotate so transform.up aligns with -gravity.")]
    public bool alignUpToGravity = false;
    [Tooltip("How quickly to align (s^-1).")]
    public float alignSpeed = 6f;

    private Rigidbody rb;
    private Vector3 lastGravityDir;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }

    void OnEnable()
    {
        if (GravityBus.Instance != null)
            GravityBus.Instance.OnGravityChanged += HandleGravityChanged;
        // Initialize with current bus state if available
        if (GravityBus.Instance != null)
            lastGravityDir = GravityBus.Instance.Direction;
        else
            lastGravityDir = Vector3.down;
    }

    void OnDisable()
    {
        if (GravityBus.Instance != null)
            GravityBus.Instance.OnGravityChanged -= HandleGravityChanged;
        rb.useGravity = false;
    }

    void FixedUpdate()
    {
        var bus = GravityBus.Instance;
        Vector3 gDir = bus ? bus.Direction : Vector3.down;
        float gMag = bus ? bus.Magnitude : 9.81f;

        // Constant-acceleration gravity, unaffected by mass
        rb.AddForce(gDir * (gMag * gravityScale), ForceMode.Acceleration);

        if (alignUpToGravity)
        {
            Vector3 targetUp = -gDir;
            // Keep current forward projected to avoid roll spikes
            Vector3 fwdProj = Vector3.ProjectOnPlane(transform.forward, targetUp).normalized;
            if (fwdProj.sqrMagnitude < 1e-6f)
                fwdProj = Vector3.ProjectOnPlane(transform.right, targetUp).normalized;

            Quaternion target = Quaternion.LookRotation(fwdProj, targetUp);
            float t = 1f - Mathf.Exp(-alignSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, t));
        }
    }

    private void HandleGravityChanged(Vector3 newDir, float mag, Vector3 vec)
    {
        if (clearVelocityIntoGravityOnSwitch)
        {
            // Remove velocity component into new gravity (prevents immediate impact)
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = Vector3.ProjectOnPlane(v, newDir);
        }
        lastGravityDir = newDir;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var bus = GravityBus.Instance;
        Vector3 gDir = bus ? bus.Direction : Vector3.down;
        Vector3 origin = transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + gDir * 0.75f);
        Gizmos.DrawSphere(origin + gDir * 0.75f, 0.05f);
    }
#endif
}
