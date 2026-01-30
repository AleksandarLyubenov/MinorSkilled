using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KeyZone : MonoBehaviour
{
    [Header("Target")]
    public MoverKinematic mover;

    [Header("Filter")]
    public string requiredTag = "Key";
    public LayerMask allowedLayers = ~0;

    private readonly HashSet<Rigidbody> inside = new();
    private Collider zone;

    void Awake()
    {
        zone = GetComponent<Collider>();
        zone.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!EnabledFor(other)) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;

        inside.Add(rb);
        mover?.SetActive(true);
        Debug.Log($"[KeyZone] ENTER {rb.name}");
    }

    void Update()
    {
        // Re-check every tracked rigidbody against zone bounds
        inside.RemoveWhere(rb => rb == null || !rb.gameObject.activeInHierarchy || !StillInside(rb));

        if (inside.Count == 0)
        {
            mover?.SetActive(false);
        }
    }

    bool StillInside(Rigidbody rb)
    {
        // Check all colliders on the rigidbody
        var cols = rb.GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            if (c.enabled && zone.bounds.Intersects(c.bounds))
                return true;
        }
        return false;
    }

    bool EnabledFor(Collider c)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !c.CompareTag(requiredTag)) return false;
        if ((allowedLayers.value & (1 << c.gameObject.layer)) == 0) return false;
        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (!col) return;
        Gizmos.color = inside.Count > 0 ? new Color(0, 1, 0, 0.25f) : new Color(1, 0, 0, 0.25f);
        if (col is BoxCollider b)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = inside.Count > 0 ? new Color(0, 1, 0, 0.8f) : new Color(1, 0, 0, 0.8f);
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
#endif
}
