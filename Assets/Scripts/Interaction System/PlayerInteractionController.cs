using UnityEngine;

/// <summary>
/// Handles player interactions - integrates with existing PlayerController system
/// </summary>
[RequireComponent(typeof(PlayerInteractionDetector))]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private bool canInteract = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Components
    private PlayerController playerController;
    private PlayerInteractionDetector interactionDetector;
    private InputManager inputManager;

    // Current state
    private bool isInteracting = false;

    // Events
    public System.Action<IInteractable> OnInteractionStarted;
    public System.Action<IInteractable, bool> OnInteractionCompleted;

    public bool CanInteract => canInteract && !isInteracting;
    public bool IsInteracting => isInteracting;
    public IInteractable CurrentInteractable => interactionDetector?.CurrentBestInteractable;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        interactionDetector = GetComponent<PlayerInteractionDetector>();
    }

    private void Start()
    {
        // Subscribe to events
        GameManager.OnManagersRefreshed += RefreshReferences;
        InputManager.OnInputManagerReady += OnInputManagerReady;

        RefreshReferences();
    }

    private void RefreshReferences()
    {
        inputManager = GameManager.Instance?.inputManager;
        ConnectToInputManager();
    }

    private void OnInputManagerReady(InputManager newInputManager)
    {
        ConnectToInputManager();
    }

    private void ConnectToInputManager()
    {
        // Disconnect from previous input manager
        if (inputManager != null)
        {
            inputManager.OnInteractPressed -= HandleInteractInput;
        }

        // Connect to current input manager
        inputManager = GameManager.Instance?.inputManager;
        if (inputManager != null)
        {
            inputManager.OnInteractPressed += HandleInteractInput;
            DebugLog($"Connected to InputManager: {inputManager.GetInstanceID()}");
        }
    }

    private void HandleInteractInput()
    {
        if (!CanInteract)
        {
            DebugLog("Interaction input received but interaction is disabled");
            return;
        }

        // Check if we're paused
        if (GameManager.Instance != null && GameManager.Instance.isPaused)
        {
            DebugLog("Interaction blocked - game is paused");
            return;
        }

        DebugLog("Interaction input received - attempting interaction");
        TryInteract();
    }

    /// <summary>
    /// Attempt to interact with the current best interactable
    /// </summary>
    public bool TryInteract()
    {
        if (!CanInteract || interactionDetector == null)
        {
            DebugLog("Cannot interact - disabled or no detector");
            return false;
        }

        var targetInteractable = interactionDetector.CurrentBestInteractable;
        if (targetInteractable == null)
        {
            DebugLog("No interactable in range");
            return false;
        }

        DebugLog($"Attempting interaction with: {targetInteractable.InteractableID}");

        // Start interaction
        isInteracting = true;
        OnInteractionStarted?.Invoke(targetInteractable);

        // Perform the interaction
        bool success = interactionDetector.TryInteract();

        // Complete interaction
        isInteracting = false;
        OnInteractionCompleted?.Invoke(targetInteractable, success);

        DebugLog($"Interaction {(success ? "succeeded" : "failed")} with: {targetInteractable.InteractableID}");
        return success;
    }

    /// <summary>
    /// Enable or disable interaction capability
    /// </summary>
    public void SetInteractionEnabled(bool enabled)
    {
        canInteract = enabled;
        DebugLog($"Interaction {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Get the current interaction prompt text
    /// </summary>
    public string GetCurrentInteractionPrompt()
    {
        if (!CanInteract || interactionDetector == null)
            return "";

        return interactionDetector.GetCurrentInteractionPrompt();
    }

    /// <summary>
    /// Check if there's an interactable in range
    /// </summary>
    public bool HasInteractableInRange()
    {
        return interactionDetector != null && interactionDetector.HasInteractableInRange;
    }

    /// <summary>
    /// Force refresh the interaction detection
    /// </summary>
    public void RefreshInteractionDetection()
    {
        interactionDetector?.ForceUpdate();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerInteractionController] {message}");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        GameManager.OnManagersRefreshed -= RefreshReferences;
        InputManager.OnInputManagerReady -= OnInputManagerReady;

        if (inputManager != null)
        {
            inputManager.OnInteractPressed -= HandleInteractInput;
        }
    }
}