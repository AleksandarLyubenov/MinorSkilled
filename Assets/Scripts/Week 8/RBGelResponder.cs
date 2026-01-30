using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class RBGelResponder : MonoBehaviour
{
    public bool scaleDampingFromSurface = true;
    public bool extraRestitutionFromSurface = true;

    Rigidbody rb;

    // caches
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
    float baseLinDamp, baseAngDamp;
#else
    float baseLinDamp, baseAngDamp; // will map to drag/angularDrag
#endif

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
        baseLinDamp = rb.linearDamping;
        baseAngDamp = rb.angularDamping;
#else
        baseLinDamp = rb.drag;
        baseAngDamp = rb.angularDrag;
#endif
    }

    void OnCollisionStay(Collision col)
    {
        var tag = col.collider.GetComponentInParent<SurfaceTag>();
        var profile = tag ? tag.profile : null;
        if (profile == null) return;

        if (scaleDampingFromSurface)
        {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
            rb.linearDamping = baseLinDamp * Mathf.Max(0.01f, profile.rbLinearDampingMul);
            rb.angularDamping = baseAngDamp * Mathf.Max(0.01f, profile.rbAngularDampingMul);
#else
            rb.drag = baseLinDamp * Mathf.Max(0.01f, profile.rbLinearDampingMul);
            rb.angularDrag = baseAngDamp * Mathf.Max(0.01f, profile.rbAngularDampingMul);
#endif
        }

        if (extraRestitutionFromSurface && profile.rbRestitutionMul != 1f)
        {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
            rb.linearVelocity *= profile.rbRestitutionMul;
#else
            rb.velocity *= profile.rbRestitutionMul;
#endif
        }
    }

    void OnCollisionExit(Collision col)
    {
        // restore damping when leaving any surface
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
        rb.linearDamping = baseLinDamp;
        rb.angularDamping = baseAngDamp;
#else
        rb.drag = baseLinDamp;
        rb.angularDrag = baseAngDamp;
#endif
    }
}
