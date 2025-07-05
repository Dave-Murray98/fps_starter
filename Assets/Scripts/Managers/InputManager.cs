using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// FIXED: Singleton InputManager with proper initialization order and immediate input responsiveness.
/// Ensures all ActionMaps are properly enabled from the start and input works immediately.
/// </summary>
public class InputManager : MonoBehaviour, IManager
{
    public static InputManager Instance { get; private set; }

    #region Fields
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    [Header("UI Actions")]
    private InputAction pauseAction;

    [Header("Core Movement Actions")]
    private InputAction moveAction;
    private InputAction lookAction;

    [Header("Ground Locomotion Actions")]
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    [Header("Swimming Actions")]
    private InputAction surfaceAction;
    private InputAction swimSpeedAction;
    private InputAction diveAction;

    [Header("Gameplay Actions")]
    private InputAction interactAction;
    private InputAction leftClickAction;
    private InputAction rightClickAction;
    private InputAction scrollWheelAction;
    private InputAction[] hotkeyActions = new InputAction[10];

    [Header("Inventory Actions")]
    private InputAction toggleInventoryAction;
    private InputAction rotateInventoryItemAction;

    #endregion

    #region Public Properties
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool PrimaryActionPressed { get; private set; }
    public bool PrimaryActionHeld { get; private set; }
    public bool SpeedModifierHeld { get; private set; }
    public bool SecondaryActionPressed { get; private set; }
    public bool SecondaryActionHeld { get; private set; }
    #endregion

    #region Events
    public event Action OnPrimaryActionPressed;
    public event Action OnPrimaryActionReleased;
    public event Action OnSecondaryActionPressed;
    public event Action OnSecondaryActionReleased;
    public event Action OnInteractPressed;
    public event Action OnRotateInventoryItemPressed;
    public event Action OnLeftClickPressed;
    public event Action OnRightClickPressed;
    public System.Action<Vector2> OnScrollWheelInput;
    public System.Action<int> OnHotkeyPressed;
    public static event Action<InputManager> OnInputManagerReady;
    #endregion

    // Action maps
    private InputActionMap uiActionMap;
    private InputActionMap coreMovementActionMap;
    private InputActionMap groundLocomotionActionMap;
    private InputActionMap swimmingActionMap;
    private InputActionMap gameplayActionMap;
    private InputActionMap inventoryActionMap;

    // State tracking
    private InputActionMap currentMovementActionMap;
    private MovementMode currentMovementMode = MovementMode.Ground;
    private bool gameplayInputEnabled = true;
    private bool isCleanedUp = false;
    private bool isFullyInitialized = false;

    // Utility methods
    public bool IsMoving() => MovementInput.magnitude > 0.1f;
    public bool IsLooking() => LookInput.magnitude > 0.1f;
    public bool IsProperlyInitialized => isFullyInitialized && !isCleanedUp;

    #region Singleton Pattern

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // IMMEDIATE SETUP - Don't wait for Initialize()
            SetupInputActionsImmediate();

