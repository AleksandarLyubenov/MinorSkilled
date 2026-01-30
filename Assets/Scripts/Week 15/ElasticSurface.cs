using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class SoftBodySurface : MonoBehaviour
{
    [Header("Physics (The Bounce)")]
    public float bounceForce = 500f;
    public float surfaceDrag = 2f;

    [Tooltip("The spring gets exponentially stiffer as it approaches this depth.")]
    public float maxPenetrationDepth = 2.0f;

    [Tooltip("How much harder the surface becomes at max depth (e.g., 10x force).")]
    public float bottomOutMultiplier = 15f;

    [Header("Visuals (The Deformation)")]
    public float springStiffness = 40f;
    public float damping = 3f;
    public float deformationRadius = 3f;

    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] displacedVertices;
    private Vector3[] vertexVelocities;
    private MeshCollider meshCollider;

    // Track which side each object entered from (1 = Top, -1 = Bottom)
    private Dictionary<Rigidbody, float> entrySides = new Dictionary<Rigidbody, float>();

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        meshCollider = GetComponent<MeshCollider>();

        if (mesh.name == "Plane Instance") { }
        else { mesh = Instantiate(mesh); GetComponent<MeshFilter>().mesh = mesh; }

        originalVertices = mesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
        vertexVelocities = new Vector3[originalVertices.Length];
        System.Array.Copy(originalVertices, displacedVertices, originalVertices.Length);

        mesh.MarkDynamic();
        meshCollider.isTrigger = true;
    }

    void FixedUpdate()
    {
        UpdateSprings();
    }

    void UpdateSprings()
    {
        bool meshChanged = false;
        float dt = Time.fixedDeltaTime;

        for (int i = 0; i < displacedVertices.Length; i++)
        {
            Vector3 displacement = displacedVertices[i] - originalVertices[i];
            Vector3 force = -springStiffness * displacement;
            force -= damping * vertexVelocities[i];

            vertexVelocities[i] += force * dt;
            displacedVertices[i] += vertexVelocities[i] * dt;

            if (displacement.sqrMagnitude > 0.0001f || vertexVelocities[i].sqrMagnitude > 0.0001f)
                meshChanged = true;
        }

        if (meshChanged)
        {
            mesh.vertices = displacedVertices;
            mesh.RecalculateNormals();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        if (rb.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Vector3 localPos = transform.InverseTransformPoint(other.transform.position);
        float side = Mathf.Sign(localPos.y);

        if (!entrySides.ContainsKey(rb))
        {
            entrySides.Add(rb, side);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        if (!entrySides.TryGetValue(rb, out float side))
        {
            side = Mathf.Sign(transform.InverseTransformPoint(other.transform.position).y);
            entrySides[rb] = side;
        }

        Vector3 localBallPos = transform.InverseTransformPoint(other.transform.position);
        float distFromPlane = Mathf.Abs(localBallPos.y);
        float ballRadius = other.bounds.extents.y;
        float penetration = ballRadius - distFromPlane;

        if (penetration > 0)
        {
            float depthRatio = penetration / maxPenetrationDepth;

            float stiffnessCurve = 1f + (depthRatio * depthRatio * (bottomOutMultiplier - 1f));

            Vector3 pushDir = transform.up * side;

            Vector3 finalForce = pushDir * (penetration * bounceForce * stiffnessCurve);

            rb.AddForce(finalForce - (rb.linearVelocity * surfaceDrag));

            // Deform Mesh
            DeformMesh(other.transform.position, penetration, side);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && entrySides.ContainsKey(rb))
        {
            entrySides.Remove(rb);
        }
    }

    void DeformMesh(Vector3 ballWorldPos, float penetrationDepth, float side)
    {
        Vector3 localPos = transform.InverseTransformPoint(ballWorldPos);

        for (int i = 0; i < displacedVertices.Length; i++)
        {
            float dist = Vector2.Distance(new Vector2(localPos.x, localPos.z), new Vector2(originalVertices[i].x, originalVertices[i].z));

            if (dist < deformationRadius)
            {
                float factor = 1f - (dist / deformationRadius);
                factor = Mathf.SmoothStep(0, 1, factor);
                float targetY = originalVertices[i].y - (penetrationDepth * factor * side);

                if (side > 0) // Top Hit
                {
                    if (displacedVertices[i].y > targetY)
                    {
                        displacedVertices[i].y = Mathf.Lerp(displacedVertices[i].y, targetY, 0.2f);
                        if (vertexVelocities[i].y > 0) vertexVelocities[i].y = 0;
                    }
                }
                else // Bottom Hit
                {
                    if (displacedVertices[i].y < targetY)
                    {
                        displacedVertices[i].y = Mathf.Lerp(displacedVertices[i].y, targetY, 0.2f);
                        if (vertexVelocities[i].y < 0) vertexVelocities[i].y = 0;
                    }
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, new Vector3(1, 0.1f, 1));
    }
}