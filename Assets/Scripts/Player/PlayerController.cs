using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ENHANCED: PlayerController with comprehensive movement validation and clean state management.
/// Now handles save/load validation, doorway transitions, and environmental consistency checks.
/// Manages different movement controllers (Ground, Swimming, Vehicle) and coordinates
/// state transitions based on environmental conditions like water detection.
/// </summary>
[RequireComponent(typeof(PlayerCamera))]
[RequireComponent(typeof(PlayerAudio))]
[RequireComponent(typeof(PlayerWaterDetector))]
public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    public PlayerCamera playerCamera;
    public PlayerAudio playerAudio;
    public PlayerWaterDetector waterDetector;

    [Header("Movement Controllers")]
    public GroundMovementController groundMovementController;
    public SwimmingMovementController swimmingMovementController;

    [Header("State")]
    public MovementState currentState = MovementState.Idle;
    public MovementState previousState = MovementState.Idle;

    [Header("Abilities")]
    public bool canMove = true;
    public bool canJump = true;
    public bool canSprint = true;
    public bool canCrouch = true;
    public bool canLook = true;
    public bool canSwim = true;

    [Header("Interaction System")]
    public PlayerInteractionController interactionController;
    public bool canInteract = true;

    [Header("Transition Settings")]
    [SerializeField] private float waterTransitionDelay = 0.1f;
    [SerializeField] private bool enableDebugLogs = true;

    [Header("ENHANCED: Validation Settings")]
    [SerializeField] private bool enableMovementValidation = true;
    [SerializeField] private float validationDelay = 0.2f;

    // Current active movement controller
    private IMovementController currentMovementController;
    private MovementMode currentMovementMode = MovementMode.Ground;

    // ENHANCED: Input system reference - now uses singleton
    private PlayerData playerData;

    // Water transition state
    private bool isTransitioningMovementMode = false;
    private float transitionTimer = 0f;

    // ENHANCED: Initialization and validation state
    private bool isFullyInitialized = false;
    private bool needsEnvironmentValidation = false;

    // Events
    public event System.Action<MovementState, MovementState> OnStateChanged;
    public event System.Action<MovementMode, MovementMode> OnMovementModeChanged;

    private void Awake()
    {
        // Get or find components
        if (playerCamera == null) playerCamera = GetComponent<PlayerCamera>();
        if (playerAudio == null) playerAudio = GetComponent<PlayerAudio>();
        if (waterDetector == null) waterDetector = GetComponent<PlayerWaterDetector>();
        if (interactionController == null) interactionController = GetComponent<PlayerInteractionController>();

        // Get or create movement controllers
        if (groundMovementController == null) groundMovementController = GetComponent<GroundMovementController>();
        if (swimmingMovementController == null) swimmingMovementController = GetComponent<SwimmingMovementController>();

        // Add missing movement controllers if needed
        if (groundMovementController == null)
        {
            groundMovementController = gameObject.AddComponent<GroundMovementController>();
        }
        if (swimmingMovementController == null)
        {
            swimmingMovementController = gameObject.AddComponent<SwimmingMovementController>();
        }
    }

    private void Start()
    {
        Initialize();

        // Subscribe to manager events
        GameManager.OnManagersRefreshed += RefreshComponentReferences;
        InputManager.OnInputManagerReady += OnInputManagerReady;

        // Ensure interaction components exist
        if (interactionController == null)
        {
            interactionController = gameObject.AddComponent<PlayerInteractionController>();
        }

        PlayerInteractionDetector interactionDetector = GetComponent<PlayerInteractionDetector>();
        if (interactionDetector == null)
        {
            interactionDetector = gameObject.AddComponent<PlayerInteractionDetector>();
        }

        // Subscribe to water detection events
        SetupWaterDetectionEvents();

        // ENHANCED: Start validation coroutine after initialization
        StartCoroutine(DelayedValidationSetup());
    }

    /// <summary>
    /// ENHANCED: Sets up validation after all systems are initialized
    /// </summary>
    private System.Collections.IEnumerator DelayedValidationSetup()
    {
        yield return new WaitForSecondsRealtime(0.2f);

        // Perform initial environment validation
        if (enableMovementValidation)
        {
            ForceMovementModeValidation();
        }

        isFullyInitialized = true;
        DebugLog("PlayerController fully initialized with validation");
    }

    private void Initialize()
    {
        RefreshComponentReferences();

        // Initialize components
        InitializeMovementControllers();
        playerCamera.Initialize(this);
        playerAudio.Initialize(this);

        // Set initial movement mode
        SetMovementMode(MovementMode.Ground);

        DebugLog("PlayerController initialized");
    }

    /// <summary>
    /// ENHANCED: Movement controller initialization with proper enable/disable management
    /// </summary>
    private void InitializeMovementControllers()
    {
        // Initialize all movement controllers
        groundMovementController?.Initialize(this);
        swimmingMovementController?.Initialize(this);

        // ENHANCED: Disable all controllers first for clean state
        if (groundMovementController is MonoBehaviour groundComp)
        {
            groundComp.enabled = false;
            DebugLog("Initially disabled ground controller component");
        }

        if (swimmingMovementController is MonoBehaviour swimmingComp)
        {
            swimmingComp.enabled = false;
            DebugLog("Initially disabled swimming controller component");
        }

        // Set initial controller and enable only that one
        currentMovementController = groundMovementController;
        if (currentMovementController != null)
        {
            if (currentMovementController is MonoBehaviour activeComp)
            {
                activeComp.enabled = true;
                DebugLog("Enabled initial ground controller component");
            }
            currentMovementController.OnControllerActivated();
            DebugLog("Initial ground movement controller activated");
        }

        DebugLog("Movement controllers initialized with proper enable/disable");
    }

    private void SetupWaterDetectionEvents()
    {
        if (waterDetector != null)
        {
            waterDetector.OnWaterEntered += HandleWaterEntered;
            waterDetector.OnWaterExited += HandleWaterExited;
            waterDetector.OnHeadSubmerged += HandleHeadSubmerged;
            waterDetector.OnHeadSurfaced += HandleHeadSurfaced;

            DebugLog("Water detection events subscribed");
        }
    }

    private void OnInputManagerReady(InputManager newInputManager)
    {
        ConnectToInputManager(newInputManager);
    }

    private void RefreshComponentReferences()
    {
        playerData = GameManager.Instance?.playerData;

        // ENHANCED: Connect to singleton InputManager
        if (InputManager.Instance != null)
        {
            ConnectToInputManager(InputManager.Instance);
        }
    }

    /// <summary>
    /// ENHANCED: Simplified input manager connection using singleton pattern
    /// </summary>
    private void ConnectToInputManager(InputManager inputManager)
    {
        DisconnectFromInputManager();

        if (inputManager != null)
        {
            // Connect to unified action events
            inputManager.OnPrimaryActionPressed += HandlePrimaryActionInput;
            inputManager.OnPrimaryActionReleased += HandlePrimaryActionReleased;
            inputManager.OnSecondaryActionPressed += HandleSecondaryActionInput;
            inputManager.OnSecondaryActionReleased += HandleSecondaryActionReleased;

            DebugLog($"PlayerController connected to InputManager singleton");
        }
    }

    /// <summary>
    /// ENHANCED: Disconnect from singleton InputManager
    /// </summary>
    private void DisconnectFromInputManager()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPrimaryActionPressed -= HandlePrimaryActionInput;
            InputManager.Instance.OnPrimaryActionReleased -= HandlePrimaryActionReleased;
            InputManager.Instance.OnSecondaryActionPressed -= HandleSecondaryActionInput;
            InputManager.Instance.OnSecondaryActionReleased -= HandleSecondaryActionReleased;
        }
    }

    private void Update()
    {
        if (!GameManager.Instance || GameManager.Instance.isPaused) return;

        UpdateTransitionTimer();
        UpdateMovementState();
        HandleInput();

        // ENHANCED: Handle validation requests
        if (needsEnvironmentValidation && enableMovementValidation)
        {
            PerformEnvironmentValidation();
            needsEnvironmentValidation = false;
        }
    }

    private void UpdateTransitionTimer()
    {
        if (isTransitioningMovementMode)
        {
            transitionTimer += Time.deltaTime;
            if (transitionTimer >= waterTransitionDelay)
            {
                isTransitioningMovementMode = false;
                transitionTimer = 0f;
            }
        }
    }

    private void HandleInput()
    {
        if (InputManager.Instance == null || currentMovementController == null) return;

        // Movement input (handled by current movement controller)
        if (canMove)
        {
            bool speedModifier = false;

            // Get appropriate speed modifier based on movement mode
            switch (currentMovementMode)
            {
                case MovementMode.Ground:
                    speedModifier = canSprint && InputManager.Instance.SpeedModifierHeld;
                    break;
                case MovementMode.Swimming:
                    speedModifier = canSwim && InputManager.Instance.SpeedModifierHeld;
                    break;
            }

            currentMovementController.HandleMovement(InputManager.Instance.MovementInput, speedModifier);
        }

        // Look input
        if (canLook)
        {
            playerCamera.SetLookInput(InputManager.Instance.LookInput);
        }
    }

    private void UpdateMovementState()
    {
        MovementState newState = DetermineMovementState();

        if (newState != currentState)
        {
            ChangeState(newState);
        }
    }

    private MovementState DetermineMovementState()
    {
        // Water-related states take priority
        if (waterDetector != null && waterDetector.IsInWater)
        {
            if (isTransitioningMovementMode)
            {
                return currentMovementMode == MovementMode.Swimming ? MovementState.WaterEntry : MovementState.WaterExit;
            }

            if (currentMovementMode == MovementMode.Swimming)
            {
                if (swimmingMovementController.IsSecondaryActive) // Diving
                {
                    return MovementState.Diving;
                }
                else if (waterDetector.FeetDepth < 0.5f) // Near surface
                {
                    return MovementState.SurfaceSwimming;
                }
                else
                {
                    return MovementState.Swimming;
                }
            }
        }

        // Ground-based states
        if (currentMovementMode == MovementMode.Ground)
        {
            if (!groundMovementController.IsGrounded)
            {
                return groundMovementController.GetVelocity().y > 0.1f ? MovementState.Jumping : MovementState.Falling;
            }

            if (groundMovementController.IsSecondaryActive) // Crouching
            {
                return MovementState.Crouching;
            }

            float horizontalSpeed = new Vector2(
                groundMovementController.GetVelocity().x,
                groundMovementController.GetVelocity().z
            ).magnitude;

            if (horizontalSpeed < 0.1f)
            {
                return MovementState.Idle;
            }

            return groundMovementController.IsSpeedModified ? MovementState.Running : MovementState.Walking;
        }

        return MovementState.Idle;
    }

    private void ChangeState(MovementState newState)
    {
        previousState = currentState;
        currentState = newState;

        OnStateChanged?.Invoke(previousState, currentState);

        // Notify components of state change
        currentMovementController?.OnMovementStateChanged(previousState, currentState);
        playerCamera.OnMovementStateChanged(previousState, currentState);
        playerAudio.OnMovementStateChanged(previousState, currentState);

        DebugLog($"State changed: {previousState} -> {currentState}");
    }

    #region ENHANCED: Movement Mode Management with Validation

    /// <summary>
    /// ENHANCED: Movement mode switching with comprehensive validation and clean transitions
    /// </summary>
    private void SetMovementMode(MovementMode newMode)
    {
        if (currentMovementMode == newMode) return;

        MovementMode previousMode = currentMovementMode;
        DebugLog($"ENHANCED: Switching movement mode: {previousMode} -> {newMode}");

        // STEP 1: Properly deactivate current controller
        if (currentMovementController != null)
        {
            DebugLog($"Deactivating {currentMovementController.GetType().Name}");
            currentMovementController.OnControllerDeactivated();

            // Disable the component so FixedUpdate stops running
            if (currentMovementController is MonoBehaviour currentComponent)
            {
                currentComponent.enabled = false;
                DebugLog($"Disabled component {currentComponent.GetType().Name}");
            }
        }

        // STEP 2: Switch to new controller
        switch (newMode)
        {
            case MovementMode.Ground:
                currentMovementController = groundMovementController;
                break;
            case MovementMode.Swimming:
                currentMovementController = swimmingMovementController;
                break;
        }

        currentMovementMode = newMode;

        // STEP 3: Properly activate new controller
        if (currentMovementController != null)
        {
            // Enable the component so FixedUpdate starts running
            if (currentMovementController is MonoBehaviour newComponent)
            {
                newComponent.enabled = true;
                DebugLog($"Enabled component {newComponent.GetType().Name}");
            }

            DebugLog($"Activating {currentMovementController.GetType().Name}");
            currentMovementController.OnControllerActivated();
        }

        // STEP 4: Update InputManager
        if (InputManager.Instance != null)
        {
            InputManager.Instance.SetMovementMode(newMode);
            DebugLog($"Updated InputManager to movement mode: {newMode}");
        }

        OnMovementModeChanged?.Invoke(previousMode, newMode);
        DebugLog($"Movement mode change complete: {previousMode} -> {newMode}");
    }

    /// <summary>
    /// ENHANCED: Sets initial movement mode for save/load operations
    /// </summary>
    public void SetInitialMovementMode(MovementMode mode)
    {
        DebugLog($"Setting initial movement mode to: {mode}");
        SetMovementMode(mode);
    }

    /// <summary>
    /// ENHANCED: Forces validation of movement mode against current environment
    /// Used after save/load and doorway transitions to ensure consistency
    /// </summary>
    public void ForceMovementModeValidation()
    {
        if (!enableMovementValidation)
        {
            DebugLog("Movement validation disabled");
            return;
        }

        DebugLog("FORCE VALIDATION: Checking movement mode against environment");

        // Check what mode we SHOULD be in based on current position
        bool shouldBeSwimming = waterDetector?.IsInWater ?? false;
        bool currentlySwimming = currentMovementMode == MovementMode.Swimming;

        DebugLog($"Should be swimming: {shouldBeSwimming}, Currently swimming: {currentlySwimming}");

        if (shouldBeSwimming != currentlySwimming)
        {
            MovementMode correctMode = shouldBeSwimming ? MovementMode.Swimming : MovementMode.Ground;
            DebugLog($"CORRECTING movement mode mismatch: {currentMovementMode} → {correctMode}");

            // Force clean state reset
            ForceCleanMovementState();

            // Set correct movement mode
            SetMovementMode(correctMode);
        }
        else
        {
            DebugLog("Movement mode is correct, but cleaning physics state anyway");
            ForceCleanMovementState();
        }
    }

    /// <summary>
    /// ENHANCED: Cleans any residual physics state from previous movement modes
    /// </summary>
    private void ForceCleanMovementState()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Clear velocity to prevent weird movement artifacts
            Vector3 currentVelocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, currentVelocity.y * 0.5f, 0f); // Keep some Y for landing

            DebugLog($"Cleaned velocity: {currentVelocity} → {rb.linearVelocity}");
        }

        // Force both movement controllers to clean their state
        if (groundMovementController != null)
        {
            groundMovementController.ForceCleanState();
        }

        if (swimmingMovementController != null)
        {
            swimmingMovementController.ForceCleanState();
        }
    }

    /// <summary>
    /// ENHANCED: Requests environment validation on next frame
    /// </summary>
    public void RequestEnvironmentValidation()
    {
        needsEnvironmentValidation = true;
        DebugLog("Environment validation requested");
    }

    /// <summary>
    /// ENHANCED: Performs environment validation with proper timing
    /// </summary>
    private void PerformEnvironmentValidation()
    {
        if (!isFullyInitialized)
        {
            DebugLog("Skipping validation - not fully initialized");
            return;
        }

        StartCoroutine(DelayedEnvironmentValidation());
    }

    /// <summary>
    /// ENHANCED: Delayed validation to ensure physics and positioning have settled
    /// </summary>
    private System.Collections.IEnumerator DelayedEnvironmentValidation()
    {
        yield return new WaitForSecondsRealtime(validationDelay);

        DebugLog("Performing delayed environment validation");
        ForceMovementModeValidation();
    }

    #endregion

    #region Water Transition Handlers

    private void HandleWaterEntered()
    {
        if (!canSwim) return;

        DebugLog("ENHANCED: Water entered - transitioning to swimming mode");
        isTransitioningMovementMode = true;
        transitionTimer = 0f;
        SetMovementMode(MovementMode.Swimming);
    }

    private void HandleWaterExited()
    {
        DebugLog("ENHANCED: Water exited - transitioning to ground mode");
        isTransitioningMovementMode = true;
        transitionTimer = 0f;
        SetMovementMode(MovementMode.Ground);

        // ENHANCED: Force a brief pause to let physics settle
        StartCoroutine(PostWaterExitCleanup());
    }

    /// <summary>
    /// ENHANCED: Additional cleanup after water exit to prevent residual movement
    /// </summary>
    private System.Collections.IEnumerator PostWaterExitCleanup()
    {
        yield return new WaitForSecondsRealtime(0.1f);

        // Ensure ground controller is properly active
        if (currentMovementMode == MovementMode.Ground && groundMovementController != null)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Clear any residual horizontal velocity
                Vector3 velocity = rb.linearVelocity;
                velocity.x *= 0.5f; // Dampen horizontal movement
                velocity.z *= 0.5f;
                rb.linearVelocity = velocity;
                DebugLog("Post-water-exit velocity cleanup applied");
            }
        }
    }

    private void HandleHeadSubmerged()
    {
        DebugLog("Head submerged");
        // Future: Trigger underwater audio/visual effects
    }

    private void HandleHeadSurfaced()
    {
        DebugLog("Head surfaced");
        // Future: Restore normal audio/visual effects
    }

    #endregion

    #region Input Handlers

    /// <summary>
    /// ENHANCED: Primary action input handling with proper surfacing support
    /// </summary>
    private void HandlePrimaryActionInput()
    {
        if (currentMovementController == null) return;

        // Context-aware primary action handling
        switch (currentMovementMode)
        {
            case MovementMode.Ground:
                if (canJump)
                {
                    currentMovementController.HandlePrimaryAction();
                }
                break;
            case MovementMode.Swimming:
                if (canSwim)
                {
                    currentMovementController.HandlePrimaryAction(); // Start surfacing
                }
                break;
        }
    }

    /// <summary>
    /// ENHANCED: Handle primary action release (needed for continuous surfacing)
    /// </summary>
    private void HandlePrimaryActionReleased()
    {
        if (currentMovementController == null) return;

        // Only swimming needs to handle primary action release (for surfacing)
        switch (currentMovementMode)
        {
            case MovementMode.Ground:
                // Ground movement doesn't need release handling for jump
                currentMovementController.HandlePrimaryActionReleased();
                break;
            case MovementMode.Swimming:
                if (canSwim)
                {
                    currentMovementController.HandlePrimaryActionReleased();
                }
                break;
        }
    }

    private void HandleSecondaryActionInput()
    {
        if (currentMovementController == null) return;

        // Context-aware secondary action handling
        switch (currentMovementMode)
        {
            case MovementMode.Ground:
                if (canCrouch)
                {
                    currentMovementController.HandleSecondaryAction();
                }
                break;
            case MovementMode.Swimming:
                if (canSwim)
                {
                    currentMovementController.HandleSecondaryAction(); // Dive
                }
                break;
        }
    }

    private void HandleSecondaryActionReleased()
    {
        if (currentMovementController == null) return;

        // Context-aware secondary action release handling
        switch (currentMovementMode)
        {
            case MovementMode.Ground:
                if (canCrouch)
                {
                    currentMovementController.HandleSecondaryActionReleased();
                }
                break;
            case MovementMode.Swimming:
                if (canSwim)
                {
                    currentMovementController.HandleSecondaryActionReleased(); // Stop diving
                }
                break;
        }
    }

    #endregion

    #region Public Getters and Setters

    // Ability controls
    public void SetMovementEnabled(bool enabled) => canMove = enabled;
    public void SetJumpEnabled(bool enabled) => canJump = enabled;
    public void SetSprintEnabled(bool enabled) => canSprint = enabled;
    public void SetCrouchEnabled(bool enabled) => canCrouch = enabled;
    public void SetLookEnabled(bool enabled) => canLook = enabled;
    public void SetSwimmingEnabled(bool enabled) => canSwim = enabled;

    // State getters
    public bool IsMoving => currentMovementController?.IsMoving ?? false;
    public bool IsGrounded => currentMovementController?.IsGrounded ?? false;
    public bool IsSprinting => currentMovementController?.IsSpeedModified ?? false;
    public bool IsCrouching => (currentMovementMode == MovementMode.Ground) && (currentMovementController?.IsSecondaryActive ?? false);
    public bool IsSwimming => currentMovementMode == MovementMode.Swimming;
    public bool IsDiving => (currentMovementMode == MovementMode.Swimming) && (currentMovementController?.IsSecondaryActive ?? false);
    public bool IsInWater => waterDetector?.IsInWater ?? false;
    public Vector3 Velocity => currentMovementController?.GetVelocity() ?? Vector3.zero;

    // Movement mode getter
    public MovementMode CurrentMovementMode => currentMovementMode;

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerController] {message}");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        GameManager.OnManagersRefreshed -= RefreshComponentReferences;
        InputManager.OnInputManagerReady -= OnInputManagerReady;
        DisconnectFromInputManager();

        // Unsubscribe from water detection events
        if (waterDetector != null)
        {
            waterDetector.OnWaterEntered -= HandleWaterEntered;
            waterDetector.OnWaterExited -= HandleWaterExited;
            waterDetector.OnHeadSubmerged -= HandleHeadSubmerged;
            waterDetector.OnHeadSurfaced -= HandleHeadSurfaced;
        }

        // Cleanup movement controllers
        currentMovementController?.Cleanup();
    }

    #region Editor Debug Methods

