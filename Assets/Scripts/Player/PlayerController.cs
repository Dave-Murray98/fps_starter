using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Updated PlayerController with modular movement system.
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

    // Current active movement controller
    private IMovementController currentMovementController;
    private MovementMode currentMovementMode = MovementMode.Ground;

    // Input system reference
    private InputManager inputManager;
    private PlayerData playerData;

    // Water transition state
    private bool isTransitioningMovementMode = false;
    private float transitionTimer = 0f;

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

    private void InitializeMovementControllers()
    {
        // Initialize all movement controllers
        groundMovementController?.Initialize(this);
        swimmingMovementController?.Initialize(this);

        // Set initial active controller
        currentMovementController = groundMovementController;
        currentMovementController?.OnControllerActivated();

        DebugLog("Movement controllers initialized");
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
        inputManager = GameManager.Instance?.inputManager;
        playerData = GameManager.Instance?.playerData;

        if (inputManager != null)
        {
            ConnectToInputManager(inputManager);
        }
    }

    private void ConnectToInputManager(InputManager newInputManager)
    {
        DisconnectFromInputManager();

        inputManager = newInputManager;

        if (inputManager != null)
        {
            // Connect to unified action events
            inputManager.OnPrimaryActionPressed += HandlePrimaryActionInput;
            inputManager.OnSecondaryActionPressed += HandleSecondaryActionInput;
            inputManager.OnSecondaryActionReleased += HandleSecondaryActionReleased;

            DebugLog($"PlayerController connected to InputManager: {inputManager.GetInstanceID()}");
        }
    }

    private void DisconnectFromInputManager()
    {
        if (inputManager != null)
        {
            inputManager.OnPrimaryActionPressed -= HandlePrimaryActionInput;
            inputManager.OnSecondaryActionPressed -= HandleSecondaryActionInput;
            inputManager.OnSecondaryActionReleased -= HandleSecondaryActionReleased;
        }
    }

    private void Update()
    {
        if (!GameManager.Instance || GameManager.Instance.isPaused) return;

        UpdateTransitionTimer();
        UpdateMovementState();
        HandleInput();
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
        if (inputManager == null || currentMovementController == null) return;

        // Movement input (handled by current movement controller)
        if (canMove)
        {
            bool speedModifier = false;

            // Get appropriate speed modifier based on movement mode
            switch (currentMovementMode)
            {
                case MovementMode.Ground:
                    speedModifier = canSprint && inputManager.SpeedModifierHeld;
                    break;
                case MovementMode.Swimming:
                    speedModifier = canSwim && inputManager.SpeedModifierHeld;
                    break;
            }

            currentMovementController.HandleMovement(inputManager.MovementInput, speedModifier);
        }

        // Look input
        if (canLook)
        {
            playerCamera.SetLookInput(inputManager.LookInput);
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

    #region Movement Mode Management

    /// <summary>
    /// Switches between different movement modes (Ground, Swimming, Vehicle)
    /// </summary>
    private void SetMovementMode(MovementMode newMode)
    {
        if (currentMovementMode == newMode) return;

        MovementMode previousMode = currentMovementMode;

        // Deactivate current controller
        currentMovementController?.OnControllerDeactivated();

        // Switch to new controller
        switch (newMode)
        {
            case MovementMode.Ground:
                currentMovementController = groundMovementController;
                break;
            case MovementMode.Swimming:
                currentMovementController = swimmingMovementController;
                break;
                // case MovementMode.Vehicle: // Future implementation
        }

        // Activate new controller
        currentMovementController?.OnControllerActivated();
        currentMovementMode = newMode;

        // Update input manager
        if (inputManager != null)
        {
            inputManager.SetMovementMode(newMode);
        }

        OnMovementModeChanged?.Invoke(previousMode, newMode);
        DebugLog($"Movement mode changed: {previousMode} -> {newMode}");
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

    #region Water Transition Handlers

    private void HandleWaterEntered()
    {
        if (!canSwim) return;

        DebugLog("Water entered - transitioning to swimming mode");
        isTransitioningMovementMode = true;
        transitionTimer = 0f;
        SetMovementMode(MovementMode.Swimming);
    }

    private void HandleWaterExited()
    {
        DebugLog("Water exited - transitioning to ground mode");
        isTransitioningMovementMode = true;
        transitionTimer = 0f;
        SetMovementMode(MovementMode.Ground);
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
                    currentMovementController.HandlePrimaryAction(); // Surface
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
        if (waterDetector != null)
        {
            Debug.Log($"Water State: {waterDetector.GetWaterStateInfo()}");
        }
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