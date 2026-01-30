using UnityEngine;
using UnityEngine.EventSystems;

public class ElasticPlacementSystem : MonoBehaviour
{
    [Header("Assets")]
    [Tooltip("The Elastic prefab to spawn.")]
    public GameObject elasticPrefab;

    [Header("Settings")]
    [Tooltip("Layers we can place elastics on (e.g. Default, Ground).")]
    public LayerMask placementLayers = ~0;
    [Tooltip("Distance to check for a surface.")]
    public float maxReachDistance = 50f;

    [Header("Dependencies")]
    [Tooltip("Reference to the HUD/State manager.")]
    public HUDController hudController;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;

        // Safety check
        if (hudController == null)
            hudController = FindFirstObjectByType<HUDController>();
    }

    private void Update()
    {
        // Only run if in the correct mode
        if (hudController.CurrentMode != BuildMode.Elastic)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Listen for click
        if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            PlaceElastic();
        }
    }

    private void PlaceElastic()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxReachDistance, placementLayers))
        {
            // Up matches the surface normal
            Quaternion orientation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(90f, 0f, 0f);

            GameObject newElastic = Instantiate(elasticPrefab, hit.point, orientation);

            // Tag it for the Editor script
            newElastic.tag = "Editable";
        }
    }
}