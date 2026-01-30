using UnityEngine;
using UnityEngine.UI;

public class FanEditor : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;

    [Header("Selection")]
    public LayerMask fanLayerMask = ~0;
    public float maxSelectDistance = 200f;

    [Header("Strength Editing")]
    public float strengthStep = 2f;
    public float minStrength = 0f;
    public float maxStrength = 60f;

    [Header("Rotation")]
    public float rotationStep = 10f; // degrees per tap

    [Header("HUD / Mode Gating")]
    [Tooltip("Optional HUD reference. If present, editing only works in Fan mode.")]
    public HUDController hud;
    public bool requireFanMode = true;

    private Fan selectedFan;
    private Outline outline; // optional if you use some outline component

    private void Awake()
    {
        if (hud == null)
        {
            hud = FindFirstObjectByType<HUDController>();
        }
    }

    private bool IsFanModeActive()
    {
        if (!requireFanMode) return true;
        if (hud == null) return true;
        return hud.CurrentMode == BuildMode.Fan;
    }

    private void Update()
    {
        // If not in Fan mode, ignore all interaction.
        if (!IsFanModeActive())
            return;

        HandleSelection();
        HandleEditing();
    }

    private void HandleSelection()
    {
        if (!Input.GetMouseButtonDown(1)) // RMB
            return;

        if (playerCamera == null)
        {
            Debug.LogWarning("FanEditor: missing playerCamera.");
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxSelectDistance, fanLayerMask, QueryTriggerInteraction.Ignore))
        {
            Fan fan = hit.collider.GetComponentInParent<Fan>();
            if (fan != null)
            {
                SelectFan(fan);
                return;
            }
        }

        // Clicked empty space
        SelectFan(null);
    }

    private void SelectFan(Fan fan)
    {
        if (selectedFan == fan) return;

        if (selectedFan != null)
        {
            SetFanHighlight(selectedFan, false);
        }

        selectedFan = fan;

        if (selectedFan != null)
        {
            SetFanHighlight(selectedFan, true);
            Debug.Log($"Selected fan: {selectedFan.name}");
        }
        else
        {
            Debug.Log("Deselected fan.");
        }
    }

    private void SetFanHighlight(Fan fan, bool state)
    {
        // for VFX
    }

    private void HandleEditing()
    {
        if (selectedFan == null)
            return;

        // Strength via scroll wheel
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float newStrength = Mathf.Clamp(selectedFan.baseStrength + scroll * strengthStep, minStrength, maxStrength);
            selectedFan.SetStrength(newStrength);
        }

        // Rotate around local Y with Z/X
        if (Input.GetKeyDown(KeyCode.Z))
        {
            selectedFan.transform.Rotate(Vector3.up, -rotationStep, Space.Self);
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            selectedFan.transform.Rotate(Vector3.up, rotationStep, Space.Self);
        }

        // Toggle pulse mode with Space
        if (Input.GetKeyDown(KeyCode.Space))
        {
            selectedFan.SetPulseMode(!selectedFan.pulseMode);
        }

        // Toggle on/off with T
        if (Input.GetKeyDown(KeyCode.T))
        {
            selectedFan.Toggle();
        }

        // Delete with MMB / Backspace
        if (Input.GetKeyDown(KeyCode.Mouse2) || Input.GetKeyDown(KeyCode.Backspace))
        {
            Destroy(selectedFan.gameObject);
            SelectFan(null);
        }
    }
}
