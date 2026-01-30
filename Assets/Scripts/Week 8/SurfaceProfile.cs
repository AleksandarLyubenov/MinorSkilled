using UnityEngine;

[CreateAssetMenu(menuName = "Surfaces/Surface Profile", fileName = "SurfaceProfile")]
public class SurfaceProfile : ScriptableObject
{
    [Header("Display")]
    public string displayName = "New Surface";
    public Color tint = Color.white;

    [Header("Character Controller (code-side)")]
    public float moveSpeedMul = 1f;
    public float jumpMul = 1f;
    [Range(0f, 1f)] public float bounciness = 0f;
    [Range(0f, 1f)] public float sticky = 0f;  // extra damping added to friction

    [Header("Traction shaping")]
    public float accelMul = 1f;   // how quickly you reach target speed
    public float decelMul = 1f;   // used when reversing direction (helps snappier control)

    [Header("Friction (critical for 'slippery' feel)")]
    [Tooltip("Ground friction in 1/seconds. 0 = no slow-down, 8 = normal floor, 20+ = very sticky.")]
    public float groundFriction = 8f;
    [Tooltip("Air friction in 1/seconds (very small).")]
    public float airFriction = 0.5f;
    [Tooltip("How much input influences velocity while airborne (0..1).")]
    [Range(0f, 1f)] public float airControl = 0.2f;

    [Header("Rigidbodies (extra on top of PhysicsMaterial)")]
    public float rbLinearDampingMul = 1f;
    public float rbAngularDampingMul = 1f;
    public float rbRestitutionMul = 1f;

    [Header("Wall Interaction")]
    [Tooltip("If true, this surface can be used for wall-running / wall-clinging.")]
    public bool allowWallRun = false;

    [Tooltip("Maximum fall speed (m/s along gravity) while attached to this wall.")]
    public float wallRunMaxFallSpeed = 2f;

    [Tooltip("Move speed multiplier while wall-running on this surface.")]
    public float wallRunMoveSpeedMul = 0.9f;


    [Header("Physics Material (for colliders on this surface)")]
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
    public PhysicsMaterial physicsMaterial;
#else
    public PhysicMaterial physicsMaterial;
#endif

    [Header("Visuals (optional)")]
    public Material surfaceMaterial;
}
