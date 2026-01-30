using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PortalTraveler : MonoBehaviour
{
    // Tracks cooldowns
    private HashSet<Portal> cooldowns = new HashSet<Portal>();

    // Stores the last valid movement speed
    public Vector3 enterVelocity { get; private set; }

    private Rigidbody rb;
    private Vector3 lastFrameVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            enterVelocity = rb.linearVelocity;
        }
    }

    public bool IsOnCooldown(Portal portal)
    {
        return cooldowns.Contains(portal);
    }

    public void SetCooldown(Portal portal, float duration)
    {
        if (!cooldowns.Contains(portal))
        {
            cooldowns.Add(portal);
            StartCoroutine(RemoveCooldown(portal, duration));
        }
    }

    private System.Collections.IEnumerator RemoveCooldown(Portal portal, float duration)
    {
        yield return new WaitForSeconds(duration);
        cooldowns.Remove(portal);
    }
}