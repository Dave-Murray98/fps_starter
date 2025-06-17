using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour, IManager
{
    #region Fields
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    [Header("Locomotion Actions")]
    //Input Actions
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;

    [Header("UI Actions")]
    private InputAction pauseAction;
    private InputAction toggleInventoryAction;
    private InputAction rotateInventoryItemAction; // For rotating items in the inventory

    [Header("Gameplay Actions")]
    private InputAction interactAction;
    private InputAction leftClickAction;
    private InputAction rightClickAction;
    private InputAction scrollWheelAction;
    private InputAction[] hotkeyActions = new InputAction[10];

    #endregion

    #region Public Properties
    //Input State - other systems will read these 
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CrouchPressed { get; private set; }
    public bool CrouchHeld { get; private set; }

    #endregion

    #region Events
    //input events
    public event Action OnJumpPressed;
    public event Action OnJumpReleased;
    public event Action OnCrouchPressed;
    public event Action OnCrouchReleased;
    public event Action OnInteractPressed;


    public event Action OnRotateInventoryItemPressed; // For rotating items in the inventory (will be used by the DraggableGridItem script to rotate items)

    // Events for equipment system
    public event Action OnLeftClickPressed;
    public event Action OnRightClickPressed;
    public System.Action<Vector2> OnScrollWheelInput;
    public System.Action<int> OnHotkeyPressed; // Optional

    // Event for when InputManager is ready
    public static event Action<InputManager> OnInputManagerReady;

    #endregion

    private InputActionMap locomotionActionMap;
    private InputActionMap uiActionMap;
    private InputActionMap gameplayActionMap; //for player actions (ie interacting, attacking, using items, etc)

    // Track if we're cleaned up to prevent calling events on destroyed objects
    private bool isCleanedUp = false;

    // Utility methods for other systems
    public bool IsMoving() => MovementInput.magnitude > 0.1f;
    public bool IsLooking() => LookInput.magnitude > 0.1f;

    public void SetInputEnabled(string actionName, bool enabled)
    {
        if (isCleanedUp) return;

        var action = locomotionActionMap?.FindAction(actionName);
        if (action != null)
        {
            if (enabled)
                action.Enable();
            else
                action.Disable();
        }
    }

    public void Initialize()
    {
        //        Debug.Log($"InputManager Initialized - Instance ID: {GetInstanceID()}");
        isCleanedUp = false;

        SetupInputActions();

        // CRITICAL: Always enable input actions after setup
        EnableAllInputActions();

        // Subscribe to game events
        GameEvents.OnGamePaused += DisableLocomotionAndGameplayInput;
        GameEvents.OnGameResumed += ReenableAllInput;

        // Notify that InputManager is ready
        OnInputManagerReady?.Invoke(this);
    }

    public void RefreshReferences()
    {
        //    Debug.Log($"InputManager: Refreshing references - Instance ID: {GetInstanceID()}");

        // CRITICAL: Ensure input actions are enabled when refreshing
        if (!isCleanedUp)
        {
            ReenableAllInput();
            OnInputManagerReady?.Invoke(this);
        }
    }

    public void Cleanup()
    {
        //   Debug.Log($"InputManager: Cleaning up - Instance ID: {GetInstanceID()}");

        // Mark as cleaned up to prevent event calls
        isCleanedUp = true;

        // Clear all events to prevent calling methods on destroyed objects
        OnJumpPressed = null;
        OnJumpReleased = null;
        OnCrouchPressed = null;
        OnCrouchReleased = null;
        //OnPausePressed = null;

        // Unsubscribe from game events
        GameEvents.OnGamePaused -= DisableLocomotionAndGameplayInput;
        GameEvents.OnGameResumed -= ReenableAllInput;

        // Disable and clean up input actions
        DisableAllInputActions();
        UnsubscribeFromInputActions();
    }

    #region Setup

    private void SetupInputActions()
    {
        if (inputActions == null)
        {
            Debug.LogError("InputActionAsset is not assigned in InputManager.");
            return;
        }

        locomotionActionMap = inputActions.FindActionMap("Locomotion");
        uiActionMap = inputActions.FindActionMap("UI");
        gameplayActionMap = inputActions.FindActionMap("Gameplay");

        if (locomotionActionMap == null || uiActionMap == null || gameplayActionMap == null)
        {
            Debug.LogError("Locomotion, UI or Gameplay action map not found in InputActionAsset.");
            return;
        }

        SetupLocomotionInputActions();
        SetupUIInputActions();
        SetupGameplayInputActions();

        SubscribeToInputActions();

        //  Debug.Log("Input actions set up successfully");
    }

    private void SetupLocomotionInputActions()
    {
        if (locomotionActionMap == null) return;

        moveAction = locomotionActionMap.FindAction("Move");
        lookAction = locomotionActionMap.FindAction("Look");
        jumpAction = locomotionActionMap.FindAction("Jump");
        sprintAction = locomotionActionMap.FindAction("Sprint");
        crouchAction = locomotionActionMap.FindAction("Crouch");
    }

    private void SetupUIInputActions()
    {
        if (uiActionMap == null) return;

        pauseAction = uiActionMap.FindAction("Pause");
        toggleInventoryAction = uiActionMap.FindAction("ToggleInventory");
        rotateInventoryItemAction = uiActionMap.FindAction("RotateInventoryItem");

        // Debug.Log("UI input actions set up successfully");
    }

    private void SetupGameplayInputActions()
    {
        if (gameplayActionMap == null) return;

        interactAction = gameplayActionMap?.FindAction("Interact");

        // Mouse actions
        leftClickAction = gameplayActionMap.FindAction("LeftClick");
        rightClickAction = gameplayActionMap.FindAction("RightClick");
        scrollWheelAction = gameplayActionMap.FindAction("ScrollWheel");

        // Hotkey actions (you'll need to add these to your Input Action Asset)
        for (int i = 1; i <= 10; i++)
        {
            string actionName = i == 10 ? "Hotkey0" : $"Hotkey{i}";
            hotkeyActions[i - 1] = gameplayActionMap.FindAction(actionName);
        }
    }

    #endregion

    #region Subscription and Unsubscription

    private void SubscribeToInputActions()
    {

        SubscribeLocomotionInputActions();
        SubscribeToUIInputActions();
        SubscribeToGameplayInputActions();
    }

    private void SubscribeLocomotionInputActions()
    {
        if (jumpAction != null)
        {
            jumpAction.performed += OnJumpPerformed;
            jumpAction.canceled += OnJumpCanceled;
        }

        if (crouchAction != null)
        {
            crouchAction.performed += OnCrouchPerformed;
            crouchAction.canceled += OnCrouchCanceled;
        }
    }

    private void SubscribeToUIInputActions()
    {
        if (pauseAction != null)
        {
            pauseAction.performed += OnPausePerformed;
        }

        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.performed += OnToggleInventoryPerformed;
        }

        if (rotateInventoryItemAction != null)
        {
            rotateInventoryItemAction.performed += OnRotateInventoryItemPerformed;
        }
    }

    private void SubscribeToGameplayInputActions()
    {

        if (interactAction != null)
        {
            interactAction.performed += OnInteractPerformed;
        }

        //mouse actions
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
                int slotNumber = i + 1; // Capture for closure
                hotkeyActions[i].performed += _ => OnHotkeyPerformed(slotNumber);
            }
        }
    }

    private void UnsubscribeFromInputActions()
    {
        UnsubscribeFromLocomotionInputActions();
        UnsubscribeFromUIInputActions();
        UnsubscribeFromGameplayInputActions();

    }

    private void UnsubscribeFromLocomotionInputActions()
    {
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPerformed;
            jumpAction.canceled -= OnJumpCanceled;
        }

        if (crouchAction != null)
        {
            crouchAction.performed -= OnCrouchPerformed;
            crouchAction.canceled -= OnCrouchCanceled;
        }
    }

    private void UnsubscribeFromUIInputActions()
    {
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
        }

        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.performed -= OnToggleInventoryPerformed;
        }

        if (rotateInventoryItemAction != null)
        {
            rotateInventoryItemAction.performed -= OnRotateInventoryItemPerformed;
        }
    }

    private void UnsubscribeFromGameplayInputActions()
    {

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

        // Unsubscribe from hotkey actions
        for (int i = 0; i < hotkeyActions.Length; i++)
        {
            if (hotkeyActions[i] != null)
            {
                int slotNumber = i + 1;
                hotkeyActions[i].performed -= _ => OnHotkeyPerformed(slotNumber);
            }
        }
    }

    #endregion

    #region Event Handlers

    // Safe event handlers that check if cleaned up
    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp)
        {
            Debug.LogWarning("InputManager is cleaned up, cannot process jump input.");
            return;
        }

        // Debug.Log("Jump action performed");

        JumpPressed = true;
        OnJumpPressed?.Invoke();
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnJumpReleased?.Invoke();
    }

    private void OnCrouchPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        CrouchPressed = true;
        OnCrouchPressed?.Invoke();
    }

    private void OnCrouchCanceled(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnCrouchReleased?.Invoke();
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        // OnPausePressed?.Invoke();

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.isPaused)
                GameManager.Instance.ResumeGame();
            else
                GameManager.Instance.PauseGame();
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        OnInteractPressed?.Invoke();
    }

    private void OnToggleInventoryPerformed(InputAction.CallbackContext context)
    {
        if (isCleanedUp) return;
        //OnToggleInventoryPressed?.Invoke();
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
        Vector2 scrollValue = context.ReadValue<Vector2>(); // Fix for your error!
        OnScrollWheelInput?.Invoke(scrollValue);
    }

    private void OnHotkeyPerformed(int slotNumber)
    {
        if (isCleanedUp) return;
        OnHotkeyPressed?.Invoke(slotNumber);
    }

    #endregion

    private void Update()
    {
        if (isCleanedUp) return;

        if (locomotionActionMap?.enabled == true)
            UpdateLocomotionInputValues();
    }

    private void UpdateLocomotionInputValues()
    {
        if (isCleanedUp) return;

        MovementInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        LookInput = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;

        JumpHeld = jumpAction?.IsPressed() ?? false;
        SprintHeld = sprintAction?.IsPressed() ?? false;

        if (JumpPressed) JumpPressed = false;
        if (CrouchPressed) CrouchPressed = false;
    }

    #region public enabling and disabling methods

    public void ReenableAllInput()
    {
        if (isCleanedUp)
        {
            Debug.Log("InputManager is cleaned up, cannot enable All input.");
            return;
        }

        //  Debug.Log("Enabling all input action maps");

        // CRITICAL: Ensure action maps are valid before enabling
        if (locomotionActionMap == null || uiActionMap == null)
        {
            //Debug.LogWarning("Action maps are null, attempting to re-setup input actions");
            SetupInputActions();
        }

        EnableAllInputActions();
    }

    public void DisableLocomotionAndGameplayInput()
    {
        if (isCleanedUp) return;

        //    Debug.Log("Disabling LOCOMOTION input actions");
        locomotionActionMap?.Disable();
        gameplayActionMap?.Disable();
    }

    private void DisableAllInputActions()
    {
        //        Debug.Log("Disabling all input actions");
        locomotionActionMap?.Disable();
        uiActionMap?.Disable();
        gameplayActionMap?.Disable();
    }

    private void EnableAllInputActions()
    {
        //        Debug.Log("Enabling all input actions");
        locomotionActionMap?.Enable();
        uiActionMap?.Enable();
        gameplayActionMap?.Enable();
    }

    public void EnableUIInput()
    {
        if (isCleanedUp) return;

        // Debug.Log("Enabling UI input actions");
        uiActionMap?.Enable();
    }

    public void DisableUIInput()
    {
        if (isCleanedUp) return;

        // Debug.Log("Disabling UI input actions");
        uiActionMap?.Disable();
    }

    #endregion

    private void OnDestroy()
    {
        Cleanup();
    }

    // Add a debug method to help troubleshoot
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugInputState()
    {
        Debug.Log($"=== InputManager Debug Info (ID: {GetInstanceID()}) ===");
        Debug.Log($"IsCleanedUp: {isCleanedUp}");
        Debug.Log($"LocomotionActionMap: {locomotionActionMap?.name} - Enabled: {locomotionActionMap?.enabled}");
        Debug.Log($"UIActionMap: {uiActionMap?.name} - Enabled: {uiActionMap?.enabled}");
        Debug.Log($"JumpAction: {jumpAction?.name} - Enabled: {jumpAction?.enabled}");
        Debug.Log($"MoveAction: {moveAction?.name} - Enabled: {moveAction?.enabled}");
        Debug.Log($"Current MovementInput: {MovementInput}");
        Debug.Log($"Current LookInput: {LookInput}");
        Debug.Log("==============================================");
    }
}