#if UNITY_EDITOR
    [ContextMenu("Debug Current State")]
    private void DebugCurrentState()
    {
        Debug.Log($"=== PlayerController Debug Info ===");
        Debug.Log($"Current State: {currentState}");
        Debug.Log($"Movement Mode: {currentMovementMode}");
        Debug.Log($"Is Moving: {IsMoving}");
        Debug.Log($"Is In Water: {IsInWater}");
        Debug.Log($"Current Velocity: {Velocity}");
        Debug.Log($"Active Controller: {currentMovementController?.GetType().Name ?? "None"}");
        Debug.Log($"Is Fully Initialized: {isFullyInitialized}");
        if (waterDetector != null)
        {
            Debug.Log($"Water State: {waterDetector.GetWaterStateInfo()}");
        }
    }

    [ContextMenu("Force Movement Mode Validation")]
    private void DebugForceValidation()
    {
        if (!Application.isPlaying) return;
        ForceMovementModeValidation();
    }

    [ContextMenu("Force Swimming Mode")]
    private void ForceSwimmingMode()
    {
        if (!Application.isPlaying) return;
        SetMovementMode(MovementMode.Swimming);
    }

    [ContextMenu("Force Ground Mode")]
    private void ForceGroundMode()
    {
        if (!Application.isPlaying) return;
        SetMovementMode(MovementMode.Ground);
    }
#endif

    #endregion
}

public enum GroundType
{
    Default,
    Grass,
    Stone,
    Metal,
    Wood,
    Water
}