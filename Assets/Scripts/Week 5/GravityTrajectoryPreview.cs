using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
[DisallowMultipleComponent]
public class GravityTrajectoryPreview : MonoBehaviour
{
    [Header("Rendering")]
    [Tooltip("LineRenderer used to draw the trajectory. If null, one is auto-created.")]
    public LineRenderer line;
    [Tooltip("Seconds to predict ahead.")]
    public float predictionTime = 3f;
    [Tooltip("Simulation substep (lower = more accurate, more cost).")]
    public float substep = 0.02f;
    [Tooltip("Maximum sampled points for the line.")]
    public int maxPoints = 256;

    [Header("Collision")]
    [Tooltip("Sphere radius used for sweep tests. -1 = auto from collider bounds.")]
    public float sphereRadius = -1f;
    [Tooltip("Layers considered solid for the prediction.")]
    public LayerMask collisionMask = ~0;

    [Header("Forces")]
    [Tooltip("Apply linear damping / drag during preview.")]
    public bool includeDrag = true;

    private Rigidbody rb;
    private Collider selfCol;
    private Vector3[] points;

    Vector3 GetLinearVelocity()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }
    float GetLinearDamping()
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
        return rb.linearDamping;
#else
        return rb.drag;
#endif
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        selfCol = GetComponent<Collider>();

        if (line == null)
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 0;
            line.widthMultiplier = 0.04f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;

            // Make sure it’s visible in Game View (Play Mode)
            Material mat = null;
            // Try URP Unlit first
            var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit != null) mat = new Material(urpUnlit);
            // Fallback to legacy Unlit/Color
            if (mat == null)
            {
                var legacy = Shader.Find("Unlit/Color");
                if (legacy != null) mat = new Material(legacy);
            }
            if (mat != null) line.material = mat;
        }

        points = new Vector3[Mathf.Max(8, maxPoints)];
        if (sphereRadius <= 0f) sphereRadius = ComputeConservativeRadius(selfCol);
    }

    void LateUpdate()
    {
        // Read gravity from your global bus (falls back if missing).
        var bus = GravityBus.Instance;
        Vector3 gDir = bus ? bus.Direction : Vector3.down;
        float gMag = bus ? bus.Magnitude : 9.81f;
        Vector3 gAcc = gDir * gMag;

        SimulateAndDraw(transform.position, GetLinearVelocity(), gAcc);
    }

    void SimulateAndDraw(Vector3 startPos, Vector3 startVel, Vector3 acc)
    {
        float tRemain = Mathf.Max(0f, predictionTime);
        float dt = Mathf.Max(0.002f, substep);
        int pCount = 0;

        Vector3 pos = startPos;
        Vector3 vel = startVel;
        points[pCount++] = pos;

        // Ignore our own collider during sweeps to prevent self-hits.
        bool colEnabled = selfCol.enabled;
        selfCol.enabled = false;

        while (tRemain > 0f && pCount < points.Length)
        {
            float step = Mathf.Min(dt, tRemain);

            // Semi-implicit Euler + optional linear damping
            float ld = includeDrag ? Mathf.Max(0f, GetLinearDamping()) : 0f;
            vel += acc * step;
            if (ld > 0f) vel *= 1f / (1f + ld * step);

            Vector3 delta = vel * step;
            float dist = delta.magnitude;

            if (dist > 1e-6f &&
                Physics.SphereCast(pos, sphereRadius, delta / dist, out var hit, dist + 1e-4f,
                                   collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Move to contact (minus tiny skin) and END the preview
                float travel = Mathf.Max(0f, hit.distance - 1e-3f);
                pos += (delta / dist) * travel;

                // Add the impact point (slightly nudged along normal so it’s visible)
                points[pCount++] = pos;
                if (pCount < points.Length)
                    points[pCount++] = pos + hit.normal * 0.02f;

                break; // stop on first collision
            }
            else
            {
                pos += delta;
                points[pCount++] = pos;
                tRemain -= step;
            }
        }

        // Restore our collider
        selfCol.enabled = colEnabled;

        // Output to line
        line.positionCount = pCount;
        for (int i = 0; i < pCount; i++)
            line.SetPosition(i, points[i]);
    }

    static float ComputeConservativeRadius(Collider c)
    {
        if (c is SphereCollider sc) return sc.radius * AbsMax(c.transform.lossyScale);
        if (c is CapsuleCollider cc) return cc.radius * AbsMax(c.transform.lossyScale);
        if (c is BoxCollider bc)
        {
            Vector3 s = Vector3.Scale(bc.size, c.transform.lossyScale);
            return Mathf.Min(Mathf.Min(s.x, s.y), s.z) * 0.5f * 0.9f; // inset a bit
        }
        // Fallback from bounds
        return Mathf.Min(Mathf.Min(c.bounds.extents.x, c.bounds.extents.y), c.bounds.extents.z) * 0.9f;
    }

    static float AbsMax(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
