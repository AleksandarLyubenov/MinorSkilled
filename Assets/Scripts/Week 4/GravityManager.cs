using UnityEngine;

/// <summary>
/// Central gravity authority. Updates Physics.gravity and exposes the current direction/magnitude.
/// Hotkeys are editable in the Inspector.
/// </summary>
public class GravityManager : MonoBehaviour
{
    public static GravityManager Instance { get; private set; }

    [Header("Gravity")]
    [Tooltip("Absolute gravity strength (m/s²).")]
    public float gravityMagnitude = 9.81f;

    [Tooltip("Initial gravity direction (unit, points where things fall).")]
    public Vector3 initialDirection = Vector3.down;

    [Header("Hotkeys (editable)")]
    public KeyCode posXKey = KeyCode.Insert;   // +X
    public KeyCode negXKey = KeyCode.Delete;   // -X
    public KeyCode posYKey = KeyCode.Home;     // +Y
    public KeyCode negYKey = KeyCode.End;      // -Y
    public KeyCode posZKey = KeyCode.PageUp;   // +Z
    public KeyCode negZKey = KeyCode.PageDown; // -Z

    public Vector3 GravityDir { get; private set; } // unit vector
    public Vector3 GravityVector => GravityDir * gravityMagnitude;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SetDirection(initialDirection);
    }

    void Update()
    {
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
        GravityDir = dir.normalized;
        Physics.gravity = GravityVector; // drives all Rigidbodies
    }
}
