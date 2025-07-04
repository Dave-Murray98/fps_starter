using UnityEngine;

/// <summary>
/// Handles ground-based movement physics and mechanics.
/// Refactored from PlayerGroundMovement to implement IMovementController interface.
/// Maintains all existing ground movement functionality while being part of the modular movement system.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class GroundMovementController : MonoBehaviour, IMovementController
{
    [Header("Ground Detection")]
    public LayerMask groundMask = 1;
    public float groundCheckDistance = 0.1f;
    public float slopeLimit = 45f;

    [Header("Movement")]
    [Tooltip("How quickly the player reaches target speed")]
    public float acceleration = 50f;
    [Tooltip("How quickly the player stops when no input")]
    public float deceleration = 50f;

    [Header("Debug")]
    public bool showGroundDebug = false;

    // Interface properties
    public MovementMode MovementMode => MovementMode.Ground;
    public bool IsGrounded { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsSpeedModified { get; private set; }
    public bool IsSecondaryActive { get; private set; }

    // Component references
    private PlayerController playerController;
    private PlayerData playerData;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;

    // Movement state
    private Vector2 movementInput;
    private bool isSprintingInput;
    private bool isCrouchingInput;
    private Vector3 groundNormal = Vector3.up;
    private GroundType currentGroundType = GroundType.Default;

    // Crouch system
    private float originalHeight;
    private float crouchHeight;
    private Vector3 originalCenter;
    private Vector3 crouchCenter;
    private bool isCrouching;

    // Properties for external access
    public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
    public GroundType CurrentGroundType => currentGroundType;
    public bool IsCrouching => isCrouching;
    public bool IsSprinting => IsSpeedModified && IsMoving && !isCrouching;

    public void Initialize(PlayerController controller)
    {
        playerController = controller;
        playerData = GameManager.Instance?.playerData;

        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        SetupRigidbody();
        SetupCrouchSystem();

        Debug.Log("[GroundMovementController] Initialized");
    }

    public Vector3 GetVelocity() => Velocity;

    public void HandleMovement(Vector2 moveInput, bool isSpeedModified)
    {
        movementInput = moveInput;
        isSprintingInput = isSpeedModified;
        IsSpeedModified = isSprintingInput && IsMoving && !isCrouching;
        IsMoving = moveInput.magnitude > 0.1f && IsGrounded;
    }

    public void HandlePrimaryAction()
    {
        // Jump logic
        if (IsGrounded && !isCrouching)
        {
            Jump();
        }
    }

    public void HandleSecondaryAction()
    {
        // Toggle crouch
        if (!isCrouching)
        {
            StartCrouch();
        }
        else
        {
            StopCrouch();
        }
    }

    public void HandleSecondaryActionReleased()
    {
        // For ground movement, we could implement hold-to-crouch here
        // For now, crouch is toggle-based, so no action needed on release
    }

    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        // Handle any ground movement specific state changes
        Debug.Log($"[GroundMovementController] State changed: {previousState} -> {newState}");
    }

    public void OnControllerActivated()
    {
        Debug.Log("[GroundMovementController] Controller activated");
    }

    public void OnControllerDeactivated()
    {
        Debug.Log("[GroundMovementController] Controller deactivated");
    }

    public void Cleanup()
    {
        Debug.Log("[GroundMovementController] Cleaning up");
    }

    private void SetupRigidbody()
    {
        if (rb == null) return;

        rb.freezeRotation = true;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }

    private void SetupCrouchSystem()
    {
        if (capsuleCollider == null) return;

        originalHeight = capsuleCollider.height;
        originalCenter = capsuleCollider.center;

        float crouchMultiplier = playerData?.crouchHeightMultiplier ?? 0.5f;
        crouchHeight = originalHeight * crouchMultiplier;
        crouchCenter = new Vector3(
            originalCenter.x,
            originalCenter.y - (originalHeight - crouchHeight) * 0.5f,
            originalCenter.z
        );
    }

    private void FixedUpdate()
    {
        if (playerController == null) return;

        CheckGrounded();
        ApplyMovement();
    }

    private void CheckGrounded()
    {
        if (capsuleCollider == null) return;

        float checkDistance = (capsuleCollider.height * 0.5f) + groundCheckDistance;
        Vector3 spherePosition = transform.position;

        bool wasGrounded = IsGrounded;

        if (Physics.SphereCast(spherePosition, capsuleCollider.radius * 0.9f, Vector3.down, out RaycastHit hit, checkDistance, groundMask))
        {
            IsGrounded = true;
            groundNormal = hit.normal;

            // Check if surface is too steep
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle > slopeLimit)
            {
                IsGrounded = false;
            }
            else
            {
                currentGroundType = DetermineGroundType(hit.collider);
            }
        }
        else
        {
            IsGrounded = false;
            groundNormal = Vector3.up;
            currentGroundType = GroundType.Default;
        }

        // Landing detection
        if (!wasGrounded && IsGrounded && rb.linearVelocity.y < -2f)
        {
            OnLanded();
        }
    }

    private GroundType DetermineGroundType(Collider groundCollider)
    {
        var groundTypeId = groundCollider.GetComponent<GroundTypeIdentifier>();
        return groundTypeId?.groundType ?? GroundType.Default;
    }

    private void ApplyMovement()
    {
        if (rb == null) return;

        float targetSpeed = GetCurrentMovementSpeed();

        // Calculate movement direction relative to camera
        Vector3 forward = playerController.playerCamera.GetCameraForward();
        Vector3 right = playerController.playerCamera.GetCameraRight();

        Vector3 targetVelocity = Vector3.zero;

        if (movementInput.magnitude > 0.1f)
        {
            // Calculate desired movement direction
            Vector3 moveDirection = (forward * movementInput.y + right * movementInput.x).normalized;

            if (IsGrounded)
            {
                // Project movement direction onto the slope
                moveDirection = Vector3.ProjectOnPlane(moveDirection, groundNormal).normalized;
            }

            targetVelocity = moveDirection * targetSpeed;
        }

        // Get current velocity but preserve Y component
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
        Vector3 targetHorizontalVelocity = new Vector3(targetVelocity.x, 0, targetVelocity.z);

        // Calculate horizontal force needed
        Vector3 horizontalVelocityDifference = targetHorizontalVelocity - currentHorizontalVelocity;

        // Use different acceleration based on whether we're accelerating or stopping
        float currentAcceleration = movementInput.magnitude > 0.1f ? acceleration : deceleration;

        // Apply only horizontal forces - let Unity's gravity handle Y
        Vector3 force = horizontalVelocityDifference * currentAcceleration;
        rb.AddForce(force, ForceMode.Acceleration);
    }

    private float GetCurrentMovementSpeed()
    {
        if (playerData == null) return 5f;

        if (isCrouching) return playerData.crouchSpeed;
        if (isSprintingInput && !isCrouching) return playerData.runSpeed;
        return playerData.walkSpeed;
    }

    private void Jump()
    {
        if (!IsGrounded || rb == null) return;

        float jumpHeight = playerData?.jumpHeight ?? 2f;
        float jumpForce = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        Debug.Log($"[GroundMovementController] Jump executed - Force: {jumpForce}");
    }

    private void StartCrouch()
    {
        if (isCrouching || capsuleCollider == null) return;

        isCrouching = true;
        IsSecondaryActive = true;
        capsuleCollider.height = crouchHeight;
        capsuleCollider.center = crouchCenter;

        Debug.Log("[GroundMovementController] Started crouching");
    }

    private void StopCrouch()
    {
        if (!isCrouching || capsuleCollider == null) return;

        if (CanStandUp())
        {
            isCrouching = false;
            IsSecondaryActive = false;
            capsuleCollider.height = originalHeight;
            capsuleCollider.center = originalCenter;

            Debug.Log("[GroundMovementController] Stopped crouching");
        }
        else
        {
            Debug.Log("[GroundMovementController] Cannot stand up - blocked by ceiling");
        }
    }

    private bool CanStandUp()
    {
        if (capsuleCollider == null) return false;

        Vector3 checkPosition = transform.position + Vector3.up * (originalHeight - crouchHeight);
        bool canStand = !Physics.CheckSphere(checkPosition, capsuleCollider.radius * 0.9f, groundMask);
        return canStand;
    }

    private void OnLanded()
    {
        Debug.Log($"[GroundMovementController] Player landed on {currentGroundType}");
    }

    private void OnDrawGizmos()
    {
        if (!showGroundDebug || capsuleCollider == null) return;

        float checkDistance = (capsuleCollider.height * 0.5f) + groundCheckDistance;
        Vector3 spherePosition = transform.position;

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(spherePosition + Vector3.down * checkDistance, capsuleCollider.radius * 0.9f);

        if (IsGrounded)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, groundNormal * 2f);
        }
    }
}