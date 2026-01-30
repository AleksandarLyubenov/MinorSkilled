using UnityEngine;
using UnityEngine.EventSystems;

public class FanPlacer : MonoBehaviour
{
    [Header("Placement")]
    public Camera playerCamera;
    public GameObject fanPrefab;

    [Tooltip("Max raycast distance for placement.")]
    public float maxPlaceDistance = 100f;

    [Tooltip("Surfaces that are allowed when 'restrictToLayer' is enabled.")]
    public LayerMask allowedSurfaceLayers = ~0;

    [Tooltip("If true, player can place fans on any collider. If false, only on allowedSurfaceLayers.")]
    public bool placeAnywhere = true;

    [Header("Input")]
    [Tooltip("Mouse button used to place a fan.")]
    public int placeMouseButton = 0; // 0 = LMB

    [Tooltip("Key to toggle placeAnywhere mode.")]
    public KeyCode togglePlacementKey = KeyCode.F;

    [Header("HUD / Mode Gating")]
    [Tooltip("Optional HUD reference. If present, fans can only be placed when HUD is in Fan mode.")]
    public HUDController hud;
    public bool requireFanMode = true;

    private void Awake()
    {
        // Auto-find HUD if not set, but stay silent if there is none.
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

    private bool IsFanModeActive()
    {
        if (!requireFanMode) return true;
        if (hud == null) return true;
        return hud.CurrentMode == BuildMode.Fan;
    }

    private void HandleToggle()
    {
        if (Input.GetKeyDown(togglePlacementKey))
        {
            placeAnywhere = !placeAnywhere;
            Debug.Log($"FanPlacer: placeAnywhere = {placeAnywhere}");
        }
    }

    private void HandlePlacement()
    {
        // Do nothing if not in Fan placement mode (when HUD exists).
        if (!IsFanModeActive())
            return;

        if (!Input.GetMouseButtonDown(placeMouseButton))
            return;

        if (playerCamera == null || fanPrefab == null)
        {
            Debug.LogWarning("FanPlacer: Missing camera or fanPrefab reference.");
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask mask = placeAnywhere ? Physics.AllLayers : allowedSurfaceLayers;

        if (Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance, mask, QueryTriggerInteraction.Ignore))
        {
            PlaceFanAt(hit);
        }
    }

    private void PlaceFanAt(RaycastHit hit)
    {
        // Orient fan so its forward points along the surface normal
        Quaternion rotation = Quaternion.LookRotation(hit.normal);

        // Slightly offset fan from the surface to avoid z-fighting / clipping
        Vector3 position = hit.point + hit.normal * 0.01f;

        GameObject fanInstance = Instantiate(fanPrefab, position, rotation);
        fanInstance.name = $"Fan_{Time.frameCount}";

        Debug.DrawRay(position, fanInstance.transform.forward * 2f, Color.cyan, 2f);
    }
}
