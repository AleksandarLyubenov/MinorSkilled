using UnityEngine;
using UnityEngine.EventSystems;

public class SurfacePlacer : MonoBehaviour
{
    [Header("Placement")]
    public Camera playerCamera;

    [Tooltip("Prefab used when in Bouncy surface mode.")]
    public GameObject bouncySurfacePrefab;

    [Tooltip("Prefab used when in Slippery surface mode.")]
    public GameObject slipperySurfacePrefab;

    [Tooltip("Prefab used when in Sticky surface mode.")]
    public GameObject stickySurfacePrefab;

    [Tooltip("Max raycast distance for placement.")]
    public float maxPlaceDistance = 100f;

    [Tooltip("Surfaces that are allowed when 'restrictToLayer' is enabled.")]
    public LayerMask allowedSurfaceLayers = ~0;

    [Tooltip("If true, player can place surfaces on any collider. If false, only on allowedSurfaceLayers.")]
    public bool placeAnywhere = true;

    [Header("Orientation")]
    [Tooltip("If true, align the prefab's 'up' axis with the surface normal. If false, align its 'forward' axis with the normal.")]
    public bool useUpAsNormal = true;

    [Tooltip("Slight offset along the surface normal to avoid z-fighting.")]
    public float surfaceOffset = 0.01f;

    [Header("Input")]
    [Tooltip("Mouse button used to place a surface.")]
    public int placeMouseButton = 0; // 0 = LMB

    [Tooltip("Key to toggle placeAnywhere mode.")]
    public KeyCode togglePlacementKey = KeyCode.F;

    [Header("HUD / Mode Gating")]
    [Tooltip("Optional HUD reference. If present, placement only works in one of the surface modes.")]
    public HUDController hud;

    [Tooltip("If true, require HUD + correct surface mode. If false, always allow placement (even without HUD).")]
    public bool requireSurfaceMode = true;

    private void Awake()
    {
        // Auto-find HUD, but be fine if none exists.
        if (hud == null)
        {
            hud = FindFirstObjectByType<HUDController>();
        }
    }

    private void Update()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        HandleToggle();
        HandlePlacement();
    }

    private void HandleToggle()
    {
        // optional toggle between anywhere / restricted.
        if (Input.GetKeyDown(togglePlacementKey))
        {
            placeAnywhere = !placeAnywhere;
            Debug.Log($"SurfacePlacer: placeAnywhere = {placeAnywhere}");
        }
    }

    private void HandlePlacement()
    {
        // Only allow when in a surface mode
        if (!IsSurfaceModeActive())
            return;

        if (!Input.GetMouseButtonDown(placeMouseButton))
            return;

        if (playerCamera == null)
        {
            Debug.LogWarning("SurfacePlacer: Missing playerCamera reference.");
            return;
        }

        GameObject prefabToPlace = GetCurrentSurfacePrefab();
        if (prefabToPlace == null)
        {
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask mask = placeAnywhere ? Physics.AllLayers : allowedSurfaceLayers;

        if (Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance, mask, QueryTriggerInteraction.Ignore))
        {
            PlaceSurfaceAt(prefabToPlace, hit);
        }
    }

    private bool IsSurfaceModeActive()
    {
        if (!requireSurfaceMode)
            return true;

        if (hud == null)
            return true;

        switch (hud.CurrentMode)
        {
            case BuildMode.SurfaceBouncy:
            case BuildMode.SurfaceSlippery:
            case BuildMode.SurfaceSticky:
                return true;

            default:
                return false;
        }
    }

    private GameObject GetCurrentSurfacePrefab()
    {
        if (hud == null)
        {
            return null;
        }

        switch (hud.CurrentMode)
        {
            case BuildMode.SurfaceBouncy:
                return bouncySurfacePrefab;

            case BuildMode.SurfaceSlippery:
                return slipperySurfacePrefab;

            case BuildMode.SurfaceSticky:
                return stickySurfacePrefab;

            default:
                return null;
        }
    }

    private void PlaceSurfaceAt(GameObject prefab, RaycastHit hit)
    {
        // Determine rotation based on surface normal
        Quaternion rotation;
        if (useUpAsNormal)
        {
            rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        }
        else
        {
            rotation = Quaternion.LookRotation(hit.normal);
        }

        Vector3 position = hit.point + hit.normal * surfaceOffset;

        GameObject instance = Instantiate(prefab, position, rotation);
        instance.name = $"{prefab.name}_{Time.frameCount}";

        Debug.DrawRay(position, hit.normal * 0.5f, Color.yellow, 2f);
    }
}
