using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class MoverKinematic : MonoBehaviour
{
    [Header("Positions")]
    [Tooltip("Interpret Start/End as localPosition (true) or world position (false).")]
    public bool useLocal = true;
    public Vector3 startPos;
    public Vector3 endPos;

    [Header("Motion")]
    [Tooltip("Units per second along the path.")]
    public float speed = 2f;
    [Tooltip("Snap when within this distance to target.")]
    public float snapThreshold = 0.001f;

    [Header("State (read-only)")]
    [SerializeField] private bool active; // true = moving toward End

    public Vector3 SurfaceVelocityWorld { get; private set; }

    private Rigidbody rb;
    private Vector3 lastWorldPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        lastWorldPos = transform.position;
    }

    void Reset()
    {
        useLocal = true;
        startPos = transform.localPosition;
        endPos = transform.localPosition + Vector3.up * 2f;
        speed = 2f;
        snapThreshold = 0.001f;

        var c = GetComponent<Collider>();
        c.isTrigger = false; // must be solid
    }

    void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        snapThreshold = Mathf.Max(1e-6f, snapThreshold);
    }
    public void SetActive(bool on) => active = on;

    void Update()
    {
        if (Application.isPlaying) return;
        Step(1f / 60f, false);
    }

    void FixedUpdate()
    {
        Step(Time.fixedDeltaTime, true);
    }

    void Step(float dt, bool physicsAPI)
    {
        Vector3 cur = transform.position;
        Vector3 target = GetTargetWorld();

        Vector3 next = Vector3.MoveTowards(cur, target, speed * dt);
        if ((target - next).sqrMagnitude <= snapThreshold * snapThreshold)
            next = target;

        SurfaceVelocityWorld = (next - cur) / Mathf.Max(dt, 1e-6f);

        if (physicsAPI && Application.isPlaying)
            rb.MovePosition(next);
        else
            transform.position = next;

        lastWorldPos = next;
    }

    Vector3 GetTargetWorld()
    {
        if (useLocal)
        {
            var parent = transform.parent;
            Vector3 startW = parent ? parent.TransformPoint(startPos) : startPos;
            Vector3 endW = parent ? parent.TransformPoint(endPos) : endPos;
            return active ? endW : startW;
        }
        else
        {
            return active ? endPos : startPos;
        }
    }
}
