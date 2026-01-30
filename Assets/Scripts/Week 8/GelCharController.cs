using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class GelGravityCharacterController : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;

    [Header("Movement (base)")]
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
    Vector3 planarVel;

    [Header("Orientation")]
    public float alignUpSpeed = 10f;

    [Header("Surface/Gel")]
    [Tooltip("Radius used to detect SurfaceVolume triggers at the feet.")]
    public float surfaceProbeRadius = 0.35f;
    [Tooltip("Which layers contain SurfaceVolume triggers (usually Everything).")]
    public LayerMask surfaceVolumeMask = ~0;

    [Header("Pushing (RB)")]
    public LayerMask pushableLayers = ~0;
    public float pushImpulse = 2.5f;
    public float maxPushAngleFromMove = 120f;

    [Header("Wall Running")]
    [Tooltip("How long after losing contact we keep wall-run state (coyote time).")]
    public float wallContactGraceTime = 0.2f;

    private CharacterController controller;
    private float pitch;
    private float gravVelScalar;
    private Vector3 gravityDir = Vector3.down;
    private float gravityMag = 9.81f;

    // surface state
    private SurfaceProfile currentSurface;
    private Vector3 dbgCastOrigin, dbgCastDir;
    private bool dbgGroundedGeneral, dbgGroundedJump;

    // wall-run state
    private SurfaceProfile wallSurface;
    private Vector3 wallNormal;
    private float wallContactTime;

    // subscription to global gravity
    private bool subscribed;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();

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

    void TrySubscribe()
    {
        var bus = GravityBus.Instance;
        if (bus == null) return;

        gravityDir = bus.Direction;
        gravityMag = bus.Magnitude;

        bus.OnGravityChanged -= OnGravityChanged;
        bus.OnGravityChanged += OnGravityChanged;
        subscribed = true;
    }

    void Unsubscribe()
    {
        var bus = GravityBus.Instance;
        if (bus != null) bus.OnGravityChanged -= OnGravityChanged;
        subscribed = false;
    }

    void OnGravityChanged(Vector3 dir, float mag, Vector3 vec)
    {
        gravityDir = dir;
        gravityMag = mag;
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
        // Push rigidbodies
        var rb = hit.rigidbody;
        if (rb != null && !rb.isKinematic && ((1 << rb.gameObject.layer) & pushableLayers) != 0)
        {
            Vector3 planarMove = Vector3.ProjectOnPlane(hit.moveDirection, gravityDir);
            if (planarMove.sqrMagnitude > 1e-6f)
            {
                Vector3 pushDir = planarMove.normalized;
                float ang = Vector3.Angle(pushDir, (hit.point - transform.position).normalized);
                if (ang <= maxPushAngleFromMove)
                    rb.AddForce(pushDir * pushImpulse, ForceMode.Impulse);
            }
        }

        // Wall-run detection
        // upDot = 1 -> floor, -1 -> ceiling, 0 -> perfect wall
        float upDot = Mathf.Abs(Vector3.Dot(-gravityDir, hit.normal));
        // Mostly-vertical surfaces are potential walls
        if (upDot < 0.3f)
        {
            SurfaceProfile prof = SurfaceResolver.Resolve(
                hit.point,
                surfaceProbeRadius,
                surfaceVolumeMask,
                hit.collider
            );

            if (prof != null && prof.allowWallRun)
            {
                wallSurface = prof;
                wallNormal = hit.normal;
                wallContactTime = wallContactGraceTime; // refresh coyote timer
            }
        }
    }

    void HandleMoveAndJump()
    {
        // Grounding (with hits for surface resolver)
        RaycastHit hitGeneral, hitJump;
        bool groundedGeneral = SphereGroundCheck(gravityDir, groundMask, out hitGeneral);
        bool groundedJump = SphereGroundCheck(gravityDir, allowJumpLayers, out hitJump);

        dbgGroundedGeneral = groundedGeneral;
        dbgGroundedJump = groundedJump;

        if (groundedGeneral && gravVelScalar > 0f) gravVelScalar = 0f;

        if (groundedGeneral && planarVel.sqrMagnitude < 1e-6f)
            planarVel = Vector3.zero;

        // decay wall contact timer
        wallContactTime = Mathf.Max(0f, wallContactTime - Time.deltaTime);
        bool hasWall = (!groundedGeneral &&
                        wallContactTime > 0f &&
                        wallSurface != null &&
                        wallSurface.allowWallRun);

        if (groundedGeneral)
        {
            // if on the ground, clear wall surface
            wallSurface = null;
            hasWall = false;
        }

        // Resolve current surface profile under feet
        currentSurface = SurfaceResolver.Resolve(
            pos: transform.position + controller.center - gravityDir * (controller.height * 0.5f - controller.radius),
            probeRadius: surfaceProbeRadius,
            triggerMask: surfaceVolumeMask,
            ground: groundedGeneral ? hitGeneral.collider : null
        );

        // Camera-relative desired velocity on the ground plane
        float ix = Input.GetAxisRaw("Horizontal");
        float iz = Input.GetAxisRaw("Vertical");

        Vector3 camFwd = playerCamera ? playerCamera.transform.forward : transform.forward;
        Vector3 camRight = playerCamera ? playerCamera.transform.right : transform.right;
        camFwd = Vector3.ProjectOnPlane(camFwd, gravityDir).normalized;
        camRight = Vector3.ProjectOnPlane(camRight, gravityDir).normalized;

        Vector3 wishDir = (camRight * ix + camFwd * iz);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        // Effective params from surface
        float effMoveSpeed = moveSpeed;
        float effJumpHeight = jumpHeight;
        float effAccelMul = 1f;
        float effDecelMul = 1f;
        float effSticky = 0f;
        float effBounce = 0f;
        float effGroundFriction = 8f;
        float effAirFriction = 0.5f;
        float effAirControl = 0.2f;

        if (currentSurface)
        {
            effMoveSpeed *= currentSurface.moveSpeedMul;
            effJumpHeight *= currentSurface.jumpMul;
            effAccelMul *= currentSurface.accelMul;
            effDecelMul *= currentSurface.decelMul;
            effSticky = currentSurface.sticky;
            effBounce = currentSurface.bounciness;
            effGroundFriction = currentSurface.groundFriction;
            effAirFriction = currentSurface.airFriction;
            effAirControl = currentSurface.airControl;
        }

        // If in wall-run state, apply wall surface modifiers
        if (hasWall)
        {
            effMoveSpeed *= wallSurface.wallRunMoveSpeedMul;
            effAirControl = Mathf.Max(effAirControl, 0.6f);
        }

        // Build desired ground velocity
        Vector3 desiredVel = wishDir * effMoveSpeed;

        // Acceleration rate (use higher when reversing direction)
        float accelRate = 12f * effAccelMul;
        if (Vector3.Dot(planarVel, desiredVel) < 0f)
            accelRate = 14f * effDecelMul;

        // Integrate planar velocity
        if (groundedGeneral)
        {
            // Accelerate toward desired
            Vector3 delta = desiredVel - planarVel;
            float maxDelta = accelRate * Time.deltaTime;
            if (delta.magnitude > maxDelta) delta = delta.normalized * maxDelta;
            planarVel += delta;

            // Friction when no input
            if (wishDir.sqrMagnitude < 0.0001f)
            {
                float friction = effGroundFriction + effSticky * 10f;
                float decay = Mathf.Exp(-friction * Time.deltaTime);
                planarVel *= decay;
            }

            // Strictly in the ground plane
            planarVel = Vector3.ProjectOnPlane(planarVel, gravityDir);
        }
        else
        {
            // Air control
            if (wishDir.sqrMagnitude > 0f && effAirControl > 0f)
            {
                Vector3 airDelta = (desiredVel - planarVel) * (effAirControl * 4f) * Time.deltaTime;
                planarVel += Vector3.ProjectOnPlane(airDelta, gravityDir);
            }
            // Air friction (very small)
            float decay = Mathf.Exp(-effAirFriction * Time.deltaTime);
            planarVel *= decay;
        }

        // small positional stick into the wall to feel attached
        if (hasWall)
        {
            Vector3 stick = -wallNormal.normalized * 0.1f * Time.deltaTime;
            controller.Move(stick);
        }

        // Apply planar motion
        controller.Move(planarVel * Time.deltaTime);

        // Jump
        if (groundedJump && Input.GetButtonDown("Jump"))
            gravVelScalar = -Mathf.Sqrt(2f * gravityMag * effJumpHeight);

        // Gravity integrate & bounce
        float prevGravVel = gravVelScalar;
        gravVelScalar += gravityMag * Time.deltaTime;
        if (groundedGeneral && prevGravVel > 0f && effBounce > 0f)
            gravVelScalar = -prevGravVel * effBounce;

        // Clamp falling speed while wall-running
        if (hasWall)
        {
            float maxDown = wallSurface.wallRunMaxFallSpeed;
            gravVelScalar = Mathf.Min(gravVelScalar, maxDown);
        }

        Vector3 gravStep = gravityDir * gravVelScalar * Time.deltaTime;
        controller.Move(gravStep);
    }

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

        if (currentSurface)
        {
            Gizmos.color = currentSurface.tint;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.05f, new Vector3(0.2f, 0.2f, 0.2f));
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + dbgCastDir * 0.75f);
        Gizmos.DrawSphere(transform.position + dbgCastDir * 0.75f, 0.05f);
    }
#endif
}
