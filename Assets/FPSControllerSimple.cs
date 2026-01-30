using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSControllerSingle : MonoBehaviour
{
	[Header("References")]
	[Tooltip("Your child camera (Main Camera)")]
	public Camera playerCamera;

	[Header("Movement")]
	public float moveSpeed = 5f;
	public float jumpHeight = 2f;
	public float gravity = -9.81f;

	[Header("Mouse Look")]
	public float mouseSensitivity = 120f; // adjust in Inspector
	public float minPitch = -89f;
	public float maxPitch = 89f;

	private CharacterController controller;
	private Vector3 velocity;   // vertical velocity only
	private float pitch = 0f;   // camera pitch

	void Awake()
	{
		controller = GetComponent<CharacterController>();
		if (playerCamera == null)
		{
			playerCamera = GetComponentInChildren<Camera>();
		}
	}

	void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	void Update()
	{
		HandleMouseLook();
		HandleMoveAndJump();
	}

	void HandleMouseLook()
	{
		float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
		float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

		// Yaw on body (Capsule)
		transform.Rotate(Vector3.up, mouseX, Space.Self);

		// Pitch on camera only
		pitch -= mouseY;
		pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
		if (playerCamera)
			playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
	}

	void HandleMoveAndJump()
	{
		bool grounded = controller.isGrounded;
		if (grounded && velocity.y < 0f)
			velocity.y = -2f; // keep snapped to ground

		// WASD input
		float x = Input.GetAxisRaw("Horizontal");
		float z = Input.GetAxisRaw("Vertical");

		// Camera-relative, but flattened to the horizontal plane
		Vector3 camForward = playerCamera ? playerCamera.transform.forward : transform.forward;
		Vector3 camRight = playerCamera ? playerCamera.transform.right : transform.right;

		camForward = Vector3.ProjectOnPlane(camForward, Vector3.up).normalized;
		camRight = Vector3.ProjectOnPlane(camRight, Vector3.up).normalized;

		Vector3 move = (camRight * x + camForward * z);
		if (move.sqrMagnitude > 1f) move.Normalize();

		controller.Move(move * moveSpeed * Time.deltaTime);

		// Jump
		if (grounded && Input.GetButtonDown("Jump"))
			velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

		// Gravity
		velocity.y += gravity * Time.deltaTime;
		controller.Move(velocity * Time.deltaTime);
	}
}
