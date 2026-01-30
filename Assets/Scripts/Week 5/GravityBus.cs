using System;
using UnityEngine;

/// <summary>
/// Global gravity authority + event bus.
/// Emits events on direction/strength change.
/// Does NOT touch Physics.gravity (keeps systems explicit and testable).
/// </summary>
public class GravityBus : MonoBehaviour
{
    public static GravityBus Instance { get; private set; }

    [Header("Gravity")]
    [Tooltip("Absolute strength in m/s².")]
    public float gravityMagnitude = 9.81f;

    [Tooltip("Initial direction (unit) — points where things fall.")]
    public Vector3 initialDirection = Vector3.down;

    [Header("Controls")]
    [Tooltip("If false, keyboard hotkeys for gravity shifting are ignored.")]
    public bool enableControls = true;

    [Header("Hotkeys (editable)")]
    public KeyCode posXKey = KeyCode.Insert;   // +X
    public KeyCode negXKey = KeyCode.Delete;   // -X
    public KeyCode posYKey = KeyCode.Home;     // +Y
    public KeyCode negYKey = KeyCode.End;      // -Y
    public KeyCode posZKey = KeyCode.PageUp;   // +Z
    public KeyCode negZKey = KeyCode.PageDown; // -Z

    public Vector3 Direction { get; private set; } // unit
    public float Magnitude => gravityMagnitude;
    public Vector3 Vector => Direction * gravityMagnitude;

    public GravityDirection CurrentGravityDirection { get; private set; }

    public event Action<Vector3, float, Vector3> OnGravityChanged;

    public event Action<GravityDirection> OnGravityDirectionChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SetDirection(initialDirection);
    }

    void Update()
    {
        if (!enableControls) return;

        if (Input.GetKeyDown(posXKey)) SetDirection(Vector3.right);
        if (Input.GetKeyDown(negXKey)) SetDirection(Vector3.left);
        if (Input.GetKeyDown(posYKey)) SetDirection(Vector3.up);
        if (Input.GetKeyDown(negYKey)) SetDirection(Vector3.down);
        if (Input.GetKeyDown(posZKey)) SetDirection(Vector3.forward);
        if (Input.GetKeyDown(negZKey)) SetDirection(Vector3.back);
    }

    public void SetDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 1e-6f) return;

        Direction = dir.normalized;
        CurrentGravityDirection = MapVectorToEnum(Direction);

        OnGravityChanged?.Invoke(Direction, gravityMagnitude, Vector);
        OnGravityDirectionChanged?.Invoke(CurrentGravityDirection);
    }

    private GravityDirection MapVectorToEnum(Vector3 dir)
    {
        // assumes axis-aligned directions
        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);
        float az = Mathf.Abs(dir.z);

        if (ax >= ay && ax >= az)
        {
            return dir.x >= 0f ? GravityDirection.XPos : GravityDirection.XNeg;
        }

        if (ay >= ax && ay >= az)
        {
            return dir.y >= 0f ? GravityDirection.YPos : GravityDirection.YNeg;
        }

        return dir.z >= 0f ? GravityDirection.ZPos : GravityDirection.ZNeg;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Vector3 origin = transform.position;
        Vector3 v = (Application.isPlaying ? Vector : initialDirection.normalized * gravityMagnitude) * 0.25f;
        Gizmos.DrawLine(origin, origin + v);
        Gizmos.DrawSphere(origin + v, 0.05f);
    }
#endif
}