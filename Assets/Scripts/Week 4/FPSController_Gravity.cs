using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController_GravityAligned : MonoBehaviour
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
    public LayerMask allowJumpLayers;
    public float groundCheckRadius = 0.25f;
    public float groundCheckDistance = 0.2f;

    [Header("Orientation")]
    public float alignUpSpeed = 8f;

    private CharacterController controller;
    private float pitch;
    private float gravVelScalar;
    private Vector3 lastGravityDir;

    // Debug info
    private bool lastGroundedGeneral;
    private bool lastGroundedJump;
    private Vector3 lastCastOrigin;
    private Vector3 lastCastDir;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var gm = GravityManager.Instance;
        Vector3 gDir = gm ? gm.GravityDir : Vector3.down;
        float gMag = gm ? gm.gravityMagnitude : 9.81f;

        AlignUprightToGravity(gDir);
        HandleMouseLook();
        HandleMoveAndJump(gDir, gMag);
    }

    void AlignUprightToGravity(Vector3 gDir)
    {
        Vector3 targetUp = -gDir;
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

        Vector3 upAxis = transform.up;
        transform.Rotate(upAxis, mouseX, Space.World);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (playerCamera)
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMoveAndJump(Vector3 gDir, float gMag)
    {
        lastGravityDir = gDir;

        bool groundedGeneral = SphereGroundCheck(gDir, groundMask, out _);
        bool groundedJump = SphereGroundCheck(gDir, allowJumpLayers, out _);

        lastGroundedGeneral = groundedGeneral;
        lastGroundedJump = groundedJump;

        if (groundedGeneral && gravVelScalar > 0f)
            gravVelScalar = 0f;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 camFwd = playerCamera ? playerCamera.transform.forward : transform.forward;
        Vector3 camRight = playerCamera ? playerCamera.transform.right : transform.right;

        camFwd = Vector3.ProjectOnPlane(camFwd, gDir).normalized;
        camRight = Vector3.ProjectOnPlane(camRight, gDir).normalized;

        Vector3 planar = camRight * x + camFwd * z;
        if (planar.sqrMagnitude > 1f) planar.Normalize();

        controller.Move(planar * moveSpeed * Time.deltaTime);

        if (groundedJump && Input.GetButtonDown("Jump"))
        {
            gravVelScalar = -Mathf.Sqrt(2f * gMag * jumpHeight);
        }

        gravVelScalar += gMag * Time.deltaTime;
        Vector3 gravStep = gDir * gravVelScalar * Time.deltaTime;
        controller.Move(gravStep);
    }

    bool SphereGroundCheck(Vector3 gDir, LayerMask mask, out RaycastHit hit)
    {
        Vector3 castOrigin = transform.position + controller.center
                           - gDir * (controller.height * 0.5f - controller.radius + 0.01f);
        lastCastOrigin = castOrigin;
        lastCastDir = gDir;

        float dist = groundCheckDistance + controller.skinWidth;
        return Physics.SphereCast(castOrigin, groundCheckRadius, gDir, out hit, dist, mask, QueryTriggerInteraction.Ignore);
    }

    // Draw debug gizmos in Scene view
    void OnDrawGizmosSelected()
    {
        if (controller == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(lastCastOrigin, groundCheckRadius);

        Vector3 end = lastCastOrigin + lastCastDir.normalized * (groundCheckDistance + controller.skinWidth);
        Gizmos.DrawLine(lastCastOrigin, end);
        Gizmos.DrawWireSphere(end, groundCheckRadius);

        if (lastGroundedGeneral)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(end, 0.1f);
        }
        if (lastGroundedJump)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(end + Vector3.right * 0.1f, 0.1f);
        }
    }
}
