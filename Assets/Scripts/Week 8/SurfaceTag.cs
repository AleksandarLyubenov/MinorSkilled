using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SurfaceTag : MonoBehaviour
{
    public SurfaceProfile profile;
    public bool applyPhysicsMaterialOnEnable = true;
    public bool swapVisualMaterial = false;

    void OnEnable()
    {
        if (profile == null) return;

#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
        if (applyPhysicsMaterialOnEnable && profile.physicsMaterial && TryGetComponent(out Collider col))
            col.sharedMaterial = profile.physicsMaterial;
#else
        if (applyPhysicsMaterialOnEnable && profile.physicsMaterial && TryGetComponent(out Collider col))
            col.sharedMaterial = profile.physicsMaterial;
#endif

        if (swapVisualMaterial && profile.surfaceMaterial && TryGetComponent(out Renderer r))
            r.sharedMaterial = profile.surfaceMaterial;
    }
}
