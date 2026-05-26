using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class HaloPlayerController : MonoBehaviour
{
	[Header("Speed Settings")]
	public float walkSpeed = 4f;
	public float runSpeed = 7.5f;
	public float crouchSpeed = 2f;
	public float slopeForce = 3f;

	[Header("Jump & Physics Settings")]
	public float jumpHeight = 1.2f;        // Realistic human jump height (approx. 4 feet)
	public float gravity = -19.6f;          // Realistic snappy gravity (-9.81 * 2 for snappy game feel)

	[Header("Crouch Settings")]
	public float standingHeight = 2f;
	public float crouchingHeight = 1f;
	public float crouchTransitionSpeed = 10f;
	public Transform playerCamera;          // Reference to the child Camera

	private CharacterController controller;
	private Vector3 velocity;
	private bool isGrounded;
	private bool isCrouching;

	// Camera eye heights
	private float cameraStandingY = 1.8f;
	private float cameraCrouchingY = 0.8f;
	private float targetCameraY;

	void Start()
	{
		controller = GetComponent<CharacterController>();

		// If playerCamera is not assigned, try to find it in children
		if (playerCamera == null)
		{
			Camera cam = GetComponentInChildren<Camera>();
			if (cam != null)
			{
				playerCamera = cam.transform;
			}
		}

		targetCameraY = cameraStandingY;
	}

	void Update()
	{
		// Ground Check
		isGrounded = controller.isGrounded;
		if (isGrounded && velocity.y < 0)
		{
			velocity.y = -2f; // snaps player to ground/slopes
		}

		// Handle Crouching Toggle/Hold
		HandleCrouch();

		// Read Movement Input
		float inputX = 0f;
		float inputZ = 0f;

		if (GetKey(KeyCode.W)) inputZ = 1f;
		else if (GetKey(KeyCode.S)) inputZ = -1f;

		if (GetKey(KeyCode.D)) inputX = 1f;
		else if (GetKey(KeyCode.A)) inputX = -1f;

		// Calculate target direction relative to player facing orientation
		Vector3 move = transform.right * inputX + transform.forward * inputZ;
		if (move.magnitude > 1f) move.Normalize();

		// Determine speed based on state
		float speed = walkSpeed;
		if (isCrouching)
		{
			speed = crouchSpeed;
		}
		else if (GetKey(KeyCode.LeftShift) && inputZ > 0.5f)
		{
			speed = runSpeed; // Sprinting (forward only)
		}

		// Move player snappily (realistic instant stop/start traction)
		controller.Move(move * speed * Time.deltaTime);

		// Jump logic
		if (GetKeyDown(KeyCode.Space) && isGrounded && !isCrouching)
		{
			// Snappy jump velocity calculation
			velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
		}

		// Apply snappy gravity
		velocity.y += gravity * Time.deltaTime;
		controller.Move(velocity * Time.deltaTime);

		// Smoothly transition camera eye height during crouching
		if (playerCamera != null)
		{
			Vector3 localPos = playerCamera.localPosition;
			float currentY = Mathf.Lerp(localPos.y, targetCameraY, Time.deltaTime * crouchTransitionSpeed);
			playerCamera.localPosition = new Vector3(localPos.x, currentY, localPos.z);
		}
	}

	private void HandleCrouch()
	{
		// Check crouch key (C or Left Control)
		bool wantsToCrouch = GetKey(KeyCode.C) || GetKey(KeyCode.LeftControl);

		if (wantsToCrouch && !isCrouching)
		{
			isCrouching = true;
			controller.height = crouchingHeight;
			controller.center = new Vector3(0f, crouchingHeight / 2f, 0f);
			targetCameraY = cameraCrouchingY;
		}
		else if (!wantsToCrouch && isCrouching)
		{
			// Check if there is an obstacle above the player before uncrouching
			if (CanUncrouch())
			{
				isCrouching = false;
				controller.height = standingHeight;
				controller.center = new Vector3(0f, standingHeight / 2f, 0f);
				targetCameraY = cameraStandingY;
			}
		}
	}

	private bool CanUncrouch()
	{
		// Raycast upwards to make sure there's no low ceiling
		Vector3 rayStart = transform.position + Vector3.up * crouchingHeight;
		float rayLength = standingHeight - crouchingHeight + 0.1f;
		
		return !Physics.Raycast(rayStart, Vector3.up, rayLength);
	}

	// Unified Input Helpers
	private bool GetKey(KeyCode key)
	{
#if ENABLE_INPUT_SYSTEM
		if (Keyboard.current == null) return false;
		switch (key)
		{
			case KeyCode.W: return Keyboard.current.wKey.isPressed;
			case KeyCode.A: return Keyboard.current.aKey.isPressed;
			case KeyCode.S: return Keyboard.current.sKey.isPressed;
			case KeyCode.D: return Keyboard.current.dKey.isPressed;
			case KeyCode.LeftShift: return Keyboard.current.leftShiftKey.isPressed;
			case KeyCode.Space: return Keyboard.current.spaceKey.isPressed;
			case KeyCode.C: return Keyboard.current.cKey.isPressed;
			case KeyCode.LeftControl: return Keyboard.current.leftCtrlKey.isPressed;
			default: return false;
		}
#else
		return Input.GetKey(key);
#endif
	}

	private bool GetKeyDown(KeyCode key)
	{
#if ENABLE_INPUT_SYSTEM
		if (Keyboard.current == null) return false;
		switch (key)
		{
			case KeyCode.Space: return Keyboard.current.spaceKey.wasPressedThisFrame;
			case KeyCode.C: return Keyboard.current.cKey.wasPressedThisFrame;
			case KeyCode.LeftControl: return Keyboard.current.leftCtrlKey.wasPressedThisFrame;
			default: return false;
		}
#else
		return Input.GetKeyDown(key);
#endif
	}
}
