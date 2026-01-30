using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Fan : MonoBehaviour
{
    [Header("Force Settings")]
    [Tooltip("Base acceleration applied along the fan's forward direction.")]
    public float baseStrength = 15f;

    [Tooltip("Maximum effective distance of the airflow from the fan's origin.")]
    public float maxRange = 5f;

    [Tooltip("Falloff of force over distance: x=0 at fan, x=1 at maxRange.")]
    public AnimationCurve distanceFalloff = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("Pulsing")]
    public bool pulseMode = false;

    [Tooltip("Pulse curve over time (looped). x=0..1 time, y=0..1 strength factor.")]
    public AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("How fast the pulse loops (cycles per second).")]
    public float pulseFrequency = 0.5f;

    [Header("Stability / Centering")]
    public float centerStrength = 3f;
    public float maxLateralSpeed = 5f;

    [Header("Filtering")]
    public LayerMask affectedLayers = ~0;

    [Header("Visuals")]
    public ParticleSystem airParticles;

    [Header("State")]
    public bool isOn = true;

    private Collider triggerVolume;
    private float currentStrength;

    private void Awake()
    {
        triggerVolume = GetComponent<Collider>();
        triggerVolume.isTrigger = true;

        // Auto-size trigger to match range on Z axis if using BoxCollider
        BoxCollider box = triggerVolume as BoxCollider;
        if (box != null)
        {
            Vector3 size = box.size;
            size.z = maxRange;
            box.size = size;
            box.center = new Vector3(0f, 0f, maxRange * 0.5f);
        }

        if (airParticles != null)
        {
            var shape = airParticles.shape;
            shape.scale = new Vector3(shape.scale.x, shape.scale.y, maxRange);
        }
    }

    private void Update()
    {
        UpdateStrengthOverTime();
        UpdateVisuals();
    }

    private void UpdateStrengthOverTime()
    {
        if (!isOn)
        {
            currentStrength = 0f;
            return;
        }

        float strength = baseStrength;

        if (pulseMode)
        {
            float t = (Time.time * pulseFrequency) % 1f;
            float factor = Mathf.Clamp01(pulseCurve.Evaluate(t));
            strength *= factor;
        }

        currentStrength = strength;
    }

    private void UpdateVisuals()
    {
        if (airParticles == null) return;

        var emission = airParticles.emission;
        emission.enabled = isOn;

        if (!isOn) return;

        // modulate particle rate with current strength
        var rate = emission.rateOverTime;
        rate.constant = Mathf.Max(5f, currentStrength * 1.5f);
        emission.rateOverTime = rate;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isOn || currentStrength <= 0.0001f)
            return;

        if ((affectedLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null || rb.isKinematic)
            return;

        ApplyAirflow(rb);
    }

    private void ApplyAirflow(Rigidbody rb)
    {
        Vector3 localPos = transform.InverseTransformPoint(rb.worldCenterOfMass);
        float distance = Mathf.Clamp(localPos.z, 0f, maxRange);
        float t = maxRange > 0.001f ? distance / maxRange : 0f;
        float falloff = distanceFalloff.Evaluate(t);

        Vector3 flowDir = transform.forward;
        Vector3 force = flowDir * (currentStrength * falloff);

        if (centerStrength > 0f)
        {
            Vector3 worldAxisPoint = transform.TransformPoint(new Vector3(0f, 0f, localPos.z));
            Vector3 toAxis = worldAxisPoint - rb.worldCenterOfMass;
            force += toAxis * centerStrength;
        }

        // Clamp lateral velocity
        Vector3 vel = rb.linearVelocity;
        Vector3 along = Vector3.Project(vel, flowDir);
        Vector3 lateral = vel - along;
        if (lateral.magnitude > maxLateralSpeed)
        {
            lateral = lateral.normalized * maxLateralSpeed;
            rb.linearVelocity = along + lateral;
        }

        rb.AddForce(force, ForceMode.Acceleration);
    }
    public void SetStrength(float newStrength)
    {
        baseStrength = Mathf.Max(0f, newStrength);
    }

    public void Toggle()
    {
        SetOn(!isOn);
    }

    public void SetOn(bool value)
    {
        isOn = value;
    }

    public void SetPulseMode(bool value)
    {
        pulseMode = value;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isOn ? Color.cyan : Color.gray;
        Gizmos.DrawRay(transform.position, transform.forward * maxRange);
    }
}
