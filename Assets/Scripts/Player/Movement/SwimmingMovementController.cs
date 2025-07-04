using UnityEngine;

/// <summary>
/// Handles swimming movement physics and mechanics.
/// Implements IMovementController for seamless integration with the modular movement system.
/// Provides Subnautica-style swimming with 3D movement, buoyancy, and water resistance.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SwimmingMovementController : MonoBehaviour, IMovementController
{
    [Header("Swimming Physics")]
    [SerializeField] private float swimSpeed = 4f;
    [SerializeField] private float fastSwimSpeed = 6f;
    [SerializeField] private float verticalSwimSpeed = 3f;
    [SerializeField] private float swimAcceleration = 15f;
    [SerializeField] private float swimDeceleration = 10f;

    [Header("Buoyancy")]
    [SerializeField] private float buoyancyForce = 20f;
    [SerializeField] private float waterDrag = 5f;
    [SerializeField] private float waterAngularDrag = 5f;
    [SerializeField] private float surfaceBuoyancyMultiplier = 2f;
    [SerializeField] private float neutralBuoyancyDepth = 0.5f; // Depth where buoyancy balances gravity

    [Header("Diving")]
    [SerializeField] private float diveForce = 15f;
    [SerializeField] private float surfaceForce = 20f;
    [SerializeField] private float minDiveDepth = 0.5f;

    [Header("Movement Settings")]
    [SerializeField] private bool useGravityWhenSwimming = true; // Changed to true for better physics
    [SerializeField] private float maxSwimSpeed = 8f;
    [SerializeField] private bool enableRotationTowardsMovement = true;
    [SerializeField] private float rotationSpeed = 2f;
    [SerializeField] private float stopDrag = 8f; // Additional drag when not moving

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;

    // Interface properties
    public MovementMode MovementMode => MovementMode.Swimming;
    public bool IsGrounded => false; // Swimming never considers player "grounded"
    public bool IsMoving { get; private set; }
    public bool IsSpeedModified { get; private set; }
    public bool IsSecondaryActive { get; private set; } // Diving

    // Component references
    private PlayerController playerController;
    private PlayerData playerData;
    private Rigidbody rb;
    private PlayerWaterDetector waterDetector;

    // Swimming state
    private Vector2 movementInput;
    private bool isFastSwimming;
    private bool isDiving;
    private bool isSurfacing;
    private bool isSurfacingActive; // Tracks if surfacing is actively happening
    private float surfacingTimer;
    private float surfacingDuration = 0.5f; // How long surfacing force is applied
    private Vector3 swimDirection;
    private float originalDrag;
    private float originalAngularDrag;
    private bool originalUseGravity;

    // Water physics
    private float currentBuoyancy;
    private float waterDepth;
    private bool isNearSurface;

    public Vector3 GetVelocity() => rb != null ? rb.linearVelocity : Vector3.zero;

    public void Initialize(PlayerController controller)
    {
        playerController = controller;
        playerData = GameManager.Instance?.playerData;

        rb = GetComponent<Rigidbody>();
        waterDetector = GetComponent<PlayerWaterDetector>();

        if (rb == null)
        {
            Debug.LogError("[SwimmingMovementController] Rigidbody component not found!");
            return;
        }

        if (waterDetector == null)
        {
            Debug.LogError("[SwimmingMovementController] PlayerWaterDetector component not found!");
            return;
        }

        // Store original physics settings
        originalDrag = rb.linearDamping;
        originalAngularDrag = rb.angularDamping;
        originalUseGravity = rb.useGravity;

        DebugLog("Swimming movement controller initialized");
    }

    public void HandleMovement(Vector2 moveInput, bool isSpeedModified)
    {
        movementInput = moveInput;
        isFastSwimming = isSpeedModified;
        IsSpeedModified = isFastSwimming;
        IsMoving = moveInput.magnitude > 0.1f;

        // Only calculate swim direction if we have input
        if (IsMoving)
        {
            CalculateSwimDirection();
        }
        else
        {
            // Clear swim direction when no input to prevent residual movement
            swimDirection = Vector3.zero;
        }
    }

    public void HandlePrimaryAction()
    {
        // Surface action - swim upward with sustained force
        StartSurfacing();
        DebugLog("Surface action triggered - starting surfacing sequence");
    }

    public void HandleSecondaryAction()
    {
        // Dive action - toggle diving (but with proper release handling)
        if (!isDiving)
        {
            // Start diving
            isDiving = true;
            IsSecondaryActive = true;
            DebugLog("Started diving");
        }
        // Note: Stopping dive is handled in HandleSecondaryActionReleased
    }

    /// <summary>
    /// Handle secondary action release (stop diving)
    /// </summary>
    public void HandleSecondaryActionReleased()
    {
        if (isDiving)
        {
            isDiving = false;
            IsSecondaryActive = false;
            DebugLog("Stopped diving");
        }
    }

    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        DebugLog($"Swimming state changed: {previousState} -> {newState}");
    }

    public void OnControllerActivated()
    {
        DebugLog("Swimming controller activated");
        SetupSwimmingPhysics();

        // Reset swimming states to ensure clean activation
        isDiving = false;
        isSurfacingActive = false;
        surfacingTimer = 0f;
        IsSecondaryActive = false;

        // Clear any residual swimming direction
        swimDirection = Vector3.zero;
    }

    public void OnControllerDeactivated()
    {
        DebugLog("Swimming controller deactivated");
        RestoreOriginalPhysics();

        // Clear swimming inputs and states
        movementInput = Vector2.zero;
        isFastSwimming = false;
        isDiving = false;
        isSurfacingActive = false;
        surfacingTimer = 0f;

        // Reset movement states
        IsMoving = false;
        IsSpeedModified = false;
        IsSecondaryActive = false;

        // Clear swim direction
        swimDirection = Vector3.zero;
    }

    public void Cleanup()
    {
        RestoreOriginalPhysics();
        DebugLog("Swimming controller cleaned up");
    }

    private void SetupSwimmingPhysics()
    {
        if (rb == null) return;

        // Apply swimming physics settings
        rb.linearDamping = waterDrag;
        rb.angularDamping = waterAngularDrag;
        rb.useGravity = useGravityWhenSwimming;

        DebugLog("Swimming physics applied");
    }

    private void RestoreOriginalPhysics()
    {
        if (rb == null) return;

        // Restore original physics settings
        rb.linearDamping = originalDrag;
        rb.angularDamping = originalAngularDrag;
        rb.useGravity = originalUseGravity;

        DebugLog("Original physics restored");
    }

    private void CalculateSwimDirection()
    {
        if (playerController?.playerCamera == null) return;

        // Only calculate direction if we have movement input
        if (movementInput.magnitude < 0.1f && !isDiving && !isSurfacingActive)
        {
            swimDirection = Vector3.zero;
            return;
        }

        Vector3 forward = playerController.playerCamera.GetCameraForward();
        Vector3 right = playerController.playerCamera.GetCameraRight();

        // Calculate horizontal movement direction only if we have input
        Vector3 horizontalDirection = Vector3.zero;
        if (movementInput.magnitude > 0.1f)
        {
            horizontalDirection = (forward * movementInput.y + right * movementInput.x).normalized;
        }

        // Calculate vertical component based on input state
        Vector3 verticalDirection = Vector3.zero;

        if (isDiving)
        {
            verticalDirection = Vector3.down;
        }
        // Note: Surfacing is now handled separately in ApplySurfacingForce()

        // Combine horizontal and vertical movement
        swimDirection = horizontalDirection + verticalDirection;

        // Normalize if we have both horizontal and vertical input
        if (swimDirection.magnitude > 1f)
        {
            swimDirection.Normalize();
        }
    }

    private void FixedUpdate()
    {
        if (playerController == null || rb == null) return;

        UpdateWaterPhysics();
        UpdateSurfacingTimer();
        ApplySwimmingMovement();
        ApplyBuoyancy();
        ApplySurfacingForce();

        if (enableRotationTowardsMovement)
        {
            ApplyRotationTowardsMovement();
        }
    }

    private void UpdateWaterPhysics()
    {
        if (waterDetector == null) return;

        waterDepth = waterDetector.FeetDepth;
        isNearSurface = waterDepth < minDiveDepth;

        // Adjust buoyancy based on proximity to surface
        currentBuoyancy = isNearSurface ? buoyancyForce * surfaceBuoyancyMultiplier : buoyancyForce;
    }

    private void ApplySwimmingMovement()
    {
        Vector3 targetVelocity = Vector3.zero;

        // Only apply movement if we have a swim direction
        if (swimDirection.magnitude > 0.01f)
        {
            // Calculate target velocity when moving
            float targetSpeed = GetCurrentSwimSpeed();
            targetVelocity = swimDirection * targetSpeed;
        }

        // Get current velocity
        Vector3 currentVelocity = rb.linearVelocity;

        // Calculate force needed to reach target velocity
        Vector3 velocityDifference = targetVelocity - currentVelocity;

        // Use different acceleration for speeding up vs slowing down
        float acceleration = (swimDirection.magnitude > 0.01f) ? swimAcceleration : swimDeceleration;

        Vector3 force = velocityDifference * acceleration;

        // Limit maximum force to prevent excessive acceleration
        force = Vector3.ClampMagnitude(force, maxSwimSpeed * acceleration);

        rb.AddForce(force, ForceMode.Acceleration);

        // Apply extra strong drag when not moving to prevent any residual movement
        if (swimDirection.magnitude < 0.01f)
        {
            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
            if (horizontalVelocity.magnitude > 0.1f)
            {
                // Much stronger drag when completely stopped
                rb.AddForce(-horizontalVelocity * (stopDrag * 2f), ForceMode.Acceleration);
            }
        }
    }

    private void ApplyBuoyancy()
    {
        if (!waterDetector.IsInWater) return;

        // Calculate buoyancy based on depth - deeper = more buoyancy needed to counteract gravity
        float adjustedBuoyancy = CalculateDepthAdjustedBuoyancy();

        Vector3 buoyancyVector = Vector3.up * adjustedBuoyancy;

        // Reduce buoyancy if player is actively diving
        if (isDiving)
        {
            buoyancyVector *= 0.1f; // Significantly reduce when diving
        }

        // Reduce buoyancy if player is actively surfacing (let surfacing force take priority)
        if (isSurfacingActive)
        {
            buoyancyVector *= 0.2f;
        }

        rb.AddForce(buoyancyVector, ForceMode.Acceleration);

        // Apply additional diving force when actively diving
        if (isDiving && waterDepth > minDiveDepth)
        {
            rb.AddForce(Vector3.down * diveForce, ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// Calculate buoyancy that balances gravity based on water depth
    /// </summary>
    private float CalculateDepthAdjustedBuoyancy()
    {
        // At surface: minimal buoyancy (let gravity work)
        // At depth: full buoyancy to counteract gravity and provide neutral buoyancy

        if (isNearSurface)
        {
            // Very close to surface - minimal buoyancy to prevent floating above water
            float surfaceDepth = Mathf.Max(0.01f, waterDepth);
            float surfaceBuoyancyRatio = Mathf.Clamp01(surfaceDepth / neutralBuoyancyDepth);
            return buoyancyForce * surfaceBuoyancyRatio * 0.3f; // Much reduced at surface
        }
        else
        {
            // Underwater - provide enough buoyancy to counteract gravity for neutral buoyancy
            float gravityMagnitude = Mathf.Abs(Physics.gravity.y);
            float neutralBuoyancy = gravityMagnitude * rb.mass;

            // Add some extra buoyancy based on settings, but not too much
            return neutralBuoyancy + (buoyancyForce * 0.5f);
        }
    }

    private void ApplyRotationTowardsMovement()
    {
        if (!IsMoving || swimDirection.magnitude < 0.1f) return;

        // Calculate target rotation based on movement direction
        Quaternion targetRotation = Quaternion.LookRotation(swimDirection);

        // Smoothly rotate towards target
        float rotationSpeedMultiplier = isFastSwimming ? rotationSpeed * 1.5f : rotationSpeed;
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeedMultiplier * Time.fixedDeltaTime));
    }

    private float GetCurrentSwimSpeed()
    {
        float baseSpeed = isFastSwimming ? fastSwimSpeed : swimSpeed;

        // Apply vertical speed modifier for diving/surfacing
        if (Mathf.Abs(swimDirection.y) > 0.1f)
        {
            float verticalSpeedRatio = verticalSwimSpeed / swimSpeed;
            baseSpeed *= verticalSpeedRatio;
        }

        // Apply surface penalty - swimming at surface is slightly slower
        if (isNearSurface && !isDiving)
        {
            baseSpeed *= 0.8f;
        }

        return baseSpeed;
    }

    /// <summary>
    /// Starts the surfacing sequence with sustained upward force
    /// </summary>
    private void StartSurfacing()
    {
        isSurfacingActive = true;
        surfacingTimer = surfacingDuration;

        // Stop diving when surfacing
        if (isDiving)
        {
            isDiving = false;
            IsSecondaryActive = false;
        }

        DebugLog($"Surfacing started - will apply force for {surfacingDuration} seconds");
    }

    /// <summary>
    /// Updates the surfacing timer
    /// </summary>
    private void UpdateSurfacingTimer()
    {
        if (isSurfacingActive)
        {
            surfacingTimer -= Time.fixedDeltaTime;
            if (surfacingTimer <= 0f)
            {
                isSurfacingActive = false;
                DebugLog("Surfacing sequence completed");
            }
        }
    }

    /// <summary>
    /// Applies sustained surfacing force when surfacing is active
    /// </summary>
    private void ApplySurfacingForce()
    {
        if (!isSurfacingActive || !waterDetector.IsInWater) return;

        // Apply strong upward force during surfacing
        float surfaceForceToApply = surfaceForce;

        // Reduce force as we get closer to surface
        if (isNearSurface)
        {
            surfaceForceToApply *= 0.5f;
        }

        rb.AddForce(Vector3.up * surfaceForceToApply, ForceMode.Acceleration);

        // Reduce horizontal drag during surfacing for more responsive movement
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (horizontalVelocity.magnitude > 0.1f)
        {
            rb.AddForce(-horizontalVelocity * (waterDrag * 0.3f), ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// Force surface - makes player quickly swim to surface
    /// </summary>
    public void ForceSurface()
    {
        if (rb == null || !waterDetector.IsInWater) return;

        // Use the new surfacing system
        StartSurfacing();

        // Also apply immediate upward velocity for instant response
        Vector3 currentVelocity = rb.linearVelocity;
        currentVelocity.y = Mathf.Max(currentVelocity.y, surfaceForce * 0.1f);
        rb.linearVelocity = currentVelocity;

        DebugLog("Force surface applied with immediate velocity boost");
    }

    /// <summary>
    /// Check if player can exit water (near surface with low vertical velocity)
    /// </summary>
    public bool CanExitWater()
    {
        if (waterDetector == null) return false;

        bool nearSurface = waterDetector.FeetDepth < 0.2f;
        bool slowVerticalMovement = Mathf.Abs(rb.linearVelocity.y) < 2f;

        return nearSurface && slowVerticalMovement;
    }

    /// <summary>
    /// Gets swimming state information for debugging
    /// </summary>
    public string GetSwimmingStateInfo()
    {
        return $"Swimming - Speed: {GetCurrentSwimSpeed():F1}m/s, " +
               $"FastSwim: {isFastSwimming}, Diving: {isDiving}, Surfacing: {isSurfacingActive}, " +
               $"WaterDepth: {waterDepth:F2}m, NearSurface: {isNearSurface}";
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SwimmingMovementController] {message}");
        }
    }

    #region Gizmos and Debug

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        // Draw swim direction
        if (swimDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, swimDirection * 2f);
            Gizmos.DrawWireSphere(transform.position + swimDirection * 2f, 0.1f);
        }

        // Draw velocity
        if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
        }

        // Draw surfacing force visualization
        if (waterDetector != null && waterDetector.IsInWater)
        {
            if (isSurfacingActive)
            {
                Gizmos.color = Color.yellow;
                Vector3 surfacingVisual = Vector3.up * (surfaceForce * 0.1f);
                Gizmos.DrawRay(transform.position, surfacingVisual);
                Gizmos.DrawWireSphere(transform.position + surfacingVisual, 0.15f);
            }
            else
            {
                Gizmos.color = isDiving ? Color.red : Color.blue;
                Vector3 buoyancyVisual = Vector3.up * (currentBuoyancy * 0.1f);
                Gizmos.DrawRay(transform.position, buoyancyVisual);
            }
        }

        // Draw water depth indicator
        if (waterDetector != null)
        {
            Gizmos.color = isNearSurface ? Color.green : Color.blue;
            Vector3 depthStart = transform.position;
            Vector3 depthEnd = depthStart + Vector3.down * waterDepth;
            Gizmos.DrawLine(depthStart, depthEnd);
            Gizmos.DrawWireSphere(depthEnd, 0.05f);
        }

#if UNITY_EDITOR
        // Draw swimming state info
        string stateInfo = GetSwimmingStateInfo();
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, stateInfo);
#endif
    }

    #endregion

    #region Editor Helpers

#if UNITY_EDITOR
    [ContextMenu("Force Surface")]
    private void Editor_ForceSurface()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Force surface only works in play mode");
            return;
        }
        ForceSurface();
    }

    [ContextMenu("Toggle Diving")]
    private void Editor_ToggleDiving()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Toggle diving only works in play mode");
            return;
        }
        HandleSecondaryAction();
    }

    [ContextMenu("Log Swimming State")]
    private void Editor_LogSwimmingState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Swimming state logging only works in play mode");
            return;
        }
        Debug.Log(GetSwimmingStateInfo());
    }
#endif

    #endregion
}