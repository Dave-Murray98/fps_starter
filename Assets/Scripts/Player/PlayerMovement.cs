using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
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

        // Simple rigidbody setup
        rb.freezeRotation = true;
        rb.useGravity = true; // Let Unity handle gravity - this is key!
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        // Store original collider dimensions
        originalHeight = capsuleCollider.height;
        originalCenter = capsuleCollider.center;
        crouchHeight = originalHeight * 0.5f;
        crouchCenter = new Vector3(originalCenter.x, originalCenter.y - (originalHeight - crouchHeight) * 0.5f, originalCenter.z);
    }

    private void FixedUpdate()
    {
        if (controller == null) return;

        CheckGrounded();
        ApplyMovement();
        // No custom gravity - let Unity handle it
        // No special ground snapping - let physics work naturally
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
            else
            {
                currentGroundType = DetermineGroundType(hit.collider);
            }
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
            OnLanded();
        }
    }

    private GroundType DetermineGroundType(Collider groundCollider)
    {
        var groundTypeId = groundCollider.GetComponent<GroundTypeIdentifier>();
        if (groundTypeId != null)
        {
            return groundTypeId.groundType;
        }
        return GroundType.Default;
    }

    private void ApplyMovement()
    {
        float targetSpeed = GetCurrentMovementSpeed();

        // Calculate movement direction relative to camera
        Vector3 forward = controller.playerCamera.GetCameraForward();
        Vector3 right = controller.playerCamera.GetCameraRight();

        Vector3 targetVelocity = Vector3.zero;

        if (movementInput.magnitude > 0.1f)
        {
            // Calculate desired movement direction
            Vector3 moveDirection = (forward * movementInput.y + right * movementInput.x).normalized;

            if (isGrounded)
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
        if (isSprinting) return playerData.runSpeed;
        return playerData.walkSpeed;
    }

    public void Jump()
    {
        if (!isGrounded) return;

        float jumpForce = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * (playerData?.jumpHeight ?? 2f));
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    public void StartCrouch()
    {
        if (isCrouching) return;

        isCrouching = true;
        capsuleCollider.height = crouchHeight;
        capsuleCollider.center = crouchCenter;
        Debug.Log("Started crouching");
    }

    public void StopCrouch()
    {
        if (!isCrouching) return;

        if (CanStandUp())
        {
            isCrouching = false;
            capsuleCollider.height = originalHeight;
            capsuleCollider.center = originalCenter;
            Debug.Log("Stopped crouching");
        }
    }

    private bool CanStandUp()
    {
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