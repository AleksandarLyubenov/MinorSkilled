using UnityEngine;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(SphereCollider))]
public class GodCameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 12f;
    public float fastMultiplier = 3f;
    public float slowMultiplier = 0.25f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;

    [Tooltip("Key to toggle the cursor visibility (Free Mouse).")]
    public KeyCode toggleCursorKey = KeyCode.Tab;
    public bool lockCursorOnStart = true;

    [Header("Collision")]
    public LayerMask collisionMask = ~0;
    public float collisionSkin = 0.05f;

    [Header("Up Mode")]
    public bool followGravityUp = true;
    public float upAlignSpeed = 5f;

    // state
    private SphereCollider body;
    private Quaternion orientation;
    private Vector3 gravityDir = Vector3.down;
    private bool subscribed;
    private bool isCursorLocked = true;

    void Awake()
    {
        body = GetComponent<SphereCollider>();
        orientation = transform.rotation;

        gravityDir = Physics.gravity.sqrMagnitude > 0.0001f
            ? Physics.gravity.normalized
            : Vector3.down;

        isCursorLocked = lockCursorOnStart;
        UpdateCursorState();
    }

    void OnEnable() { TrySubscribe(); }
    void OnDisable()
    {
        Unsubscribe();
        // Always unlock cursor when disabling
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        // If time is frozen, unlock cursor and STOP camera logic
        if (Time.timeScale == 0f)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        // Only when unpaused
        if (Input.GetKeyDown(toggleCursorKey))
        {
            isCursorLocked = !isCursorLocked;
            UpdateCursorState();
        }

        // If cursor is unlocked (for Gizmos), don't rotate camera
        if (!isCursorLocked) return;

        if (!subscribed) TrySubscribe();

        HandleMouseLook();
        AlignUpVector();
        ApplyOrientation();
        HandleMovement();
    }

    void UpdateCursorState()
    {
        if (isCursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void TrySubscribe()
    {
        var bus = GravityBus.Instance;
        if (bus == null) return;
        gravityDir = bus.Direction;
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
    }

    void HandleMouseLook()
    {
        float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        if (Mathf.Approximately(mx, 0f) && Mathf.Approximately(my, 0f)) return;

        Vector3 currentUp = orientation * Vector3.up;
        Vector3 currentRight = orientation * Vector3.right;

        Quaternion yaw = Quaternion.AngleAxis(mx, currentUp);
        Quaternion pitch = Quaternion.AngleAxis(-my, currentRight);

        orientation = yaw * pitch * orientation;
    }

    void AlignUpVector()
    {
        Vector3 targetUp = followGravityUp ? -gravityDir : Vector3.up;
        Vector3 currentForward = orientation * Vector3.forward;

        if (currentForward.sqrMagnitude < 1e-6f) return;

        Quaternion targetRot = Quaternion.LookRotation(currentForward, targetUp);
        float t = 1f - Mathf.Exp(-upAlignSpeed * Time.deltaTime);
        orientation = Quaternion.Slerp(orientation, targetRot, t);
    }

    void ApplyOrientation()
    {
        transform.rotation = orientation;
    }

    void HandleMovement()
    {
        float ix = Input.GetAxisRaw("Horizontal");
        float iz = Input.GetAxisRaw("Vertical");

        float upInput = 0f;
        if (Input.GetKey(KeyCode.E)) upInput += 1f;
        if (Input.GetKey(KeyCode.Q)) upInput -= 1f;

        Vector3 fwd = orientation * Vector3.forward;
        Vector3 right = orientation * Vector3.right;
        Vector3 up = orientation * Vector3.up;

        Vector3 moveDir = fwd * iz + right * ix + up * upInput;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= fastMultiplier;
        if (Input.GetKey(KeyCode.LeftAlt)) speed *= slowMultiplier;

        Vector3 delta = moveDir * speed * Time.deltaTime;
        if (delta.sqrMagnitude < 1e-10f) return;

        Vector3 origin = transform.position;
        float radius = body ? body.radius * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)) : 0.5f;

        if (Physics.SphereCast(origin, radius, delta.normalized, out RaycastHit hit, delta.magnitude + collisionSkin, collisionMask, QueryTriggerInteraction.Ignore))
        {
            float allowed = hit.distance - collisionSkin;
            delta = delta.normalized * Mathf.Max(0, allowed);
        }

        transform.position = origin + delta;
    }
}