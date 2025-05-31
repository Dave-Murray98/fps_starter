using System;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Header("Input Acitons")]
    public InputActionAsset inputActions;


    //Input Actions
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction pauseAction;


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
    public event Action OnPausePressed;

    private InputActionMap locomotionActionMap;
    private InputActionMap uiActionMap;


    // Utility methods for other systems
    public bool IsMoving() => MovementInput.magnitude > 0.1f;
    public bool IsLooking() => LookInput.magnitude > 0.1f;

    // Methods to temporarily disable specific inputs (useful for cutscenes, etc.)
    public void SetInputEnabled(string actionName, bool enabled)
    {
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
        Debug.Log("InputManager Initialized");

        SetupInputActions();
        EnableLocomotionInput();

        GameEvents.OnGamePaused += DisableLocomotionInput;
        GameEvents.OnGameResumed += EnableLocomotionInput;
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

        if (locomotionActionMap == null || uiActionMap == null)
        {
            Debug.LogError("Locomotion or UI action map not found in InputActionAsset.");
            return;
        }

        moveAction = locomotionActionMap.FindAction("Move");
        lookAction = locomotionActionMap.FindAction("Look");
        jumpAction = locomotionActionMap.FindAction("Jump");
        sprintAction = locomotionActionMap.FindAction("Sprint");
        crouchAction = locomotionActionMap.FindAction("Crouch");
        pauseAction = uiActionMap.FindAction("Pause");

        if (jumpAction != null)
        {
            jumpAction.performed += ctx => { JumpPressed = true; OnJumpPressed?.Invoke(); };
            jumpAction.canceled += ctx => OnJumpReleased?.Invoke();
        }

        if (crouchAction != null)
        {
            crouchAction.performed += ctx => { CrouchPressed = true; OnCrouchPressed?.Invoke(); };
            crouchAction.canceled += ctx => OnCrouchReleased?.Invoke();
        }

        if (pauseAction != null)
        {
            pauseAction.performed += OnPausePerformed;
        }

    }

    private void Update()
    {
        if (locomotionActionMap?.enabled == true)
            UpdateLocomotionInputValues();
    }

    private void UpdateLocomotionInputValues()
    {
        MovementInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        LookInput = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;

        JumpHeld = jumpAction?.IsPressed() ?? false;
        SprintHeld = sprintAction?.IsPressed() ?? false;

        // Reset one-frame flags at end of frame
        if (JumpPressed) JumpPressed = false; // Reset after reading
        if (CrouchPressed) CrouchPressed = false; // Reset after reading
    }


    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        OnPausePressed?.Invoke();

        if (GameManager.Instance.isPaused)
            GameManager.Instance.ResumeGame();
        else
            GameManager.Instance.PauseGame();
    }

    public void EnableLocomotionInput()
    {
        locomotionActionMap?.Enable();
        uiActionMap?.Enable();
    }

    public void DisableLocomotionInput()
    {
        locomotionActionMap?.Disable();
    }


    private void OnDestroy()
    {
        GameEvents.OnGamePaused -= DisableLocomotionInput;
        GameEvents.OnGameResumed -= EnableLocomotionInput;

        if (jumpAction != null)
        {
            jumpAction.performed -= ctx => { JumpPressed = true; OnJumpPressed?.Invoke(); };
            jumpAction.canceled -= ctx => OnJumpReleased?.Invoke();
        }

        if (crouchAction != null)
        {
            crouchAction.performed -= ctx => { CrouchPressed = true; OnCrouchPressed?.Invoke(); };
            crouchAction.canceled -= ctx => OnCrouchReleased?.Invoke();
        }

        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
        }

    }
}
