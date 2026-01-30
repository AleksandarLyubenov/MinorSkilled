using UnityEngine;
using UnityEngine.EventSystems;

public class PortalPlacer : MonoBehaviour
{
    [Header("Dependencies")]
    public HUDController hud;
    public Camera playerCamera;

    [Header("Input")]
    public KeyCode placeA = KeyCode.Mouse0;
    public KeyCode placeB = KeyCode.Mouse1;

    [Header("Placement")]
    public float maxPlaceDistance = 50f;
    public LayerMask placeableLayers = ~0;
    public string portalLayerName = "Portal";     // raycast ignores this

    [Header("Scale (fixed)")]
    public float portalWidth = 2f;
    public float portalHeight = 2.5f;
    public float triggerThickness = 0.12f; // Z (slim)

    [Header("Placement Offsets")]
    [Tooltip("Extra offset outward from the surface to avoid clipping/z-fighting.")]
    public float surfacePullOut = 0.03f;  // tweak 0.01–0.05

    [Header("Portal visuals")]
    public Color portalAColor = new Color(0.2f, 0.6f, 1f, 1f);
    public Color portalBColor = new Color(1f, 0.5f, 0.2f, 1f);
    [Tooltip("How much bigger the border is than the screen (meters).")]
    public float borderMargin = 0.06f;

    [Tooltip("Local Z offsets to avoid z-fighting (meters).")]
    public float screenZ = 0.005f;  
    public float borderZ = 0.0f;

    private Portal portalA;
    private Portal portalB;
    private int portalLayerMask;

    void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
        int layer = LayerMask.NameToLayer(portalLayerName);
        portalLayerMask = (layer >= 0) ? (1 << layer) : 0;
    }

    void Update()
    {
        // Only run if in Portal Mode
        if (hud == null || hud.CurrentMode != BuildMode.Portal) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetKeyDown(placeA))
        {
            TryPlace(ref portalA, portalB, portalAColor);
            hud.SetPortalIsA(false);
        }

        if (Input.GetKeyDown(placeB))
        {
            TryPlace(ref portalB, portalA, portalBColor);
            hud.SetPortalIsA(true);
        }
    }

    void TryPlace(ref Portal targetPortal, Portal otherPortal, Color tint)
    {
        if (!playerCamera) return;

        var mask = placeableLayers & ~portalLayerMask;
        if (Physics.Raycast(playerCamera.transform.position,
                            playerCamera.transform.forward,
                            out var hit, maxPlaceDistance, mask, QueryTriggerInteraction.Ignore))
        {
            if (targetPortal == null)
                targetPortal = CreatePortal("Portal", tint);

            // Face outward (toward viewer): forward = -surface normal
            Vector3 forward = -hit.normal;
            Vector3 up = Mathf.Abs(Vector3.Dot(Vector3.up, forward)) > 0.95f ? Vector3.right : Vector3.up;
            up = Vector3.ProjectOnPlane(up, forward).normalized;
            Quaternion rot = Quaternion.LookRotation(forward, up);

            // Pull out of wall
            Vector3 pos = hit.point + forward * (triggerThickness * 0.5f + surfacePullOut);

            targetPortal.transform.SetPositionAndRotation(pos, rot);
            targetPortal.SetSize(portalWidth, portalHeight, triggerThickness, borderMargin, screenZ, borderZ);

            float minClearance = triggerThickness * 0.5f + surfacePullOut + 0.05f; // tiny safety margin
            targetPortal.SnapViewpointInFront(minClearance);

            // Link traversal + views
            targetPortal.SetLinked(otherPortal);
            if (otherPortal != null) otherPortal.SetLinked(targetPortal);
        }
    }

    Portal CreatePortal(string baseName, Color tint)
    {
        var go = new GameObject(baseName);
        int layer = LayerMask.NameToLayer(portalLayerName);
        if (layer >= 0) go.layer = layer;

        // Core portal (trigger + traversal)
        var portal = go.AddComponent<Portal>();
        portal.portalTint = tint;

        // Trigger volume
        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        portal.box = bc;

        // Border (colored outline quad)
        var border = GameObject.CreatePrimitive(PrimitiveType.Quad);
        border.name = "Border";
        border.transform.SetParent(go.transform, false);
        border.GetComponent<Collider>().enabled = false;
        var borderMR = border.GetComponent<MeshRenderer>();
        borderMR.sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = tint };
        portal.border = border.transform;

        // Screen (render target)
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screen.name = "Screen";
        screen.transform.SetParent(go.transform, false);
        screen.GetComponent<Collider>().enabled = false;
        var screenMR = screen.GetComponent<MeshRenderer>();
        screenMR.sharedMaterial = new Material(Shader.Find("Unlit/Texture"));
        portal.screen = screen.transform;

        // Per-portal viewpoint camera
        var viewGO = new GameObject("ViewpointCamera");
        viewGO.transform.SetParent(go.transform, false);
        viewGO.transform.localPosition = new Vector3(0f, 0f, 2.7f);
        viewGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var vpCam = viewGO.AddComponent<Camera>();
        vpCam.enabled = true;
        portal.viewpointCamera = vpCam;

        // View component
        var view = go.AddComponent<PortalView>();
        view.portalLayerName = portalLayerName;
        view.borderMaterial = borderMR.sharedMaterial;
        view.screenMaterial = screenMR.sharedMaterial;

        // Initial sizing
        portal.SetSize(portalWidth, portalHeight, triggerThickness, borderMargin, screenZ, borderZ);

        return portal;
    }
}