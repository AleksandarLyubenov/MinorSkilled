using UnityEngine;

[DisallowMultipleComponent]
public class Portal : MonoBehaviour
{
    [Header("Components")]
    public Transform screen;
    public Transform border;
    public BoxCollider box;
    public Camera viewpointCamera;

    [Header("Link")]
    public Portal linked;

    [Header("Rules")]
    [Tooltip("If true, prevents entering from behind using velocity checks.")]
    public bool requireFrontEntry = false;

    [Header("Tuning")]
    public float reenterCooldown = 0.75f;
    public float exitOffset = 0.5f;

    [Header("Viewpoint")]
    public float defaultViewDistance = 2.4f;

    [Header("Debug")]
    public Color portalTint = Color.cyan;

    public void SetSize(float width, float height, float thickness, float borderMargin, float screenZ, float borderZ)
    {
        if (box)
        {
            box.size = new Vector3(width, height, thickness);
            box.center = new Vector3(0f, 0f, thickness * 0.5f);
        }
        if (screen)
        {
            screen.localScale = new Vector3(width, height, 1f);
            screen.localPosition = new Vector3(0f, 0f, screenZ);
        }
        if (border)
        {
            border.localScale = new Vector3(width + borderMargin, height + borderMargin, 1f);
            border.localPosition = new Vector3(0f, 0f, borderZ);
        }
    }

    public void SnapViewpointInFront(float minClearance, bool flipY180 = true)
    {
        if (!viewpointCamera) return;
        float z = Mathf.Max(defaultViewDistance, minClearance);
        var t = viewpointCamera.transform;
        t.localPosition = new Vector3(0f, 0f, z);
        t.localRotation = flipY180 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
    }

    public void SetLinked(Portal other) => linked = other;

    void OnDrawGizmos()
    {
        Gizmos.color = portalTint;
        Gizmos.matrix = transform.localToWorldMatrix;
        float z = box ? box.size.z : 0.12f;
        Vector3 size = new Vector3(box ? box.size.x : 2f, box ? box.size.y : 2.5f, z);
        Gizmos.DrawWireCube(new Vector3(0, 0, box ? box.center.z : (z * 0.5f)), size);
        Gizmos.matrix = Matrix4x4.identity;

        if (linked)
        {
            Gizmos.color = Color.Lerp(portalTint, linked.portalTint, 0.5f);
            Gizmos.DrawLine(transform.position, linked.transform.position);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!linked) return;

        var trav = other.GetComponent<PortalTraveler>();
        if (!trav) trav = other.gameObject.AddComponent<PortalTraveler>();

        if (trav.IsOnCooldown(this)) return;

        if (requireFrontEntry)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
            {
                Vector3 checkVel = trav.enterVelocity.sqrMagnitude > 0.1f ? trav.enterVelocity : GetVel(rb);

                if (Vector3.Dot(checkVel, transform.forward) > 0) return;
            }
        }

        Teleport(other, trav);
    }

    void Teleport(Collider other, PortalTraveler trav)
    {
        Transform inTrans = transform;
        Transform outTrans = linked.transform;
        Rigidbody rb = other.attachedRigidbody;

        var through = outTrans.localToWorldMatrix
                    * Matrix4x4.Rotate(Quaternion.AngleAxis(180f, Vector3.up))
                    * inTrans.worldToLocalMatrix;

        Quaternion newRot = through.rotation * other.transform.rotation;

        // Convert to local, Flip X, Keep Y, Force Z Offset (-exitOffset)
        Vector3 localPos = inTrans.InverseTransformPoint(other.transform.position);
        Vector3 newLocalPos = new Vector3(-localPos.x, localPos.y, -exitOffset);
        Vector3 finalPos = outTrans.TransformPoint(newLocalPos);

        if (rb != null && !rb.isKinematic)
        {
            // Retrieve from Memory
            Vector3 storedVel = trav.enterVelocity;

            // If memory is empty, try current velocity
            if (storedVel.sqrMagnitude < 0.01f) storedVel = GetVel(rb);

            float originalSpeed = storedVel.magnitude;

            // Rotate vector using the matrix
            Vector3 rawNewVel = through.MultiplyVector(storedVel);

            // Convert to Exit Local Space
            Vector3 localVel = outTrans.InverseTransformDirection(rawNewVel);

            // This ensures it shoots OUT of the portal, never back into the wall
            localVel.z = -Mathf.Abs(localVel.z);

            // Convert back to World
            Vector3 finalWorldVel = outTrans.TransformDirection(localVel);

            // Use Max(speed, 0.5f) to ensure it doesn't get stuck if speed was near zero
            finalWorldVel = finalWorldVel.normalized * Mathf.Max(originalSpeed, 0.5f);

            // Move Position
            rb.position = finalPos;
            rb.rotation = newRot;

            rb.Sleep();
            rb.WakeUp();

            SetVel(rb, finalWorldVel);

            rb.angularVelocity = through.MultiplyVector(rb.angularVelocity);
        }
        else
        {
            // Non-Rigidbody Fallback
            other.transform.SetPositionAndRotation(finalPos, newRot);
        }

        // Set Cooldown on destination
        trav.SetCooldown(linked, reenterCooldown);
    }

    static Vector3 GetVel(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }
    static void SetVel(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}