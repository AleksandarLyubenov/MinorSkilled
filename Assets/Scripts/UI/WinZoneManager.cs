using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WinZoneManager : MonoBehaviour
{
    public enum WinCondition { OneBall, AllBalls, Percentage }

    [Header("Condition Settings")]
    public WinCondition condition = WinCondition.OneBall;

    [Range(0f, 100f)]
    [Tooltip("Only used if Condition is 'Percentage'.")]
    public float targetPercentage = 50f;

    [Header("Filter")]
    public string requiredTag = "Key";
    public LayerMask allowedLayers = ~0;

    // Tracking
    private readonly HashSet<Rigidbody> ballsInside = new();
    private Collider zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsBall(other)) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        if (ballsInside.Add(rb))
        {
            Debug.Log($"[WinZone] Ball Entered: {rb.name}. Count: {ballsInside.Count}");
            CheckWinCondition();
        }
    }

    void Update()
    {
        // Only check while playing to save performance
        if (LevelManager.Instance == null || !LevelManager.Instance.IsPlaying) return;

        // Re-validate balls
        int removedCount = ballsInside.RemoveWhere(rb => rb == null || !rb.gameObject.activeInHierarchy || !StillInside(rb));

        if (removedCount > 0)
        {
            Debug.Log($"[WinZone] Ball left or invalidated. Count: {ballsInside.Count}");
        }

        // Continuously check win
        CheckWinCondition();
    }

    void CheckWinCondition()
    {
        if (LevelManager.Instance == null) return;

        int totalBallsInLevel = LevelManager.Instance.balls.Length;
        int currentCount = ballsInside.Count;

        bool won = false;

        switch (condition)
        {
            case WinCondition.OneBall:
                if (currentCount >= 1) won = true;
                break;

            case WinCondition.AllBalls:
                if (currentCount >= totalBallsInLevel) won = true;
                break;

            case WinCondition.Percentage:
                float percent = (float)currentCount / totalBallsInLevel * 100f;
                if (percent >= targetPercentage) won = true;
                break;
        }

        if (won)
        {
            LevelManager.Instance.OnLevelPassed();
            // Disable this script, don't spam the win call
            this.enabled = false;
        }
    }
    bool IsBall(Collider c)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !c.CompareTag(requiredTag)) return false;
        if ((allowedLayers.value & (1 << c.gameObject.layer)) == 0) return false;
        return true;
    }

    bool StillInside(Rigidbody rb)
    {
        var cols = rb.GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            // Precise bounds check
            if (c.enabled && zoneCollider.bounds.Intersects(c.bounds))
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (!col) return;

        Gizmos.color = ballsInside.Count > 0 ? new Color(0, 1, 0, 0.3f) : new Color(1, 0.92f, 0.016f, 0.3f); // Yellow if empty, Green if occupied

        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider b)
        {
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = new Color(1, 1, 1, 0.5f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
        else if (col is SphereCollider s)
        {
            Gizmos.DrawSphere(s.center, s.radius);
            Gizmos.color = new Color(1, 1, 1, 0.5f);
            Gizmos.DrawWireSphere(s.center, s.radius);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}