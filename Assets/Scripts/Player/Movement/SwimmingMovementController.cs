using UnityEngine;

/// <summary>
/// Handles swimming movement physics and mechanics.
/// FIXED: Proper movement reset, physics restoration, and input handling
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
    [SerializeField] private float neutralBuoyancyDepth = 0.5f;

    [Header("Diving")]
    [SerializeField] private float diveForce = 8f;
    [SerializeField] private float surfaceForce = 30f;
    [SerializeField] private float minDiveDepth = 0.5f;
    [SerializeField] private bool useGravityForDiving = false; // NEW: Option to disable gravity when diving
    [SerializeField] private float diveGravityMultiplier = 0.3f; // NEW: Reduce gravity effect when diving

    [Header("Movement Settings")]
    [SerializeField] private bool useGravityWhenSwimming = true;
    [SerializeField] private float maxSwimSpeed = 8f;
    [SerializeField] private bool enableRotationTowardsMovement = true;
    [SerializeField] private float rotationSpeed = 2f;
    [SerializeField] private float stopDrag = 12f; // Increased for better stopping

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;

    // Interface properties
    public MovementMode MovementMode => MovementMode.Swimming;
    public bool IsGrounded => false;
    public bool IsMoving { get; private set; }
    public bool IsSpeedModified { get; private set; }
    public bool IsSecondaryActive { get; private set; }

    // Component references
    private PlayerController playerController;
    private PlayerData playerData;
    private Rigidbody rb;
    private PlayerWaterDetector waterDetector;

    // Swimming state
    private Vector2 movementInput;
    private bool isFastSwimming;
    private bool isDiving;
    private bool isSurfacingActive;
    private Vector3 swimDirection;

    // Physics state storage
    private float originalDrag;
    private float originalAngularDrag;
    private bool originalUseGravity;
    private bool physicsStateStored = false;

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

        // Store original physics settings ONCE during initialization
        if (!physicsStateStored)
        {
            originalDrag = rb.linearDamping;
            originalAngularDrag = rb.angularDamping;
            originalUseGravity = rb.useGravity;
            physicsStateStored = true;
            DebugLog($"Stored original physics: Drag={originalDrag}, AngularDrag={originalAngularDrag}, Gravity={originalUseGravity}");
        }

        DebugLog("Swimming movement controller initialized");
    }

    public void HandleMovement(Vector2 moveInput, bool isSpeedModified)
    {
        movementInput = moveInput;
        isFastSwimming = isSpeedModified;
        IsSpeedModified = isFastSwimming;
        IsMoving = moveInput.magnitude > 0.1f;

        // FIXED: Always recalculate swim direction, including when input is zero
        CalculateSwimDirection();
    }

    /// <summary>
    /// FIXED: Enhanced surfacing that works while holding the button
    /// </summary>
    public void HandlePrimaryAction()
    {
        // FIXED: Start continuous surfacing instead of timed burst
        if (!isSurfacingActive)
        {
            StartContinuousSurfacing();
            DebugLog("Surface action triggered - starting continuous surfacing");
        }
    }

    /// <summary>
    /// FIXED: Starts continuous surfacing (no timer limit)
    /// </summary>
    private void StartContinuousSurfacing()
    {
        isSurfacingActive = true;

        // Stop diving when surfacing
        if (isDiving)
        {
            isDiving = false;
            IsSecondaryActive = false;
        }

        DebugLog("Continuous surfacing started");
    }


    /// <summary>
    /// NEW: Handle primary action release to stop surfacing
    /// </summary>
    public void HandlePrimaryActionReleased()
    {
        // FIXED: Stop surfacing when button is released
        if (isSurfacingActive)
        {
            StopContinuousSurfacing();
            DebugLog("Surface action released - stopping surfacing");
        }
    }

    /// <summary>
    /// FIXED: Stops continuous surfacing
    /// </summary>
    private void StopContinuousSurfacing()
    {
        isSurfacingActive = false;
        DebugLog("Continuous surfacing stopped");
    }


    public void HandleSecondaryAction()
    {
        if (!isDiving)
        {
            isDiving = true;
            IsSecondaryActive = true;
            DebugLog("Started diving");
        }
    }

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

        // FIXED: Complete state reset on activation
        ResetSwimmingState();
    }

    public void OnControllerDeactivated()
    {
        DebugLog("Swimming controller deactivated");

        // FIXED: Comprehensive cleanup when deactivating
        CompleteSwimmingCleanup();
    }

    public void Cleanup()
    {
        RestoreOriginalPhysics();
        DebugLog("Swimming controller cleaned up");
    }

    /// <summary>
    /// FIXED: Comprehensive state reset for clean activation
    /// </summary>
    private void ResetSwimmingState()
    {
        // Reset all input and movement state
        movementInput = Vector2.zero;
        isFastSwimming = false;
        isDiving = false;
        isSurfacingActive = false;
        swimDirection = Vector3.zero;

        // Reset interface properties
        IsMoving = false;
        IsSpeedModified = false;
        IsSecondaryActive = false;

        DebugLog("Swimming state completely reset");
    }

    /// <summary>
    /// FIXED: Thorough cleanup including physics and velocity reset
    /// </summary>
    private void CompleteSwimmingCleanup()
    {
        // Stop any residual movement immediately
        if (rb != null)
        {
            // FIXED: Zero out horizontal velocity to prevent drift
            Vector3 currentVelocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
            DebugLog($"Cleared horizontal velocity: {currentVelocity} -> {rb.linearVelocity}");
        }

        // Restore physics before clearing state
        RestoreOriginalPhysics();

        // Clear all state
        ResetSwimmingState();

        DebugLog("Complete swimming cleanup performed");
    }

    private void SetupSwimmingPhysics()
    {
        if (rb == null) return;

        rb.linearDamping = waterDrag;
        rb.angularDamping = waterAngularDrag;
        rb.useGravity = useGravityWhenSwimming;

        DebugLog($"Swimming physics applied: Drag={waterDrag}, AngularDrag={waterAngularDrag}, Gravity={useGravityWhenSwimming}");
    }

    private void RestoreOriginalPhysics()
    {
        if (rb == null || !physicsStateStored) return;

        rb.linearDamping = originalDrag;
        rb.angularDamping = originalAngularDrag;
        rb.useGravity = originalUseGravity;

        DebugLog($"Original physics restored: Drag={originalDrag}, AngularDrag={originalAngularDrag}, Gravity={originalUseGravity}");
    }

    /// <summary>
    /// FIXED: Always calculate direction, including zero input for proper stopping
    /// </summary>
    private void CalculateSwimDirection()
    {
        if (playerController?.playerCamera == null)
        {
            swimDirection = Vector3.zero;
            return;
        }

        Vector3 forward = playerController.playerCamera.GetCameraForward();
        Vector3 right = playerController.playerCamera.GetCameraRight();

        // FIXED: Always calculate direction, even for zero input
        Vector3 horizontalDirection = Vector3.zero;
        if (movementInput.magnitude > 0.1f)
        {
            horizontalDirection = (forward * movementInput.y + right * movementInput.x).normalized;
        }

        // Calculate vertical component
        Vector3 verticalDirection = Vector3.zero;
        if (isDiving)
        {
            verticalDirection = Vector3.down;
        }

        // Combine directions
        swimDirection = horizontalDirection + verticalDirection;

        // Normalize if needed
        if (swimDirection.magnitude > 1f)
        {
            swimDirection.Normalize();
        }

        // FIXED: Explicit zero when no input
        if (movementInput.magnitude < 0.1f && !isDiving && !isSurfacingActive)
        {
            swimDirection = Vector3.zero;
        }
    }

    private void FixedUpdate()
    {
        if (playerController == null || rb == null) return;

        UpdateWaterPhysics();
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
        currentBuoyancy = isNearSurface ? buoyancyForce * surfaceBuoyancyMultiplier : buoyancyForce;
    }

    /// <summary>
    /// FIXED: Improved movement application with better stopping behavior
    /// </summary>
    private void ApplySwimmingMovement()
    {
        Vector3 targetVelocity = Vector3.zero;

        // Calculate target velocity based on swim direction
        if (swimDirection.magnitude > 0.01f)
        {
            float targetSpeed = GetCurrentSwimSpeed();
            targetVelocity = swimDirection * targetSpeed;
        }

        // Get current velocity
        Vector3 currentVelocity = rb.linearVelocity;

        // Calculate force needed
        Vector3 velocityDifference = targetVelocity - currentVelocity;

        // Use different acceleration for movement vs stopping
        float acceleration = (swimDirection.magnitude > 0.01f) ? swimAcceleration : swimDeceleration;

        Vector3 force = velocityDifference * acceleration;
        force = Vector3.ClampMagnitude(force, maxSwimSpeed * acceleration);

        rb.AddForce(force, ForceMode.Acceleration);

        // FIXED: Enhanced stopping force when no input
        if (swimDirection.magnitude < 0.01f)
        {
            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
            if (horizontalVelocity.magnitude > 0.1f)
            {
                // Stronger stopping force
                Vector3 stopForce = -horizontalVelocity * stopDrag;
                rb.AddForce(stopForce, ForceMode.Acceleration);
            }
        }
    }

    /// <summary>
    /// FIXED: Balanced buoyancy application that accounts for diving vs surfacing
    /// </summary>
    private void ApplyBuoyancy()
    {
        if (!waterDetector.IsInWater) return;

        float adjustedBuoyancy = CalculateDepthAdjustedBuoyancy();
        Vector3 buoyancyVector = Vector3.up * adjustedBuoyancy;

        // FIXED: Different buoyancy handling for diving vs surfacing
        if (isDiving)
        {
            // OPTION 1: Stronger buoyancy reduction for diving
            buoyancyVector *= 0.05f; // Further reduced from 0.1f

            // OPTION 2: Counter gravity when diving for more control
            if (!useGravityForDiving)
            {
                // Add upward force to counteract gravity partially
                float gravityCounter = Mathf.Abs(Physics.gravity.y) * rb.mass * (1f - diveGravityMultiplier);
                buoyancyVector += Vector3.up * gravityCounter;
            }
        }
        else if (isSurfacingActive)
        {
            // FIXED: Slightly more buoyancy when surfacing
            buoyancyVector *= 0.3f; // Increased from 0.2f
        }

        rb.AddForce(buoyancyVector, ForceMode.Acceleration);

        // FIXED: Enhanced diving force application with gravity consideration
        if (isDiving && waterDepth > minDiveDepth)
        {
            ApplyDivingForce();
        }
    }

    /// <summary>
    /// NEW: Separate diving force application for better control
    /// </summary>
    private void ApplyDivingForce()
    {
        // Calculate diving force that accounts for existing gravity
        float effectiveDiveForce = diveForce;

        // OPTION 1: Reduce dive force to account for gravity already pulling down
        if (useGravityForDiving)
        {
            // Since gravity is already pulling down, we need less additional force
            effectiveDiveForce = diveForce * 0.6f;
        }

        // OPTION 2: Apply consistent diving force regardless of gravity
        // This gives more predictable diving speed
        Vector3 diveVector = Vector3.down * effectiveDiveForce;

        // FIXED: Apply diving force
        rb.AddForce(diveVector, ForceMode.Acceleration);

        DebugLog($"Applied diving force: {effectiveDiveForce:F1}, UseGravity: {useGravityForDiving}");
    }

    /// <summary>
    /// FIXED: Enhanced surfacing force with gravity compensation
    /// </summary>
    private void ApplySurfacingForce()
    {
        if (!isSurfacingActive || !waterDetector.IsInWater) return;

        // FIXED: Calculate surface force that properly counters gravity
        float surfaceForceToApply = surfaceForce;

        // Add extra force to counter gravity for more responsive surfacing
        float gravityCompensation = Mathf.Abs(Physics.gravity.y) * rb.mass;
        surfaceForceToApply += gravityCompensation * 0.5f; // 50% gravity compensation

        // Reduce force as we get closer to surface for smoother control
        if (isNearSurface)
        {
            surfaceForceToApply *= 0.8f; // Slightly reduced from 0.7f
        }

        rb.AddForce(Vector3.up * surfaceForceToApply, ForceMode.Acceleration);

        // Reduce horizontal drag during surfacing for more responsive movement
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (horizontalVelocity.magnitude > 0.1f)
        {
            rb.AddForce(-horizontalVelocity * (waterDrag * 0.3f), ForceMode.Acceleration);
        }

        DebugLog($"Applied surface force: {surfaceForceToApply:F1}, GravityComp: {gravityCompensation * 0.5f:F1}");
    }

    /// <summary>
    /// ALTERNATIVE: Gravity override system for balanced diving/surfacing
    /// </summary>
    private void ApplyGravityOverride()
    {
        if (!waterDetector.IsInWater) return;

        // Override gravity when diving or surfacing for more control
        if (isDiving || isSurfacingActive)
        {
            // Cancel out Unity's gravity and apply our own controlled vertical force
            Vector3 gravityCancel = -Physics.gravity * rb.mass;
            rb.AddForce(gravityCancel, ForceMode.Acceleration);

            if (isDiving)
            {
                // Apply controlled downward force
                rb.AddForce(Vector3.down * diveForce, ForceMode.Acceleration);
            }
            else if (isSurfacingActive)
            {
                // Apply controlled upward force
                rb.AddForce(Vector3.up * surfaceForce, ForceMode.Acceleration);
            }
        }
    }

    private float CalculateDepthAdjustedBuoyancy()
    {
        if (isNearSurface)
        {
            float surfaceDepth = Mathf.Max(0.01f, waterDepth);
            float surfaceBuoyancyRatio = Mathf.Clamp01(surfaceDepth / neutralBuoyancyDepth);
            return buoyancyForce * surfaceBuoyancyRatio * 0.3f;
        }
        else
        {
            float gravityMagnitude = Mathf.Abs(Physics.gravity.y);
            float neutralBuoyancy = gravityMagnitude * rb.mass;
            return neutralBuoyancy + (buoyancyForce * 0.5f);
        }
    }

    private void ApplyRotationTowardsMovement()
    {
        if (!IsMoving || swimDirection.magnitude < 0.1f) return;

        Quaternion targetRotation = Quaternion.LookRotation(swimDirection);
        float rotationSpeedMultiplier = isFastSwimming ? rotationSpeed * 1.5f : rotationSpeed;
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeedMultiplier * Time.fixedDeltaTime));
    }

    private float GetCurrentSwimSpeed()
    {
        float baseSpeed = isFastSwimming ? fastSwimSpeed : swimSpeed;

        if (Mathf.Abs(swimDirection.y) > 0.1f)
        {
            float verticalSpeedRatio = verticalSwimSpeed / swimSpeed;
            baseSpeed *= verticalSpeedRatio;
        }

        if (isNearSurface && !isDiving)
        {
            baseSpeed *= 0.8f;
        }

        return baseSpeed;
    }

    private void StartSurfacing()
    {
        isSurfacingActive = true;

        if (isDiving)
        {
            isDiving = false;
            IsSecondaryActive = false;
        }

    }

    public void ForceSurface()
    {
        if (rb == null || !waterDetector.IsInWater) return;

        StartSurfacing();

        Vector3 currentVelocity = rb.linearVelocity;
        currentVelocity.y = Mathf.Max(currentVelocity.y, surfaceForce * 0.1f);
        rb.linearVelocity = currentVelocity;

        DebugLog("Force surface applied with immediate velocity boost");
    }

    public bool CanExitWater()
    {
        if (waterDetector == null) return false;

        bool nearSurface = waterDetector.FeetDepth < 0.2f;
        bool slowVerticalMovement = Mathf.Abs(rb.linearVelocity.y) < 2f;

        return nearSurface && slowVerticalMovement;
    }

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

    /// <summary>
    /// ENHANCED: Force clean state for proper transitions and save/load operations
    /// </summary>
    public void ForceCleanState()
    {
        DebugLog("Force cleaning swimming state");

        // Reset all swimming state
        movementInput = Vector2.zero;
        isFastSwimming = false;
        isDiving = false;
        isSurfacingActive = false;
        swimDirection = Vector3.zero;

        // Reset interface properties
        IsMoving = false;
        IsSpeedModified = false;
        IsSecondaryActive = false;

        // Force restore original physics immediately
        RestoreOriginalPhysics();

        DebugLog("Swimming movement state cleaned and physics restored");
    }


    #region Gizmos and Debug

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        if (swimDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, swimDirection * 2f);
            Gizmos.DrawWireSphere(transform.position + swimDirection * 2f, 0.1f);
        }

        if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
        }

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

        if (waterDetector != null)
        {
            Gizmos.color = isNearSurface ? Color.green : Color.blue;
            Vector3 depthStart = transform.position;
            Vector3 depthEnd = depthStart + Vector3.down * waterDepth;
            Gizmos.DrawLine(depthStart, depthEnd);
            Gizmos.DrawWireSphere(depthEnd, 0.05f);
        }

#if UNITY_EDITOR
        string stateInfo = GetSwimmingStateInfo();
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, stateInfo);
#endif
    }

    #endregion
}