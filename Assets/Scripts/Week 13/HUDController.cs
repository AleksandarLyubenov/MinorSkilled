using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("Mode & Input")]
    public BuildMode startingMode = BuildMode.Portal;
    public KeyCode previousModeKey = KeyCode.Z;
    public KeyCode nextModeKey = KeyCode.X;
    public KeyCode toggleControlsKey = KeyCode.H;

    [System.Serializable]
    public struct ToolSetting
    {
        public BuildMode mode;
        public bool isEnabled;
    }

    [Header("Level Configuration")]
    [Tooltip("Toggle which tools are available in this level.")]
    public List<ToolSetting> toolAvailability = new List<ToolSetting>();

    // Internal list of only the modes that are currently enabled
    private List<BuildMode> activeModes = new List<BuildMode>();

    [Header("Crosshair UI")]
    public Image crosshairImage;

    [Tooltip("UI Image showing the previous tool in the cycle.")]
    public Image previousToolImage;
    [Tooltip("UI Image showing the next tool in the cycle.")]
    public Image nextToolImage;

    [Header("Sprites")]
    public Sprite portalCrosshairSprite;
    public Sprite fanCrosshairSprite;
    public Sprite bouncyCrosshairSprite;
    public Sprite slipperyCrosshairSprite;
    public Sprite stickyCrosshairSprite;
    public Sprite elasticCrosshairSprite;
    public Sprite editModeCrosshairSprite;

    [Header("Colors")]
    public Color portalAColor = Color.cyan;
    public Color portalBColor = Color.magenta;
    public Color fanColor = Color.white;
    public Color bouncyColor = new Color(1f, 0.5f, 0f); // orange
    public Color slipperyColor = Color.blue;
    public Color stickyColor = Color.green;
    public Color elasticColor = Color.yellow;
    public Color editModeColor = Color.white;

    [Header("Gravity Direction UI")]
    public Image gravityDirectionImage;

    [Serializable]
    public struct GravitySpriteMapping
    {
        public GravityDirection direction;
        public Sprite sprite;
    }

    public List<GravitySpriteMapping> gravitySprites = new();
    Dictionary<GravityDirection, Sprite> gravitySpriteLookup;

    [Header("Controls / Tooltips Panel")]
    public GameObject controlsPanel;

    public BuildMode CurrentMode { get; private set; }
    public bool IsPlacingPortalA { get; private set; } = true;
    private bool controlsVisible = true;

    private void Awake()
    {
        BuildGravitySpriteLookup();

        RefreshActiveModes();

        // Set Starting Mode
        if (activeModes.Count > 0)
        {
            if (activeModes.Contains(startingMode))
                CurrentMode = startingMode;
            else
                CurrentMode = activeModes[0];
        }

        if (gravityDirectionImage != null && GravityBus.Instance != null)
        {
            SetGravityDirection(GravityBus.Instance.CurrentGravityDirection);
        }

        UpdateCrosshairUI();
        UpdateControlsVisibility();
    }

    private void RefreshActiveModes()
    {
        activeModes.Clear();

        if (toolAvailability == null || toolAvailability.Count == 0)
        {
            foreach (BuildMode mode in Enum.GetValues(typeof(BuildMode)))
            {
                activeModes.Add(mode);
            }
        }
        else
        {
            // Only add enabled modes
            foreach (var setting in toolAvailability)
            {
                if (setting.isEnabled)
                {
                    activeModes.Add(setting.mode);
                }
            }
        }
    }

    private void Update()
    {
        HandleModeInput();
        HandleControlsToggle();
    }

    private void HandleModeInput()
    {
        // Prevent input if no tools are available
        if (activeModes.Count <= 1) return;

        bool changed = false;

        if (Input.GetKeyDown(previousModeKey))
        {
            CycleMode(-1);
            changed = true;
        }
        else if (Input.GetKeyDown(nextModeKey))
        {
            CycleMode(+1);
            changed = true;
        }

        if (changed) UpdateCrosshairUI();
    }

    private void HandleControlsToggle()
    {
        if (Input.GetKeyDown(toggleControlsKey))
        {
            controlsVisible = !controlsVisible;
            UpdateControlsVisibility();
        }
    }

    private void CycleMode(int direction)
    {
        if (activeModes.Count == 0) return;

        // Find where we are in the active list
        int currentIndex = activeModes.IndexOf(CurrentMode);

        // Calculate new index wrapping around
        int newIndex = (currentIndex + direction + activeModes.Count) % activeModes.Count;

        CurrentMode = activeModes[newIndex];
    }

    private void UpdateControlsVisibility()
    {
        if (controlsPanel != null) controlsPanel.SetActive(controlsVisible);
    }

    void BuildGravitySpriteLookup()
    {
        gravitySpriteLookup = new Dictionary<GravityDirection, Sprite>();
        foreach (var mapping in gravitySprites)
        {
            if (mapping.sprite == null) continue;
            if (!gravitySpriteLookup.ContainsKey(mapping.direction))
                gravitySpriteLookup.Add(mapping.direction, mapping.sprite);
        }
    }

    private void UpdateCrosshairUI()
    {
        if (activeModes.Count == 0) return;

        if (crosshairImage != null)
        {
            var (sprite, color) = GetToolVisuals(CurrentMode);
            crosshairImage.sprite = sprite;
            crosshairImage.color = color;
        }

        int currentIndex = activeModes.IndexOf(CurrentMode);

        if (previousToolImage != null)
        {
            int prevIndex = (currentIndex - 1 + activeModes.Count) % activeModes.Count;
            var (sprite, color) = GetToolVisuals(activeModes[prevIndex]);
            previousToolImage.sprite = sprite;
            previousToolImage.color = color;
            previousToolImage.color = new Color(color.r, color.g, color.b, 0.5f);
        }

        if (nextToolImage != null)
        {
            int nextIndex = (currentIndex + 1) % activeModes.Count;
            var (sprite, color) = GetToolVisuals(activeModes[nextIndex]);
            nextToolImage.sprite = sprite;
            nextToolImage.color = color;
            nextToolImage.color = new Color(color.r, color.g, color.b, 0.5f);
        }
    }

    // Helper to get visuals for any mode (used for Main, Prev, and Next)
    private (Sprite sprite, Color color) GetToolVisuals(BuildMode mode)
    {
        switch (mode)
        {
            case BuildMode.Portal:
                Color col = (mode == CurrentMode) ? (IsPlacingPortalA ? portalAColor : portalBColor) : portalAColor;
                return (portalCrosshairSprite, col);

            case BuildMode.Fan:
                return (fanCrosshairSprite, fanColor);

            case BuildMode.SurfaceBouncy:
                return (bouncyCrosshairSprite, bouncyColor);

            case BuildMode.SurfaceSlippery:
                return (slipperyCrosshairSprite, slipperyColor);

            case BuildMode.SurfaceSticky:
                return (stickyCrosshairSprite, stickyColor);

            case BuildMode.Elastic:
                return (elasticCrosshairSprite, elasticColor);

            case BuildMode.Edit:
                return (editModeCrosshairSprite, editModeColor);

            default:
                return (null, Color.white);
        }
    }

    public void SetGravityDirection(GravityDirection direction)
    {
        if (gravityDirectionImage == null || gravitySpriteLookup == null) return;
        if (gravitySpriteLookup.TryGetValue(direction, out Sprite sprite) && sprite != null)
            gravityDirectionImage.sprite = sprite;
    }

    public void SetPortalIsA(bool isA)
    {
        IsPlacingPortalA = isA;
        if (CurrentMode == BuildMode.Portal) UpdateCrosshairUI();
    }

    public void SetBuildMode(BuildMode mode)
    {
        // Only allow setting if enabled in this level
        if (activeModes.Contains(mode))
        {
            CurrentMode = mode;
            UpdateCrosshairUI();
        }
    }

    private void OnValidate()
    {
        if (toolAvailability.Count == 0)
        {
            var modes = Enum.GetValues(typeof(BuildMode));
            foreach (BuildMode m in modes)
            {
                toolAvailability.Add(new ToolSetting { mode = m, isEnabled = true });
            }
        }
    }
}