            Debug.Log("[InputManager] Singleton created with immediate input setup");
        }
        else
        {
            Debug.Log("[InputManager] Duplicate destroyed");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Complete the initialization process
        CompleteInitialization();
    }

    #endregion

    #region FIXED: Immediate Setup

    /// <summary>
    /// CRITICAL FIX: Sets up input actions immediately in Awake() so input works from frame 1
    /// </summary>
    private void SetupInputActionsImmediate()
    {
        if (inputActions == null)
        {
            Debug.LogError("[InputManager] InputActionAsset is not assigned! Input will not work!");
            return;
        }

        // Get action maps
        uiActionMap = inputActions.FindActionMap("UI");
        coreMovementActionMap = inputActions.FindActionMap("CoreMovement");
        groundLocomotionActionMap = inputActions.FindActionMap("GroundLocomotion");
        swimmingActionMap = inputActions.FindActionMap("Swimming");
        gameplayActionMap = inputActions.FindActionMap("Gameplay");
        inventoryActionMap = inputActions.FindActionMap("Inventory");

        // Validate critical action maps exist
        if (uiActionMap == null)
        {
            Debug.LogError("[InputManager] UI ActionMap not found! Pause won't work!");
            return;
        }

        if (coreMovementActionMap == null || groundLocomotionActionMap == null)
        {
            Debug.LogError("[InputManager] Core movement ActionMaps not found! Movement won't work!");
            return;
        }

        // Setup actions
        SetupUIInputActions();
        SetupCoreMovementInputActions();
        SetupGroundLocomotionInputActions();
        SetupSwimmingInputActions();
        SetupGameplayInputActions();
        SetupInventoryInputActions();

        // Subscribe to events
        SubscribeToInputActions();

        // CRITICAL: Enable essential ActionMaps immediately
        EnableEssentialActionMapsImmediate();

        // Set initial movement mode
        currentMovementMode = MovementMode.Ground;
        currentMovementActionMap = groundLocomotionActionMap;

        Debug.Log("[InputManager] Immediate input setup complete - Input should work now!");
    }

    /// <summary>
    /// CRITICAL FIX: Enables essential ActionMaps immediately so input works from frame 1
    /// </summary>
    private void EnableEssentialActionMapsImmediate()
    {
        // UI ActionMap - MUST be enabled for pause to work
        if (uiActionMap != null)
        {
            uiActionMap.Enable();
            Debug.Log("[InputManager] UI ActionMap enabled immediately");
        }

        // Core Movement ActionMap - MUST be enabled for movement input
        if (coreMovementActionMap != null)
        {
            coreMovementActionMap.Enable();
            Debug.Log("[InputManager] Core Movement ActionMap enabled immediately");
        }

        // Ground Locomotion ActionMap - Default movement mode
        if (groundLocomotionActionMap != null)
        {
            groundLocomotionActionMap.Enable();
            Debug.Log("[InputManager] Ground Locomotion ActionMap enabled immediately");
        }

        // Gameplay ActionMap - For interactions
        if (gameplayActionMap != null)
        {
            gameplayActionMap.Enable();
            Debug.Log("[InputManager] Gameplay ActionMap enabled immediately");
        }

        // Inventory ActionMap - For inventory controls
        if (inventoryActionMap != null)
        {
            inventoryActionMap.Enable();
            Debug.Log("[InputManager] Inventory ActionMap enabled immediately");
        }

        Debug.Log("[InputManager] All essential ActionMaps enabled - Input is active!");
    }

    /// <summary>
    /// Completes initialization after immediate setup
    /// </summary>
    private void CompleteInitialization()
    {
        // Subscribe to game events
        GameEvents.OnGamePaused += DisableGameplayInput;
        GameEvents.OnGameResumed += EnableGameplayInput;

        isFullyInitialized = true;

        Debug.Log("[InputManager] Full initialization complete");

        // Notify other systems
        OnInputManagerReady?.Invoke(this);
    }

    #endregion

    #region IManager Implementation

    public void Initialize()
    {
        if (isCleanedUp)
        {
            Debug.Log("[InputManager] Reinitializing after cleanup");
            isCleanedUp = false;
            SetupInputActionsImmediate();
        }

        if (!isFullyInitialized)
        {
            CompleteInitialization();
        }

        Debug.Log("[InputManager] Initialize called - already set up in Awake()");
    }

    public void RefreshReferences()
    {
        if (isCleanedUp || !isFullyInitialized)
        {
            Debug.Log("[InputManager] Skipping RefreshReferences - not properly initialized");
            return;
        }

        Debug.Log("[InputManager] RefreshReferences - ensuring ActionMaps are enabled");

        // Re-enable essential ActionMaps
        EnableEssentialActionMapsImmediate();

        // Notify systems that we're ready
        OnInputManagerReady?.Invoke(this);
    }

    public void Cleanup()
    {
        Debug.Log("[InputManager] Starting cleanup");
        isCleanedUp = true;
        isFullyInitialized = false;

        // Clear events
        ClearAllEvents();

        // Unsubscribe from game events
        GameEvents.OnGamePaused -= DisableGameplayInput;
        GameEvents.OnGameResumed -= EnableGameplayInput;

        // Disable and clean up input actions
        DisableAllInputActions();
        UnsubscribeFromInputActions();
    }

    #endregion

    #region Movement Mode Management

    public void SetMovementMode(MovementMode mode)
    {
        if (currentMovementMode == mode && currentMovementActionMap != null && currentMovementActionMap.enabled)
        {
            return; // Already in correct mode and working
        }

        Debug.Log($"[InputManager] Setting movement mode: {currentMovementMode} -> {mode}");

        // Disable current movement ActionMap (but keep others enabled)
        if (currentMovementActionMap != null)
        {
            currentMovementActionMap.Disable();
        }

        // Set new movement ActionMap
        switch (mode)
        {
            case MovementMode.Ground:
                currentMovementActionMap = groundLocomotionActionMap;
                break;
            case MovementMode.Swimming:
                currentMovementActionMap = swimmingActionMap;
                break;
            default:
                Debug.LogWarning($"[InputManager] Unknown movement mode {mode}, defaulting to Ground");
                currentMovementActionMap = groundLocomotionActionMap;
                mode = MovementMode.Ground;
                break;
        }

        currentMovementMode = mode;

        // Enable new movement ActionMap
        if (currentMovementActionMap != null && gameplayInputEnabled)
        {
            currentMovementActionMap.Enable();
            Debug.Log($"[InputManager] Enabled new movement ActionMap: {currentMovementActionMap.name}");
        }
    }

    public MovementMode GetCurrentMovementMode() => currentMovementMode;

    public void ForceResetToGroundMode()
    {
        Debug.LogWarning("[InputManager] FORCE RESET: Resetting to Ground mode");

        // Disable all movement ActionMaps
        groundLocomotionActionMap?.Disable();
        swimmingActionMap?.Disable();

        // Force set to ground mode
        currentMovementMode = MovementMode.Ground;
        currentMovementActionMap = groundLocomotionActionMap;

        // Enable ground ActionMap
        if (currentMovementActionMap != null && gameplayInputEnabled)
        {
            currentMovementActionMap.Enable();
            Debug.Log("[InputManager] Ground mode reset complete");
        }
    }

    #endregion

    #region Input State Management

    public void DisableGameplayInput()
    {
        if (isCleanedUp) return;

        Debug.Log("[InputManager] Disabling gameplay input (keeping UI enabled)");
        gameplayInputEnabled = false;

        // Disable gameplay ActionMaps but KEEP UI enabled
        coreMovementActionMap?.Disable();
        currentMovementActionMap?.Disable();
        gameplayActionMap?.Disable();
        inventoryActionMap?.Disable();

        // UI ActionMap stays enabled for pause functionality
        Debug.Log("[InputManager] Gameplay input disabled, UI remains active");
    }

    public void EnableGameplayInput()
    {
        if (isCleanedUp) return;

        Debug.Log("[InputManager] Enabling gameplay input");
        gameplayInputEnabled = true;

        // Re-enable all essential ActionMaps
        EnableEssentialActionMapsImmediate();
    }

    private void DisableAllInputActions()
    {
        uiActionMap?.Disable();
        coreMovementActionMap?.Disable();
        groundLocomotionActionMap?.Disable();
        swimmingActionMap?.Disable();
        gameplayActionMap?.Disable();
        inventoryActionMap?.Disable();
    }

    #endregion

    #region Setup Methods

    private void SetupUIInputActions()
    {
        pauseAction = uiActionMap.FindAction("Pause");
        if (pauseAction == null)
        {
            Debug.LogError("[InputManager] Pause action not found in UI ActionMap!");
        }
    }

    private void SetupCoreMovementInputActions()
    {
        moveAction = coreMovementActionMap.FindAction("Move");
        lookAction = coreMovementActionMap.FindAction("Look");
    }

    private void SetupGroundLocomotionInputActions()
    {
        jumpAction = groundLocomotionActionMap.FindAction("Jump");
        sprintAction = groundLocomotionActionMap.FindAction("Sprint");
        crouchAction = groundLocomotionActionMap.FindAction("Crouch");
    }

    private void SetupSwimmingInputActions()
    {
        surfaceAction = swimmingActionMap.FindAction("Surface");
        swimSpeedAction = swimmingActionMap.FindAction("SwimSpeed");
        diveAction = swimmingActionMap.FindAction("Dive");
    }

    private void SetupGameplayInputActions()
    {
        interactAction = gameplayActionMap.FindAction("Interact");
        leftClickAction = gameplayActionMap.FindAction("LeftClick");
        rightClickAction = gameplayActionMap.FindAction("RightClick");
        scrollWheelAction = gameplayActionMap.FindAction("ScrollWheel");

        // Hotkey actions
        for (int i = 1; i <= 10; i++)
        {
            string actionName = i == 10 ? "Hotkey0" : $"Hotkey{i}";
            hotkeyActions[i - 1] = gameplayActionMap.FindAction(actionName);
        }
    }

    private void SetupInventoryInputActions()
    {
        toggleInventoryAction = inventoryActionMap.FindAction("ToggleInventory");
        rotateInventoryItemAction = inventoryActionMap.FindAction("RotateInventoryItem");
    }

    #endregion

    #region Event Management

    private void ClearAllEvents()
    {
        OnPrimaryActionPressed = null;
        OnPrimaryActionReleased = null;
        OnSecondaryActionPressed = null;
        OnSecondaryActionReleased = null;
        OnInteractPressed = null;
        OnRotateInventoryItemPressed = null;
        OnLeftClickPressed = null;
        OnRightClickPressed = null;
        OnScrollWheelInput = null;
        OnHotkeyPressed = null;
    }

    #endregion

    #region Event Subscription

    private void SubscribeToInputActions()
    {
        SubscribeToUIInputActions();
        SubscribeToPrimarySecondaryActions();
        SubscribeToGameplayInputActions();
        SubscribeToInventoryInputActions();
    }

    private void SubscribeToUIInputActions()
    {
        if (pauseAction != null)
        {
            pauseAction.performed += OnPausePerformed;
        }
    }

    private void SubscribeToPrimarySecondaryActions()
    {
        // Ground locomotion actions
        if (jumpAction != null)
        {
            jumpAction.performed += OnPrimaryActionPerformed;
            jumpAction.canceled += OnPrimaryActionCanceled;
        }

        if (crouchAction != null)
        {
            crouchAction.performed += OnSecondaryActionPerformed;
            crouchAction.canceled += OnSecondaryActionCanceled;
        }

        // Swimming actions
        if (surfaceAction != null)
        {
            surfaceAction.performed += OnPrimaryActionPerformed;
            surfaceAction.canceled += OnPrimaryActionCanceled;
        }

        if (diveAction != null)
        {
            diveAction.performed += OnSecondaryActionPerformed;
            diveAction.canceled += OnSecondaryActionCanceled;
        }
    }

    private void SubscribeToGameplayInputActions()
    {
        if (interactAction != null)
        {
            interactAction.performed += OnInteractPerformed;
        }

        if (leftClickAction != null)
        {
            leftClickAction.performed += OnLeftClickPerformed;
        }

        if (rightClickAction != null)
        {
            rightClickAction.performed += OnRightClickPerformed;
        }

        if (scrollWheelAction != null)
        {
            scrollWheelAction.performed += OnScrollWheelPerformed;
        }

        // Subscribe to hotkey actions
        for (int i = 0; i < hotkeyActions.Length; i++)
        {
            if (hotkeyActions[i] != null)
            {
                int slotNumber = i + 1;
                hotkeyActions[i].performed += _ => OnHotkeyPerformed(slotNumber);
            }
        }
    }

    private void SubscribeToInventoryInputActions()
    {
        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.performed += OnToggleInventoryPerformed;
        }

        if (rotateInventoryItemAction != null)
        {
            rotateInventoryItemAction.performed += OnRotateInventoryItemPerformed;
        }
    }

    private void UnsubscribeFromInputActions()
    {
        // UI actions
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
        }

        // Primary/Secondary actions
        if (jumpAction != null)
        {
            jumpAction.performed -= OnPrimaryActionPerformed;
            jumpAction.canceled -= OnPrimaryActionCanceled;
        }

        if (crouchAction != null)
        {
            crouchAction.performed -= OnSecondaryActionPerformed;
            crouchAction.canceled -= OnSecondaryActionCanceled;
        }

        if (surfaceAction != null)
        {
            surfaceAction.performed -= OnPrimaryActionPerformed;
            surfaceAction.canceled -= OnPrimaryActionCanceled;
        }

        if (diveAction != null)
        {
            diveAction.performed -= OnSecondaryActionPerformed;
            diveAction.canceled -= OnSecondaryActionCanceled;
        }

        // Gameplay actions
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
        }

        if (leftClickAction != null)
        {
            leftClickAction.performed -= OnLeftClickPerformed;
        }

        if (rightClickAction != null)
        {
            rightClickAction.performed -= OnRightClickPerformed;
        }

        if (scrollWheelAction != null)
        {
            scrollWheelAction.performed -= OnScrollWheelPerformed;
        }

        // Hotkey actions
        for (int i = 0; i < hotkeyActions.Length; i++)
        {
            if (hotkeyActions[i] != null)
            {
                int slotNumber = i + 1;
                hotkeyActions[i].performed -= _ => OnHotkeyPerformed(slotNumber);
            }
        }

        // Inventory actions
        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.performed -= OnToggleInventoryPerformed;
        }

        if (rotateInventoryItemAction != null)
        {
            rotateInventoryItemAction.performed -= OnRotateInventoryItemPerformed;
        }
    }

    #endregion

    #region Event Handlers

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;

        Debug.Log("[InputManager] Pause input detected!");

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.isPaused)
                GameManager.Instance.ResumeGame();
            else
                GameManager.Instance.PauseGame();
        }
        else
        {
            Debug.LogWarning("[InputManager] GameManager.Instance is null - cannot handle pause");
        }
    }

    private void OnPrimaryActionPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        PrimaryActionPressed = true;
        OnPrimaryActionPressed?.Invoke();
    }

    private void OnPrimaryActionCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnPrimaryActionReleased?.Invoke();
    }

    private void OnSecondaryActionPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        SecondaryActionPressed = true;
        OnSecondaryActionPressed?.Invoke();
    }

    private void OnSecondaryActionCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnSecondaryActionReleased?.Invoke();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnInteractPressed?.Invoke();
    }

    private void OnToggleInventoryPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;

        if (GameManager.Instance?.uiManager != null)
        {
            if (GameManager.Instance.uiManager.isInventoryOpen)
            {
                GameEvents.TriggerInventoryClosed();
            }
            else
            {
                GameEvents.TriggerInventoryOpened();
            }
        }
    }

    private void OnRotateInventoryItemPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnRotateInventoryItemPressed?.Invoke();
    }

    private void OnLeftClickPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnLeftClickPressed?.Invoke();
    }

    private void OnRightClickPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnRightClickPressed?.Invoke();
    }

    private void OnScrollWheelPerformed(InputAction.CallbackContext context)
    {
        Vector2 scrollValue = context.ReadValue<Vector2>();
        OnScrollWheelInput?.Invoke(scrollValue);
    }

    private void OnHotkeyPerformed(int slotNumber)
    {
        if (isCleanedUp) return;
        OnHotkeyPressed?.Invoke(slotNumber);
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        if (isCleanedUp) return;

        // Update input values
        if (coreMovementActionMap?.enabled == true)
            UpdateCoreMovementInputValues();

        UpdateContextualInputValues();
    }

    private void UpdateCoreMovementInputValues()
    {
        MovementInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        LookInput = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
    }

    private void UpdateContextualInputValues()
    {
        // Update speed modifier based on current movement mode
        switch (currentMovementMode)
        {
            case MovementMode.Ground:
                SpeedModifierHeld = sprintAction?.IsPressed() ?? false;
                PrimaryActionHeld = jumpAction?.IsPressed() ?? false;
                SecondaryActionHeld = crouchAction?.IsPressed() ?? false;
                break;
            case MovementMode.Swimming:
                SpeedModifierHeld = swimSpeedAction?.IsPressed() ?? false;
                PrimaryActionHeld = surfaceAction?.IsPressed() ?? false;
                SecondaryActionHeld = diveAction?.IsPressed() ?? false;
                break;
        }

        // Reset pressed states after they've been read
        if (PrimaryActionPressed) PrimaryActionPressed = false;
        if (SecondaryActionPressed) SecondaryActionPressed = false;
    }

    #endregion

    #region Utility and Debug Methods

    public void SetInputEnabled(string actionName, bool enabled)
    {
        if (isCleanedUp) return;

        var action = currentMovementActionMap?.FindAction(actionName);
        if (action != null)
        {
            if (enabled)
                action.Enable();
            else
                action.Disable();
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugInputState()
    {
        Debug.Log("=== InputManager Debug Info ===");
        Debug.Log($"IsCleanedUp: {isCleanedUp}");
        Debug.Log($"IsFullyInitialized: {isFullyInitialized}");
        Debug.Log($"GameplayInputEnabled: {gameplayInputEnabled}");
        Debug.Log($"CurrentMovementMode: {currentMovementMode}");

        Debug.Log($"UI ActionMap: {uiActionMap?.name} - Enabled: {uiActionMap?.enabled}");
        Debug.Log($"Core Movement ActionMap: {coreMovementActionMap?.name} - Enabled: {coreMovementActionMap?.enabled}");
        Debug.Log($"Current Movement ActionMap: {currentMovementActionMap?.name} - Enabled: {currentMovementActionMap?.enabled}");
        Debug.Log($"Gameplay ActionMap: {gameplayActionMap?.name} - Enabled: {gameplayActionMap?.enabled}");

        Debug.Log($"Pause Action: {pauseAction?.name} - Enabled: {pauseAction?.enabled}");
        Debug.Log($"Current MovementInput: {MovementInput}");
        Debug.Log($"Current LookInput: {LookInput}");
        Debug.Log("==============================");
    }

    #endregion

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Debug.Log("[InputManager] Singleton destroyed");
            Instance = null;
        }
        Cleanup();
    }
}