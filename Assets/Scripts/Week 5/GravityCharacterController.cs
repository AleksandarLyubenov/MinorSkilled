using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class GravityCharacterController : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpHeight = 2f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 120f;
    public float minPitch = -89f;
    public float maxPitch = 89f;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public LayerMask allowJumpLayers = 0;
    public float groundCheckRadius = 0.25f;
    public float groundCheckDistance = 0.3f;

    [Header("Orientation")]
    public float alignUpSpeed = 10f;

    [Header("Moving Surface Carry")]
    public bool inheritSurfaceVelocity = true;   // if true, platform moves the player
    [Range(0f, 1f)] public float surfaceVelocityBlend = 1.0f;

    [Header("Pushing")]
    public LayerMask pushableLayers = ~0;   // which RBs can be pushed
    public float pushImpulse = 2.5f;        // impulse strength per contact
    public float maxPushAngleFromMove = 120f; // ignore if moving mostly away

    private CharacterController controller;
    private float pitch;
    private float gravVelScalar;
    private Vector3 gravityDir = Vector3.down;
    private float gravityMag = 9.81f;

    // subscription state
    private bool subscribed;

    // carry state (debug)
    private Vector3 dbgCastOrigin, dbgCastDir;
    private bool dbgGroundedGeneral, dbgGroundedJump;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        // hard fallback if no bus
        gravityDir = Physics.gravity.sqrMagnitude > 0.0001f ? Physics.gravity.normalized : Vector3.down;
        gravityMag = Physics.gravity.magnitude > 0.0001f ? Physics.gravity.magnitude : 9.81f;
    }

    void OnEnable() { TrySubscribe(); }
    void OnDisable() { Unsubscribe(); }

    void Update()
    {
        if (!subscribed) TrySubscribe();

        AlignUprightToGravity();
        HandleMouseLook();
        HandleMoveAndJump();
    }

    private void TrySubscribe()
    {
        var bus = GravityBus.Instance;
        if (bus == null) return;

        gravityDir = bus.Direction;
        gravityMag = bus.Magnitude;

        bus.OnGravityChanged -= OnGravityChanged; // idempotent
        bus.OnGravityChanged += OnGravityChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        var bus = GravityBus.Instance;
        if (bus != null) bus.OnGravityChanged -= OnGravityChanged;
        subscribed = false;
    }

    private void OnGravityChanged(Vector3 dir, float mag, Vector3 vec)
    {
        gravityDir = dir;
        gravityMag = mag;
        // Don’t slam into new ground plane; cancel downward component
        gravVelScalar = Mathf.Min(0f, gravVelScalar);
    }

    void AlignUprightToGravity()
    {
        Vector3 targetUp = -gravityDir;
        Vector3 fwdProjected = Vector3.ProjectOnPlane(transform.forward, targetUp).normalized;
        if (fwdProjected.sqrMagnitude < 1e-6f)
            fwdProjected = Vector3.ProjectOnPlane(transform.right, targetUp).normalized;

        Quaternion targetRot = Quaternion.LookRotation(fwdProjected, targetUp);
        float t = 1f - Mathf.Exp(-alignUpSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        transform.Rotate(transform.up, mouseX, Space.World);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (playerCamera)
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var rb = hit.rigidbody;
        if (rb == null || rb.isKinematic) return;
        if (((1 << rb.gameObject.layer) & pushableLayers) == 0) return;

        // Planar move direction this frame (approximate)
        Vector3 planarMove = Vector3.ProjectOnPlane(hit.moveDirection, gravityDir);
        if (planarMove.sqrMagnitude < 1e-6f) return;

        Vector3 pushDir = planarMove.normalized;

        float ang = Vector3.Angle(pushDir, (hit.point - transform.position).normalized);
        if (ang > maxPushAngleFromMove) return;

        rb.AddForce(pushDir * pushImpulse, ForceMode.Impulse);
    }

    void HandleMoveAndJump()
    {
        // Ground with hit info
        RaycastHit hitGeneral, hitJump;
        bool groundedGeneral = SphereGroundCheck(gravityDir, groundMask, out hitGeneral);
        bool groundedJump = SphereGroundCheck(gravityDir, allowJumpLayers, out hitJump);

        dbgGroundedGeneral = groundedGeneral;
        dbgGroundedJump = groundedJump;

        if (groundedGeneral && gravVelScalar > 0f)
            gravVelScalar = 0f;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 camFwd = playerCamera ? playerCamera.transform.forward : transform.forward;
        Vector3 camRight = playerCamera ? playerCamera.transform.right : transform.right;

        camFwd = Vector3.ProjectOnPlane(camFwd, gravityDir).normalized;
        camRight = Vector3.ProjectOnPlane(camRight, gravityDir).normalized;

        Vector3 planar = camRight * x + camFwd * z;
        if (planar.sqrMagnitude > 1f) planar.Normalize();

        Vector3 displacement = planar * moveSpeed * Time.deltaTime;

        // Inherit moving platform velocity while grounded
        if (inheritSurfaceVelocity && groundedGeneral && hitGeneral.collider != null)
        {
            var mover = hitGeneral.collider.GetComponentInParent<MoverKinematic>();
            if (mover != null)
            {
                Vector3 carry = mover.SurfaceVelocityWorld * Time.deltaTime;
                displacement += carry * Mathf.Clamp01(surfaceVelocityBlend);
            }
        }

        controller.Move(displacement);

        // Jump opposite gravity from allowed layers only
        if (groundedJump && Input.GetButtonDown("Jump"))
            gravVelScalar = -Mathf.Sqrt(2f * gravityMag * jumpHeight);

        // Gravity
        gravVelScalar += gravityMag * Time.deltaTime;
        Vector3 gravStep = gravityDir * gravVelScalar * Time.deltaTime;
        controller.Move(gravStep);
    }

    // Ground check along gravity, with hit out
    bool SphereGroundCheck(Vector3 gDir, LayerMask mask, out RaycastHit hit)
    {
        Vector3 castOrigin = transform.position + controller.center
                           - gDir * (controller.height * 0.5f - controller.radius + 0.01f);

        float dist = groundCheckDistance + controller.skinWidth;

        dbgCastOrigin = castOrigin;
        dbgCastDir = gDir;

        return Physics.SphereCast(castOrigin, groundCheckRadius, gDir, out hit, dist, mask, QueryTriggerInteraction.Ignore);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (controller == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(dbgCastOrigin, groundCheckRadius);
        Vector3 end = dbgCastOrigin + dbgCastDir.normalized * (groundCheckDistance + controller.skinWidth);
        Gizmos.DrawLine(dbgCastOrigin, end);
        Gizmos.DrawWireSphere(end, groundCheckRadius);

        if (dbgGroundedGeneral) { Gizmos.color = Color.green; Gizmos.DrawSphere(end, 0.08f); }
        if (dbgGroundedJump) { Gizmos.color = Color.cyan; Gizmos.DrawSphere(end + Vector3.right * 0.08f, 0.08f); }

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + dbgCastDir * 0.75f);
        Gizmos.DrawSphere(transform.position + dbgCastDir * 0.75f, 0.05f);
    }
#endif
}
