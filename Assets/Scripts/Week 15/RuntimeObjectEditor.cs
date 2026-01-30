using UnityEngine;

public class RuntimeObjectEditor : MonoBehaviour
{
    [Header("Dependencies")]
    public HUDController hud;
    public Camera playerCamera;

    [Header("Gizmo Settings")]
    public GameObject gizmoPrefab;
    [Tooltip("Only objects on these layers can be selected. Exclude your 'Portal' layer here.")]
    public LayerMask editableLayer;
    public LayerMask gizmoLayer;

    [Header("Input Settings")]
    public KeyCode deleteKey = KeyCode.C;

    [Header("Interaction Settings")]
    public float rotateSpeed = 0.5f;
    public float snapAngle = 15f;
    public float clickRotateAngle = 90f;
    public float dragThreshold = 5f;

    private GameObject currentSelection;
    private GameObject activeGizmo;
    private bool isDraggingHandle = false;

    private string currentHandleTag;
    private Vector3 dragAxis;
    private Plane dragPlane;
    private Vector3 initialObjectPos;
    private Quaternion initialRotation;
    private Vector3 initialHitPoint;
    private Vector2 initialMouseScreen;

    private Rigidbody selectedRb;
    private bool wasKinematic;

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
    }

    void Update()
    {
        // Only run in Edit Mode
        if (hud.CurrentMode != BuildMode.Edit)
        {
            if (currentSelection != null) ClearSelection();
            return;
        }

        // Handle Deletion (Hot Key C)
        if (Input.GetKeyDown(deleteKey))
        {
            HandleDelete();
        }

        // Mouse Inputs
        if (Input.GetMouseButtonDown(0)) HandleClick();
        if (isDraggingHandle && Input.GetMouseButton(0)) HandleDrag();
        if (Input.GetMouseButtonUp(0)) HandleRelease();
    }

    void HandleClick()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Check Gizmo Handles first (highest priority)
        if (activeGizmo != null && Physics.Raycast(ray, out hit, 100f, gizmoLayer))
        {
            if (hit.collider.CompareTag("HandleX") || hit.collider.CompareTag("HandleY") || hit.collider.CompareTag("HandleZ"))
            {
                StartDrag(hit.collider.tag);
                return;
            }
        }

        // Check Editable Objects
        if (Physics.Raycast(ray, out hit, 100f, editableLayer))
        {
            SelectObject(hit.collider.gameObject);
        }
        else
        {
            // Clicked empty space or a non-editable layer
            ClearSelection();
        }
    }

    void HandleDelete()
    {
        if (currentSelection == null) return;

        // Destroy the visual gizmo
        if (activeGizmo != null)
        {
            Destroy(activeGizmo);
            activeGizmo = null;
        }

        // Destroy the actual object
        Destroy(currentSelection);

        // Reset references
        currentSelection = null;
        isDraggingHandle = false;
        selectedRb = null;
    }

    void StartDrag(string handleTag)
    {
        isDraggingHandle = true;
        currentHandleTag = handleTag;

        // State
        initialObjectPos = currentSelection.transform.position;
        initialRotation = currentSelection.transform.rotation;
        initialMouseScreen = Input.mousePosition;

        // Determine Axis
        if (handleTag == "HandleX") dragAxis = currentSelection.transform.right;
        if (handleTag == "HandleY") dragAxis = currentSelection.transform.up;
        if (handleTag == "HandleZ") dragAxis = currentSelection.transform.forward;

        // Setup Plane
        dragPlane = new Plane(-playerCamera.transform.forward, initialObjectPos);
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float enter))
        {
            initialHitPoint = ray.GetPoint(enter);
        }

        // Freeze Physics while editing
        selectedRb = currentSelection.GetComponent<Rigidbody>();
        if (selectedRb != null)
        {
            wasKinematic = selectedRb.isKinematic;
            selectedRb.isKinematic = true;
            selectedRb.linearVelocity = Vector3.zero;
            selectedRb.angularVelocity = Vector3.zero;
        }
    }

    void HandleDrag()
    {
        if (currentSelection == null)
        {
            // if object was deleted externally while dragging
            isDraggingHandle = false;
            if (activeGizmo != null) Destroy(activeGizmo);
            return;
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            float pixelDist = Input.mousePosition.x - initialMouseScreen.x;
            float angle = pixelDist * rotateSpeed;

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                angle = Mathf.Round(angle / snapAngle) * snapAngle;
            }

            Quaternion rotationChange = Quaternion.AngleAxis(angle, GetLocalAxisVector());
            currentSelection.transform.rotation = initialRotation * rotationChange;
        }
        else
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 currentHitPoint = ray.GetPoint(enter);
                Vector3 offset = currentHitPoint - initialHitPoint;

                Vector3 projectedMove = Vector3.Project(offset, dragAxis);
                currentSelection.transform.position = initialObjectPos + projectedMove;
            }
        }

        // Sync Gizmo to Object
        if (activeGizmo != null)
        {
            activeGizmo.transform.position = currentSelection.transform.position;
            activeGizmo.transform.rotation = currentSelection.transform.rotation;
        }
    }

    void HandleRelease()
    {
        if (currentSelection == null) return;

        float dragDistance = Vector2.Distance(initialMouseScreen, Input.mousePosition);

        // If moved less than threshold
        if (isDraggingHandle && dragDistance < dragThreshold)
        {
            currentSelection.transform.position = initialObjectPos;
            currentSelection.transform.rotation = initialRotation;

            // Rotate around the visual axis
            currentSelection.transform.Rotate(dragAxis, clickRotateAngle, Space.World);

            if (activeGizmo != null)
            {
                activeGizmo.transform.rotation = currentSelection.transform.rotation;
            }
        }

        isDraggingHandle = false;

        // Restore Physics
        if (selectedRb != null)
        {
            selectedRb.isKinematic = wasKinematic;
            selectedRb.WakeUp();
            selectedRb = null;
        }
    }

    Vector3 GetLocalAxisVector()
    {
        if (currentHandleTag == "HandleX") return Vector3.right;
        if (currentHandleTag == "HandleY") return Vector3.up;
        return Vector3.forward;
    }

    void SelectObject(GameObject obj)
    {
        if (currentSelection == obj) return;

        ClearSelection();

        currentSelection = obj;
        if (gizmoPrefab != null)
        {
            activeGizmo = Instantiate(gizmoPrefab, currentSelection.transform.position, currentSelection.transform.rotation);
        }
    }

    void ClearSelection()
    {
        isDraggingHandle = false;
        if (activeGizmo != null) Destroy(activeGizmo);
        currentSelection = null;
    }
}