using UnityEngine;

public class GravityHudBridge : MonoBehaviour
{
    [Tooltip("Optional reference to the HUD controller. If left empty, it will try to find one at runtime.")]
    public HUDController hud;

    bool subscribed;

    void Awake()
    {
        if (hud == null)
        {
            hud = FindFirstObjectByType<HUDController>();
        }
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void OnDisable()
    {
        TryUnsubscribe();
    }

    void Update()
    {
        // If GravityBus was created after this object enabled, subscribe later.
        if (!subscribed)
        {
            TrySubscribe();
        }
    }

    void TrySubscribe()
    {
        if (subscribed) return;

        if (GravityBus.Instance == null)
            return; // bus not alive yet, try again next frame

        GravityBus.Instance.OnGravityDirectionChanged += HandleGravityDirectionChanged;
        subscribed = true;
        // Debug.Log("GravityHudBridge: subscribed to GravityBus.");
    }

    void TryUnsubscribe()
    {
        if (!subscribed) return;

        if (GravityBus.Instance != null)
        {
            GravityBus.Instance.OnGravityDirectionChanged -= HandleGravityDirectionChanged;
        }

        subscribed = false;
    }

    void HandleGravityDirectionChanged(GravityDirection dir)
    {
        if (hud == null)
            return; // scene without HUD is fine – just ignore

        hud.SetGravityDirection(dir);
        // Debug.Log($"GravityHudBridge: HUD updated to {dir}");
    }
}
