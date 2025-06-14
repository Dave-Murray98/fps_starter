using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour, IManager
{
    [Header("Input Actions")]
    public InputActionAsset inputActions;

    //Input Actions
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction pauseAction;
    private InputAction interactAction;
    private InputAction toggleInventoryAction;
    private InputAction rotateInventoryItemAction; // For rotating items in the inventory

    //Input State - other systems will read these 
    public Vector2 MovementInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool CrouchPressed { get; private set; }
    public bool CrouchHeld { get; private set; }


    //input events
    public event Action OnJumpPressed;
    public event Action OnJumpReleased;
    public event Action OnCrouchPressed;
    public event Action OnCrouchReleased;
    public event Action OnInteractPressed;


    public event Action OnRotateInventoryItemPressed; // For rotating items in the inventory (will be used by the DraggableGridItem script to rotate items)

    // Event for when InputManager is ready
    public static event Action<InputManager> OnInputManagerReady;

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

        moveAction = locomotionActionMap.FindAction("Move");
        lookAction = locomotionActionMap.FindAction("Look");
        jumpAction = locomotionActionMap.FindAction("Jump");
        sprintAction = locomotionActionMap.FindAction("Sprint");
        crouchAction = locomotionActionMap.FindAction("Crouch");
        interactAction = gameplayActionMap?.FindAction("Interact");
        pauseAction = uiActionMap.FindAction("Pause");
        toggleInventoryAction = uiActionMap.FindAction("ToggleInventory");
        rotateInventoryItemAction = uiActionMap.FindAction("RotateInventoryItem");

        SubscribeToInputActions();

        //  Debug.Log("Input actions set up successfully");
    }

    private void SubscribeToInputActions()
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

        if (pauseAction != null)
        {
            pauseAction.performed += OnPausePerformed;
        }

        if (interactAction != null)
        {
            interactAction.performed += OnInteractPerformed;
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

    private void UnsubscribeFromInputActions()
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

        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
        }

        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
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