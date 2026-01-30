using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class RBGravityCharacter : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float airControl = 0.4f;
    public float jumpHeight = 2f;
    public float maxSlopeAngle = 65f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 120f;
    public float minPitch = -89f;
    public float maxPitch = 89f;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public LayerMask allowJumpLayers = 0;
    public float groundProbeDistance = 0.2f;
    public float stepInset = 0.02f;

    [Header("Collision Sweeps")]
    public int slideIterations = 2;
    public float shell = 0.02f;

    [Header("Orientation")]
    public float alignUpSpeed = 10f;

    private Rigidbody rb;
    private CapsuleCollider col;
    private float pitch;
    private Vector3 gravityDir = Vector3.down;
    private float gravityMag = 9.81f;

    private float radius, height;

    Vector3 LocalTop => Vector3.up * (height * 0.5f - radius);
    Vector3 LocalBottom => -LocalTop;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.None;
        rb.angularDamping = 0f;
        rb.maxAngularVelocity = 0.01f;

        radius = col.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        height = Mathf.Max(col.height * Mathf.Abs(transform.lossyScale.y), radius * 2f);
    }

    void OnEnable()
    {
        var bus = GravityBus.Instance;
        if (bus != null)
        {
            gravityDir = bus.Direction;
            gravityMag = bus.Magnitude;
            bus.OnGravityChanged += OnGravityChanged;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        var bus = GravityBus.Instance;
        if (bus != null) bus.OnGravityChanged -= OnGravityChanged;
    }

    void Update()
    {
        AlignUprightToGravity();

        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        transform.Rotate(transform.up, mouseX, Space.World);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (playerCamera) playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void FixedUpdate()
    {
        bool groundedGeneral = ProbeGround(groundMask, out RaycastHit groundHit, out float groundAngle);
        bool groundedJump = ProbeGround(allowJumpLayers, out _, out _);

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 camFwd = playerCamera ? playerCamera.transform.forward : transform.forward;
        Vector3 camRight = playerCamera ? playerCamera.transform.right : transform.right;
        camFwd = Vector3.ProjectOnPlane(camFwd, gravityDir).normalized;
        camRight = Vector3.ProjectOnPlane(camRight, gravityDir).normalized;

        Vector3 desiredPlanar = (camRight * x + camFwd * z);
        if (desiredPlanar.sqrMagnitude > 1f) desiredPlanar.Normalize();
        desiredPlanar *= moveSpeed * (groundedGeneral ? 1f : airControl);

        Vector3 v = rb.linearVelocity;
        Vector3 vN = Vector3.Project(v, gravityDir);
        Vector3 vT = v - vN;

        vT = desiredPlanar;

        if (groundedJump && Input.GetButtonDown("Jump") && groundAngle <= maxSlopeAngle)
        {
            vN = Vector3.zero;
            float jumpV = Mathf.Sqrt(2f * gravityMag * jumpHeight);
            vN += (-gravityDir) * jumpV;
        }

        vN += gravityDir * (gravityMag * Time.fixedDeltaTime);

        Vector3 vNext = vT + vN;
        Vector3 delta = vNext * Time.fixedDeltaTime;

        Vector3 newPos = ResolveCollisions(transform.position, delta);
        rb.MovePosition(newPos);
        rb.linearVelocity = (newPos - transform.position) / Time.fixedDeltaTime;
        rb.angularVelocity = Vector3.zero;
    }

    void AlignUprightToGravity()
    {
        Vector3 targetUp = -gravityDir;
        Vector3 fwdProjected = Vector3.ProjectOnPlane(transform.forward, targetUp).normalized;
        if (fwdProjected.sqrMagnitude < 1e-6f)
            fwdProjected = Vector3.ProjectOnPlane(transform.right, targetUp).normalized;

        Quaternion targetRot = Quaternion.LookRotation(fwdProjected, targetUp);
        float t = 1f - Mathf.Exp(-alignUpSpeed * Time.deltaTime);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, t));
    }

    void OnGravityChanged(Vector3 dir, float mag, Vector3 vec)
    {
        gravityDir = dir;
        gravityMag = mag;
        rb.linearVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, dir);
    }

    void GetCapsule(out Vector3 p1, out Vector3 p2)
    {
        Vector3 up = transform.up;
        Vector3 center = transform.position + col.center;
        p1 = center + up * (height * 0.5f - radius);
        p2 = center - up * (height * 0.5f - radius);
    }

    bool ProbeGround(LayerMask mask, out RaycastHit hit, out float slopeAngle)
    {
        GetCapsule(out var top, out var bottom);
        Vector3 origin = bottom - transform.up * (radius - shell);
        float dist = groundProbeDistance + shell;

        if (Physics.SphereCast(origin, radius - shell, gravityDir, out hit, dist, mask, QueryTriggerInteraction.Ignore))
        {
            slopeAngle = Vector3.Angle(hit.normal, -gravityDir);
            return true;
        }
        slopeAngle = 180f;
        return false;
    }

    Vector3 ResolveCollisions(Vector3 startPos, Vector3 desiredMove)
    {
        Vector3 pos = startPos;
        Vector3 remaining = desiredMove;

        for (int i = 0; i < slideIterations; i++)
        {
            GetCapsule(out var top, out var bottom);
            Vector3 dir = remaining.normalized;
            float len = remaining.magnitude;
            if (len <= 1e-6f) break;

            if (Physics.CapsuleCast(top, bottom, radius - shell, dir, out RaycastHit hit, len + shell, groundMask, QueryTriggerInteraction.Ignore))
            {
                float travel = Mathf.Max(0f, hit.distance - shell);
                pos += dir * travel;
                Vector3 slideDir = Vector3.ProjectOnPlane(remaining - dir * travel, hit.normal);
                remaining = slideDir;
                pos += hit.normal * stepInset;
            }
            else
            {
                pos += remaining;
                break;
            }
        }
        return pos;
    }
}
