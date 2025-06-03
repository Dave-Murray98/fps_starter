using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Ground Detection")]
    public LayerMask groundMask = 1;
    public float groundCheckDistance = 0.1f;
    public float slopeLimit = 45f;

    [Header("Debug")]
    public bool showGroundDebug = false;

    // Private variables
    private PlayerController controller;
    private PlayerData playerData;
    [SerializeField] private Rigidbody rb;
    private CapsuleCollider capsuleCollider;

    // Movement state
    private Vector2 movementInput;
    private bool isSprinting;
    private bool isCrouching;
    private bool isGrounded;
    private GroundType currentGroundType = GroundType.Default;
    private Vector3 groundNormal = Vector3.up;

    // Crouch system
    private float originalHeight;
    private float crouchHeight;
    private Vector3 originalCenter;
    private Vector3 crouchCenter;

    // Properties
    public bool IsGrounded => isGrounded;
    public bool IsMoving => movementInput.magnitude > 0.1f && isGrounded;
    public bool IsSprinting => isSprinting && IsMoving && !isCrouching;
    public bool IsCrouching => isCrouching;
    public Vector3 Velocity => rb.linearVelocity;
    public GroundType CurrentGroundType => currentGroundType;

    public void Initialize(PlayerController playerController)
    {
        controller = playerController;
        playerData = GameManager.Instance.playerData;

        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        // Configure rigidbody
        rb.freezeRotation = true;
        rb.useGravity = false; // We'll handle gravity manually

        // Store original collider dimensions
        originalHeight = capsuleCollider.height;
        originalCenter = capsuleCollider.center;
        crouchHeight = originalHeight * 0.5f;
        crouchCenter = new Vector3(originalCenter.x, originalCenter.y - (originalHeight - crouchHeight) * 0.5f, originalCenter.z);

        //        Debug.Log("PlayerMovement initialized");
    }

    private void FixedUpdate()
    {
        if (controller == null) return;

        CheckGrounded();
        ApplyMovement();
        ApplyGravity();
    }

    private void CheckGrounded()
    {
        float checkDistance = (capsuleCollider.height * 0.5f) + groundCheckDistance;
        Vector3 spherePosition = transform.position;

        RaycastHit hit;
        bool wasGrounded = isGrounded;

        if (Physics.SphereCast(spherePosition, capsuleCollider.radius * 0.9f, Vector3.down, out hit, checkDistance, groundMask))
        {
            isGrounded = true;
            groundNormal = hit.normal;

            // Check if surface is too steep
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            if (slopeAngle > slopeLimit)
            {
                isGrounded = false;
            }

            // Determine ground type
            currentGroundType = DetermineGroundType(hit.collider);
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
            currentGroundType = GroundType.Default;
        }

        // Landing detection
        if (!wasGrounded && isGrounded && rb.linearVelocity.y < -2f)
        {
            // Trigger landing event/sound
            OnLanded();
        }
    }

    private GroundType DetermineGroundType(Collider groundCollider)
    {
        // Check for GroundTypeIdentifier component first
        var groundTypeId = groundCollider.GetComponent<GroundTypeIdentifier>();
        if (groundTypeId != null)
        {
            return groundTypeId.groundType;
        }

        // Fallback to tag-based detection
        switch (groundCollider.tag)
        {
            case "Grass": return GroundType.Grass;
            case "Stone": return GroundType.Stone;
            case "Metal": return GroundType.Metal;
            case "Wood": return GroundType.Wood;
            case "Water": return GroundType.Water;
            default: return GroundType.Default;
        }
    }

    private void ApplyMovement()
    {
        // Get movement speed based on state
        float targetSpeed = GetCurrentMovementSpeed();

        // Calculate movement direction relative to camera
        Vector3 forward = controller.playerCamera.GetCameraForward();
        Vector3 right = controller.playerCamera.GetCameraRight();

        Vector3 targetVelocity = Vector3.zero;

        if (movementInput.magnitude > 0.1f)
        {
            // Calculate desired movement direction
            Vector3 desiredMoveDirection = (forward * movementInput.y + right * movementInput.x).normalized;

            // Project movement onto slope
            if (isGrounded)
            {
                desiredMoveDirection = Vector3.ProjectOnPlane(desiredMoveDirection, groundNormal).normalized;
            }

            // Calculate target velocity
            targetVelocity = desiredMoveDirection * targetSpeed;
        }

        // Apply movement or stopping force
        Vector3 currentHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        Vector3 velocityChange = targetVelocity - currentHorizontalVelocity;

        if (movementInput.magnitude < 0.1f && isGrounded)
        {
            // Apply stronger stopping force when no input and grounded
            float stopForce = playerData?.stopForce ?? 10f;
            velocityChange = -currentHorizontalVelocity * stopForce;
            rb.AddForce(velocityChange, ForceMode.Acceleration);
        }
        else
        {
            // Normal movement
            rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }
    }

    private float GetCurrentMovementSpeed()
    {
        if (playerData == null) return 5f;

        if (isCrouching) return playerData.crouchSpeed;
        if (isSprinting) return playerData.runSpeed;
        return playerData.walkSpeed;
    }

    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            float gravity = playerData?.gravity ?? -9.81f;
            rb.AddForce(Vector3.up * gravity, ForceMode.Acceleration);
        }
    }

    public void Jump()
    {
        if (!isGrounded) return;

        float jumpForce = Mathf.Sqrt(2f * Mathf.Abs(playerData?.gravity ?? 9.81f) * (playerData?.jumpHeight ?? 2f));
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        //Debug.Log("Player jumped!");
    }

    public void StartCrouch()
    {
        if (isCrouching) return;

        isCrouching = true;

        // Adjust collider
        capsuleCollider.height = crouchHeight;
        capsuleCollider.center = crouchCenter;

        Debug.Log("Started crouching");
    }

    public void StopCrouch()
    {
        if (!isCrouching) return;

        // Check if there's room to stand up
        if (CanStandUp())
        {
            isCrouching = false;

            // Restore collider
            capsuleCollider.height = originalHeight;
            capsuleCollider.center = originalCenter;

            Debug.Log("Stopped crouching");
        }
    }

    private bool CanStandUp()
    {
        // Check position above the player's head when standing
        Vector3 checkPosition = transform.position + Vector3.up * (originalHeight - crouchHeight);
        bool canStand = !Physics.CheckSphere(checkPosition, capsuleCollider.radius * 0.9f, groundMask);

        Debug.Log($"CanStandUp check - Position: {checkPosition}, Can stand: {canStand}");
        return canStand;
    }

    private void OnLanded()
    {
        //Debug.Log($"Player landed on {currentGroundType}");
    }

    // Input methods
    public void SetMovementInput(Vector2 input) => movementInput = input;
    public void SetSprinting(bool running) => isSprinting = running;

    // State change notification
    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        // Handle any movement-specific state change logic here
    }

    private void OnDrawGizmos()
    {
        if (!showGroundDebug) return;

        if (capsuleCollider != null)
        {
            float checkDistance = (capsuleCollider.height * 0.5f) + groundCheckDistance;
            Vector3 spherePosition = transform.position;

            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(spherePosition + Vector3.down * checkDistance, capsuleCollider.radius * 0.9f);

            if (isGrounded)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, groundNormal * 2f);
            }
        }
    }
}