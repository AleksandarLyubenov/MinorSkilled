using UnityEngine;

[ExecuteAlways]
public class Mover : MonoBehaviour
{
    [Header("Positions")]
    [Tooltip("Interpret Start/End as localPosition (true) or world position (false).")]
    public bool useLocal = true;

    public Vector3 startPos;   // edited via scene handles
    public Vector3 endPos;     // edited via scene handles

    [Header("Motion")]
    [Tooltip("Units per second along the path.")]
    public float speed = 2f;
    [Tooltip("If true, snap exactly to target when within this distance.")]
    public float snapThreshold = 0.001f;

    [Header("State (read-only)")]
    [SerializeField, Tooltip("True => moving toward End; False => toward Start.")]
    private bool active = false;

    // cache
    Vector3 Current
    {
        get => useLocal ? transform.localPosition : transform.position;
        set { if (useLocal) transform.localPosition = value; else transform.position = value; }
    }

    Vector3 Target => active ? (useLocal ? transform.parent.TransformPointLocalAware(endPos, transform) : endPos)
                             : (useLocal ? transform.parent.TransformPointLocalAware(startPos, transform) : startPos);

    public void SetActive(bool on) => active = on;

    void Reset()
    {
        useLocal = true;
        startPos = transform.localPosition;
        endPos = transform.localPosition + Vector3.up * 2f;
        speed = 2f;
        snapThreshold = 0.001f;
    }

    void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        snapThreshold = Mathf.Max(1e-6f, snapThreshold);
        // keep start at current
        if (!Application.isPlaying && startPos == Vector3.zero && endPos == Vector3.zero)
            startPos = useLocal ? transform.localPosition : transform.position;
    }

    void Update()
    {
        // run both edit & play so you can preview motion in editor
        float dt = Application.isPlaying ? Time.deltaTime : (1f / 60f);

        var target = active ? (useLocal ? startPos.ToWorld(transform) : endPos)
                            : (useLocal ? startPos.ToWorld(transform) : startPos);

        // Correct Target calc (explicitly)
        target = active
            ? (useLocal ? endPos.ToWorld(transform) : endPos)
            : (useLocal ? startPos.ToWorld(transform) : startPos);

        Vector3 cur = Current;
        Vector3 next = Vector3.MoveTowards(cur, target, speed * dt);

        if ((target - next).sqrMagnitude <= snapThreshold * snapThreshold)
            next = target;

        Current = next;
    }
}

static class MoverExtensions
{
    // Convert a local-position value (relative to this transform) to world
    public static Vector3 ToWorld(this Vector3 localPos, Transform t)
    {
        // If the mover uses local coordinates, treat vectors as positions relative to the mover's parent
        var parent = t.parent;
        return parent ? parent.TransformPoint(localPos) : localPos;
    }

    // For clarity/symmetry
    public static Vector3 ToLocal(this Vector3 worldPos, Transform t)
    {
        var parent = t.parent;
        return parent ? parent.InverseTransformPoint(worldPos) : worldPos;
    }

    // Compatibility alias used above for intent; identical to ToWorld
    public static Vector3 TransformPointLocalAware(this Transform parent, Vector3 localPos, Transform self)
        => localPos.ToWorld(self);
}
