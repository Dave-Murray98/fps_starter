using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour, IManager
{
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
    private InputAction surfaceAction;      // Same binding as Jump
    private InputAction swimSpeedAction;    // Same binding as Sprint
    private InputAction diveAction;         // Different binding from Crouch

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
    // Core movement input - always available during gameplay
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    // Context-specific input states
    public bool PrimaryActionPressed { get; private set; }    // Jump/Surface
    public bool PrimaryActionHeld { get; private set; }       // Jump/Surface held
    public bool SpeedModifierHeld { get; private set; }       // Sprint/SwimSpeed
    public bool SecondaryActionPressed { get; private set; }  // Crouch/Dive
    public bool SecondaryActionHeld { get; private set; }     // Crouch/Dive held

    #endregion

    #region Events
    // Core movement events
    public event Action OnPrimaryActionPressed;    // Jump/Surface
    public event Action OnPrimaryActionReleased;
    public event Action OnSecondaryActionPressed;  // Crouch/Dive
    public event Action OnSecondaryActionReleased;
    public event Action OnInteractPressed;

    // Inventory events
    public event Action OnRotateInventoryItemPressed;

    // Equipment system events
    public event Action OnLeftClickPressed;
    public event Action OnRightClickPressed;
    public System.Action<Vector2> OnScrollWheelInput;
    public System.Action<int> OnHotkeyPressed;

    // Event for when InputManager is ready
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

    // Utility methods for other systems
    public bool IsMoving() => MovementInput.magnitude > 0.1f;
    public bool IsLooking() => LookInput.magnitude > 0.1f;

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

    #region IManager Implementation

    public void Initialize()
    {
        isCleanedUp = false;
        SetupInputActions();

        // CRITICAL FIX: Enable UI actions first (always active)
        if (uiActionMap != null)
        {
            uiActionMap.Enable();
            Debug.Log("[InputManager] UI ActionMap enabled");
        }

        // CRITICAL FIX: Enable core movement actions
        if (coreMovementActionMap != null)
        {
            coreMovementActionMap.Enable();
            Debug.Log("[InputManager] Core Movement ActionMap enabled");
        }

        // CRITICAL FIX: Set initial movement mode BEFORE enabling gameplay input
        currentMovementMode = MovementMode.Ground;
        currentMovementActionMap = groundLocomotionActionMap;

        // CRITICAL FIX: Now enable gameplay input which will enable the current movement action map
        EnableGameplayInput();

        // Subscribe to game events
        GameEvents.OnGamePaused += DisableGameplayInput;
        GameEvents.OnGameResumed += EnableGameplayInput;

        Debug.Log($"[InputManager] Initialize complete - Current mode: {currentMovementMode}, ActionMap: {currentMovementActionMap?.name}");

        // Notify that InputManager is ready
        StartCoroutine(NotifyInputManagerReady());
    }

    /// <summary>
    /// FIXED: Ensures input manager ready notification happens after full setup
    /// </summary>
    private System.Collections.IEnumerator NotifyInputManagerReady()
    {
        yield return null; // Wait one frame to ensure everything is set up

        // Notify that InputManager is ready
        OnInputManagerReady?.Invoke(this);
        Debug.Log($"[InputManager] Ready notification sent - ID: {GetInstanceID()}");
    }

    /// <summary>
    /// CRITICAL FIX: Enhanced RefreshReferences to ensure proper ActionMap setup
    /// </summary>
    public void RefreshReferences()
    {
        if (!isCleanedUp)
        {
            Debug.Log("[InputManager] RefreshReferences called - re-enabling gameplay input");

            // CRITICAL FIX: Ensure we're in the correct movement mode
            if (currentMovementActionMap == null)
            {
                Debug.LogWarning("[InputManager] currentMovementActionMap is null during refresh - resetting to ground");
                currentMovementMode = MovementMode.Ground;
                currentMovementActionMap = groundLocomotionActionMap;
            }

            EnableGameplayInput();
            OnInputManagerReady?.Invoke(this);
        }
    }


    public void Cleanup()
    {
        isCleanedUp = true;

        // Clear all events to prevent calling methods on destroyed objects
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

        // Unsubscribe from game events
        GameEvents.OnGamePaused -= DisableGameplayInput;
        GameEvents.OnGameResumed -= EnableGameplayInput;

        // Disable and clean up input actions
        DisableAllInputActions();
        UnsubscribeFromInputActions();
    }

    #endregion

    #region Movement Mode Management

    /// <summary>
    /// CRITICAL FIX: Enhanced movement mode switching with explicit ActionMap management
    /// </summary>
    public void SetMovementMode(MovementMode mode)
    {
        if (currentMovementMode == mode) return;

        Debug.Log($"[InputManager] Switching movement mode: {currentMovementMode} -> {mode}");

        // CRITICAL FIX: Properly disable current movement action map
        if (currentMovementActionMap != null)
        {
            currentMovementActionMap.Disable();
            Debug.Log($"[InputManager] Disabled ActionMap: {currentMovementActionMap.name}");
        }

        // CRITICAL FIX: Set new movement action map
        switch (mode)
        {
            case MovementMode.Ground:
                currentMovementActionMap = groundLocomotionActionMap;
                break;
            case MovementMode.Swimming:
                currentMovementActionMap = swimmingActionMap;
                break;
        }

        currentMovementMode = mode;

        // CRITICAL FIX: Enable new movement action map if gameplay input is enabled
        if (gameplayInputEnabled && currentMovementActionMap != null)
        {
            currentMovementActionMap.Enable();
            Debug.Log($"[InputManager] Enabled ActionMap: {currentMovementActionMap.name}");
        }
        else
        {
            Debug.LogWarning($"[InputManager] Could not enable ActionMap - gameplayInputEnabled: {gameplayInputEnabled}, currentMovementActionMap: {currentMovementActionMap?.name ?? "null"}");
        }

        Debug.Log($"[InputManager] Movement mode change complete: {mode}");
    }

    /// <summary>
    /// Gets the current movement mode
    /// </summary>
    public MovementMode GetCurrentMovementMode() => currentMovementMode;

    #endregion

    #region Input State Management

    /// <summary>
    /// Disables all gameplay input (called when paused)
    /// </summary>
    public void DisableGameplayInput()
    {
        if (isCleanedUp) return;

        gameplayInputEnabled = false;
        coreMovementActionMap?.Disable();
        currentMovementActionMap?.Disable();
        gameplayActionMap?.Disable();
        inventoryActionMap?.Disable();

        Debug.Log("[InputManager] Gameplay input disabled");
    }

    /// <summary>
    /// CRITICAL FIX: Enhanced gameplay input enabling with explicit ActionMap control
    /// </summary>
    public void EnableGameplayInput()
    {
        if (isCleanedUp) return;

        gameplayInputEnabled = true;

        // Enable core systems first
        if (coreMovementActionMap != null)
        {
            coreMovementActionMap.Enable();
            Debug.Log("[InputManager] Core Movement ActionMap enabled");
        }

        // CRITICAL FIX: Enable current movement action map
        if (currentMovementActionMap != null)
        {
            currentMovementActionMap.Enable();
            Debug.Log($"[InputManager] Current Movement ActionMap enabled: {currentMovementActionMap.name}");
        }
        else
        {
            Debug.LogError("[InputManager] currentMovementActionMap is null! This should not happen.");

            // EMERGENCY FIX: Force set to ground locomotion if null
            if (groundLocomotionActionMap != null)
            {
                currentMovementActionMap = groundLocomotionActionMap;
                currentMovementActionMap.Enable();
                Debug.Log("[InputManager] EMERGENCY: Forced ground locomotion ActionMap");
            }
        }

        // Enable other gameplay systems
        if (gameplayActionMap != null)
        {
            gameplayActionMap.Enable();
            Debug.Log("[InputManager] Gameplay ActionMap enabled");
        }

        if (inventoryActionMap != null)
        {
            inventoryActionMap.Enable();
            Debug.Log("[InputManager] Inventory ActionMap enabled");
        }

        Debug.Log($"[InputManager] EnableGameplayInput complete - Current mode: {currentMovementMode}");

        // CRITICAL FIX: Verify all expected ActionMaps are enabled
        VerifyActionMapStates();
    }

    /// <summary>
    /// CRITICAL FIX: Verification method to ensure ActionMaps are in expected states
    /// </summary>
    private void VerifyActionMapStates()
    {
        Debug.Log("=== INPUT MANAGER ACTIONMAP VERIFICATION ===");

        if (uiActionMap != null)
            Debug.Log($"UI ActionMap: {uiActionMap.name} - Enabled: {uiActionMap.enabled}");

        if (coreMovementActionMap != null)
            Debug.Log($"Core Movement ActionMap: {coreMovementActionMap.name} - Enabled: {coreMovementActionMap.enabled}");

        if (groundLocomotionActionMap != null)
            Debug.Log($"Ground Locomotion ActionMap: {groundLocomotionActionMap.name} - Enabled: {groundLocomotionActionMap.enabled}");

        if (swimmingActionMap != null)
            Debug.Log($"Swimming ActionMap: {swimmingActionMap.name} - Enabled: {swimmingActionMap.enabled}");

        if (gameplayActionMap != null)
            Debug.Log($"Gameplay ActionMap: {gameplayActionMap.name} - Enabled: {gameplayActionMap.enabled}");

        if (inventoryActionMap != null)
            Debug.Log($"Inventory ActionMap: {inventoryActionMap.name} - Enabled: {inventoryActionMap.enabled}");

        Debug.Log($"Current Movement Mode: {currentMovementMode}");
        Debug.Log($"Current Movement ActionMap: {currentMovementActionMap?.name ?? "NULL"} - Enabled: {currentMovementActionMap?.enabled ?? false}");

        // CRITICAL CHECK: Verify ground locomotion actions
        if (currentMovementMode == MovementMode.Ground)
        {
            if (jumpAction != null)
                Debug.Log($"Jump Action: {jumpAction.name} - Enabled: {jumpAction.enabled} - ActionMap Enabled: {jumpAction.actionMap?.enabled ?? false}");

            if (sprintAction != null)
                Debug.Log($"Sprint Action: {sprintAction.name} - Enabled: {sprintAction.enabled} - ActionMap Enabled: {sprintAction.actionMap?.enabled ?? false}");

            if (crouchAction != null)
                Debug.Log($"Crouch Action: {crouchAction.name} - Enabled: {crouchAction.enabled} - ActionMap Enabled: {crouchAction.actionMap?.enabled ?? false}");
        }

        Debug.Log("============================================");
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

    private void SetupInputActions()
    {
        if (inputActions == null)
        {
            Debug.LogError("InputActionAsset is not assigned in InputManager.");
            return;
        }

        // Get action maps
        uiActionMap = inputActions.FindActionMap("UI");
        coreMovementActionMap = inputActions.FindActionMap("CoreMovement");
        groundLocomotionActionMap = inputActions.FindActionMap("GroundLocomotion");
        swimmingActionMap = inputActions.FindActionMap("Swimming");
        gameplayActionMap = inputActions.FindActionMap("Gameplay");
        inventoryActionMap = inputActions.FindActionMap("Inventory");

        if (uiActionMap == null || coreMovementActionMap == null ||
            groundLocomotionActionMap == null || swimmingActionMap == null ||
            gameplayActionMap == null || inventoryActionMap == null)
        {
            Debug.LogError("Required action maps not found in InputActionAsset.");
            return;
        }

        SetupUIInputActions();
        SetupCoreMovementInputActions();
        SetupGroundLocomotionInputActions();
        SetupSwimmingInputActions();
        SetupGameplayInputActions();
        SetupInventoryInputActions();

        SubscribeToInputActions();

        Debug.Log("Input actions set up successfully");
    }

    private void SetupUIInputActions()
    {
        pauseAction = uiActionMap.FindAction("Pause");
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
        interactAction = gameplayActionMap?.FindAction("Interact");
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

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.isPaused)
                GameManager.Instance.ResumeGame();
            else
                GameManager.Instance.PauseGame();
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

        if (GameManager.Instance.uiManager.isInventoryOpen)
        {
            GameEvents.TriggerInventoryClosed();
        }
        else
        {
            GameEvents.TriggerInventoryOpened();
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

    private void OnDestroy()
    {
        Cleanup();
    }

    // Debug method for troubleshooting
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugInputState()
    {
        Debug.Log($"=== InputManager Debug Info (ID: {GetInstanceID()}) ===");
        Debug.Log($"IsCleanedUp: {isCleanedUp}");
        Debug.Log($"GameplayInputEnabled: {gameplayInputEnabled}");
        Debug.Log($"CurrentMovementMode: {currentMovementMode}");
        Debug.Log($"CoreMovementActionMap: {coreMovementActionMap?.name} - Enabled: {coreMovementActionMap?.enabled}");
        Debug.Log($"CurrentMovementActionMap: {currentMovementActionMap?.name} - Enabled: {currentMovementActionMap?.enabled}");
        Debug.Log($"Current MovementInput: {MovementInput}");
        Debug.Log($"Current LookInput: {LookInput}");
        Debug.Log("==============================================");
    }
}