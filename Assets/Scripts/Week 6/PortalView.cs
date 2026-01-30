using UnityEngine;

/// <summary>
/// Per-portal viewpoint camera -> RenderTexture, shown on the linked portal's screen.
/// Applies oblique near-clip in Camera.onPreCull so first frame is valid.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Portal))]
public class PortalView : MonoBehaviour
{
    [Header("References")]
    public string portalLayerName = "Portal";  // excluded from render
    public Material screenMaterial;            // Unlit/Texture
    public Material borderMaterial;            // Unlit/Color

    [Header("Render Texture")]
    public bool matchScreenResolution = true;
    public int textureWidth = 1024;
    public int textureHeight = 1024;
    public int depthBufferBits = 24;
    public RenderTextureFormat colorFormat = RenderTextureFormat.ARGB32;

    [Header("Culling & Clip")]
    public LayerMask cullingMask = ~0;
    public bool useObliqueClip = true;
    [Tooltip("Small push along the clip plane normal to avoid self-clipping acne.")]
    public float clipEpsilon = 0.01f;
    [Tooltip("If true, clip against THIS portal's plane. Set false to clip against the linked portal's plane.")]
    public bool clipAgainstThisPortal = true;

    private Portal portal;
    private Camera viewCam;    // the portal's own viewpoint camera
    private RenderTexture rt;
    private int portalLayerMask;

    void Awake()
    {
        portal = GetComponent<Portal>();
        if (!portal || !portal.viewpointCamera)
        {
            Debug.LogError("[PortalView] Missing Portal or viewpointCamera.", this);
            enabled = false;
            return;
        }

        viewCam = portal.viewpointCamera;

        int layer = LayerMask.NameToLayer(portalLayerName);
        portalLayerMask = (layer >= 0) ? (1 << layer) : 0;

        // Configure camera for world visibility
        viewCam.enabled = true;
        viewCam.clearFlags = CameraClearFlags.Skybox;
        viewCam.backgroundColor = Color.black;
        viewCam.nearClipPlane = 2.35f;
        viewCam.farClipPlane = 1000f;
        viewCam.fieldOfView = 45f;
        viewCam.usePhysicalProperties = true;
        viewCam.sensorSize = new Vector2(24, 36);

        viewCam.cullingMask = cullingMask;
        if (portalLayerMask != 0)
            viewCam.cullingMask &= ~portalLayerMask;

        AllocateRT();
        viewCam.targetTexture = rt;

        // Set aspect/FOV explicitly to avoid weird initial frustum
        viewCam.aspect = (float)rt.width / rt.height;

        // Assign materials/textures
        if (portal.border && borderMaterial)
        {
            var bmr = portal.border.GetComponent<MeshRenderer>();
            if (bmr) { borderMaterial.color = portal.portalTint; bmr.sharedMaterial = borderMaterial; }
        }
        if (portal.screen)
        {
            var smr = portal.screen.GetComponent<MeshRenderer>();
            if (smr)
            {
                var mat = screenMaterial != null ? screenMaterial : new Material(Shader.Find("Unlit/Texture"));
                mat.mainTexture = rt;
                smr.sharedMaterial = mat;
            }
        }
    }

    void OnEnable()
    {
        // Apply the oblique plane at the correct time each render
        Camera.onPreCull += HandlePreCull;
    }

    void OnDisable()
    {
        Camera.onPreCull -= HandlePreCull;
    }

    void OnDestroy()
    {
        if (viewCam) viewCam.targetTexture = null;
        if (rt != null) { rt.Release(); DestroyImmediate(rt); rt = null; }
    }

    void Update()
    {
#if UNITY_EDITOR
        if (matchScreenResolution && rt && (rt.width != Screen.width || rt.height != Screen.height))
        {
            AllocateRT();
            if (viewCam)
            {
                viewCam.targetTexture = rt;
                viewCam.aspect = (float)rt.width / rt.height;
            }
            // ensure the linked screen keeps our texture if editor reallocated
            PushTextureToLinked();
        }
#endif
        // Make sure linked portal shows our texture even if linked late
        PushTextureToLinked();
    }

    void HandlePreCull(Camera cam)
    {
        if (!viewCam || cam != viewCam) return;
        if (!useObliqueClip) { viewCam.ResetProjectionMatrix(); return; }

        // Choose which portal plane to clip against
        Transform planeTf = clipAgainstThisPortal ? transform
                             : (portal.linked ? portal.linked.transform : transform);

        // Reset before overriding to avoid accumulating drift
        viewCam.ResetProjectionMatrix();

        // Build plane facing the camera
        Vector3 p = planeTf.position;
        Vector3 n = planeTf.forward;
        Vector3 camPos = viewCam.transform.position;
        if (Vector3.Dot(n, camPos - p) < 0f) n = -n; // face toward the camera
        p += n * clipEpsilon; // nudge

        Vector4 planeWorld = new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, p));

        // Convert to camera space (M^-T * planeWorld)
        Matrix4x4 V = viewCam.worldToCameraMatrix;
        Vector4 planeCam = Matrix4x4.Transpose(V.inverse) * planeWorld;

        // Apply oblique projection
        viewCam.projectionMatrix = CalculateObliqueMatrix(viewCam, planeCam);
    }

    void AllocateRT()
    {
        if (rt != null) { rt.Release(); DestroyImmediate(rt); rt = null; }

        int w = matchScreenResolution ? Mathf.Max(64, Screen.width) : textureWidth;
        int h = matchScreenResolution ? Mathf.Max(64, Screen.height) : textureHeight;

        rt = new RenderTexture(w, h, depthBufferBits, colorFormat)
        {
            name = $"PortalRT_{gameObject.name}",
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing)
        };
        rt.Create();
    }

    void PushTextureToLinked()
    {
        if (!portal || !portal.linked || !rt) return;
        var mr = portal.linked.screen ? portal.linked.screen.GetComponent<MeshRenderer>() : null;
        if (!mr) return;

        var mat = mr.sharedMaterial;
        if (mat == null || mat.shader == null || !mat.shader.name.Contains("Unlit"))
        {
            mat = new Material(Shader.Find("Unlit/Texture"));
            mr.sharedMaterial = mat;
        }
        mat.mainTexture = rt;
    }

    // Standard oblique-matrix builder
    static Matrix4x4 CalculateObliqueMatrix(Camera cam, Vector4 clipPlaneCameraSpace)
    {
        var proj = cam.projectionMatrix;
        Vector4 q = new Vector4(
            Mathf.Sign(clipPlaneCameraSpace.x),
            Mathf.Sign(clipPlaneCameraSpace.y),
            1.0f,
            1.0f
        );
        Vector4 c = proj.inverse * q;
        Vector4 s = clipPlaneCameraSpace * (2.0f / Vector4.Dot(clipPlaneCameraSpace, c));
        proj[2] = s.x - proj[3];
        proj[6] = s.y - proj[7];
        proj[10] = s.z - proj[11];
        proj[14] = s.w - proj[15];
        return proj;
    }
}
