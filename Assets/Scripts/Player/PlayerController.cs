using UnityEngine;


public enum MovementState
{
    Idle,
    Walking,
    Running,
    Crouching,
    Jumping,
    Falling,
    Landing
}

public enum GroundType
{
    Default,
    Grass,
    Stone,
    Metal,
    Wood,
    Water
    // Add more surface types as needed
}


[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerCamera))]
[RequireComponent(typeof(PlayerAudio))]
public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    public PlayerMovement movement;
    public PlayerCamera playerCamera;
    public PlayerAudio playerAudio;

    [Header("State")]
    public MovementState currentState = MovementState.Idle;
    public MovementState previousState = MovementState.Idle;

    [Header("Abilities")]
    public bool canMove = true;
    public bool canJump = true;
    public bool canSprint = true;
    public bool canCrouch = true;
    public bool canLook = true;

    private InputManager inputManager;
    private PlayerData playerData;

    // Events
    public event System.Action<MovementState, MovementState> OnStateChanged;

    private void Awake()
    {
        // Get or find components
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (playerCamera == null) playerCamera = GetComponent<PlayerCamera>();
        if (playerAudio == null) playerAudio = GetComponent<PlayerAudio>();
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Get references
        inputManager = GameManager.Instance.inputManager;
        playerData = GameManager.Instance.playerData;

        // Initialize components
        movement.Initialize(this);
        playerCamera.Initialize(this);
        playerAudio.Initialize(this);

        // ALWAYS re-subscribe to input events (even if already subscribed)
        SubscribeToInputEvents();

        // Lock cursor for first-person
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("PlayerController initialized");
    }

    private void SubscribeToInputEvents()
    {
        if (inputManager != null)
        {
            // Unsubscribe first to prevent double subscription
            inputManager.OnJumpPressed -= HandleJumpInput;
            inputManager.OnCrouchPressed -= HandleCrouchInput;
            inputManager.OnCrouchReleased -= HandleCrouchReleased;

            // Subscribe to input events
            inputManager.OnJumpPressed += HandleJumpInput;
            inputManager.OnCrouchPressed += HandleCrouchInput;
            inputManager.OnCrouchReleased += HandleCrouchReleased;

            Debug.Log("PlayerController subscribed to input events");
        }
        else
        {
            Debug.LogWarning("InputManager is null in PlayerController!");
        }
    }

    public void RefreshComponentReferences()
    {
        // Re-get references in case they changed
        inputManager = GameManager.Instance?.inputManager;
        playerData = GameManager.Instance?.playerData;

        // Re-subscribe to input events
        SubscribeToInputEvents();

        // NEW: Start a coroutine to save position after player has moved away from spawn
        StartCoroutine(SavePositionAfterMovement());

        Debug.Log("[PlayerController] Component references refreshed");
    }

    private System.Collections.IEnumerator SavePositionAfterMovement()
    {
        Vector3 initialPosition = transform.position;

        // Wait a bit for player to potentially move
        yield return new WaitForSeconds(2f);

        // Check if player has moved significantly from spawn
        float distanceMoved = Vector3.Distance(initialPosition, transform.position);

        if (distanceMoved > 1f) // If player moved more than 1 unit
        {
            SavePositionToSaveSystem();
            Debug.Log($"[PlayerController] Position saved after movement: {transform.position}");
        }
        else
        {
            Debug.Log("[PlayerController] Player hasn't moved much, not saving spawn position");
        }
    }

    private void SavePositionToSaveSystem()
    {
        // Update SaveManager data
        if (SaveManager.Instance != null && SaveManager.Instance.CurrentGameData != null)
        {
            if (SaveManager.Instance.CurrentGameData.playerData == null)
            {
                SaveManager.Instance.CurrentGameData.playerData = new PlayerSaveData();
            }
            SaveManager.Instance.CurrentGameData.playerData.position = transform.position;
            SaveManager.Instance.CurrentGameData.playerData.rotation.y = transform.eulerAngles.y;
            SaveManager.Instance.CurrentGameData.playerData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        // Update ScenePersistenceManager data
        if (ScenePersistenceManager.Instance != null)
        {
            var persistentData = ScenePersistenceManager.Instance.GetPersistentData();
            if (persistentData != null)
            {
                if (persistentData.playerData == null)
                {
                    persistentData.playerData = new PlayerSaveData();
                }
                persistentData.playerData.position = transform.position;
                persistentData.playerData.rotation.y = transform.eulerAngles.y;
                persistentData.playerData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            }
        }
    }

    public void LoadPositionFromSaveSystem()
    {
        Debug.Log("Loading player position from save system...");
        Vector3 savedPosition = Vector3.zero;
        float savedRotation = 0f;
        bool foundSaveData = false;

        // Try SaveManager first
        if (SaveManager.Instance != null && SaveManager.Instance.CurrentGameData?.playerData != null)
        {
            var playerData = SaveManager.Instance.CurrentGameData.playerData;
            savedPosition = playerData.position;
            savedRotation = playerData.rotation.y;
            foundSaveData = true;
            Debug.Log($"PlayerController loaded position FROM SAVEMANAGER: {savedPosition}");
        }
        // Try ScenePersistenceManager
        else if (ScenePersistenceManager.Instance != null)
        {
            var persistentData = ScenePersistenceManager.Instance.GetPersistentData();
            if (persistentData?.playerData != null)
            {
                savedPosition = persistentData.playerData.position;
                savedRotation = persistentData.playerData.rotation.y;
                foundSaveData = true;
                Debug.Log($"PlayerController loaded position FROM SCENEPERSISTENCEMANAGER: {savedPosition}");
            }
        }

        if (foundSaveData && savedPosition != Vector3.zero)
        {
            transform.position = savedPosition;
            transform.rotation = Quaternion.Euler(0, savedRotation, 0);
            Debug.Log($"PlayerController loaded position from save: {savedPosition}");
        }
    }

    private void Update()
    {
        if (!GameManager.Instance || GameManager.Instance.isPaused) return;

        UpdateMovementState();
        HandleInput();
    }

    private void HandleInput()
    {
        if (inputManager == null) return;

        // Movement input
        if (canMove)
        {
            movement.SetMovementInput(inputManager.MovementInput);
            movement.SetSprinting(canSprint && inputManager.SprintHeld);
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
        if (!movement.IsGrounded)
        {
            return movement.Velocity.y > 0.1f ? MovementState.Jumping : MovementState.Falling;
        }

        if (movement.IsCrouching)
        {
            return MovementState.Crouching;
        }

        float horizontalSpeed = new Vector2(movement.Velocity.x, movement.Velocity.z).magnitude;

        if (horizontalSpeed < 0.1f)
        {
            return MovementState.Idle;
        }

        return movement.IsSprinting ? MovementState.Running : MovementState.Walking;
    }

    private void ChangeState(MovementState newState)
    {
        previousState = currentState;
        currentState = newState;

        OnStateChanged?.Invoke(previousState, currentState);

        // Notify components of state change
        movement.OnMovementStateChanged(previousState, currentState);
        playerCamera.OnMovementStateChanged(previousState, currentState);
        playerAudio.OnMovementStateChanged(previousState, currentState);
    }

    // Input handlers
    private void HandleJumpInput()
    {
        if (canJump && movement.IsGrounded && !movement.IsCrouching)
        {
            movement.Jump();
        }
    }

    private void HandleCrouchInput()
    {
        if (canCrouch)
        {
            movement.StartCrouch();
        }
    }

    private void HandleCrouchReleased()
    {
        if (canCrouch)
        {
            movement.StopCrouch();
        }
    }

    // Public methods for other systems
    public void SetMovementEnabled(bool enabled) => canMove = enabled;
    public void SetJumpEnabled(bool enabled) => canJump = enabled;
    public void SetSprintEnabled(bool enabled) => canSprint = enabled;
    public void SetCrouchEnabled(bool enabled) => canCrouch = enabled;
    public void SetLookEnabled(bool enabled) => canLook = enabled;

    // Getters
    public bool IsMoving => movement.IsMoving;
    public bool IsGrounded => movement.IsGrounded;
    public bool IsSprinting => movement.IsSprinting;
    public bool IsCrouching => movement.IsCrouching;
    public Vector3 Velocity => movement.Velocity;

    private void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnJumpPressed -= HandleJumpInput;
            inputManager.OnCrouchPressed -= HandleCrouchInput;
            inputManager.OnCrouchReleased -= HandleCrouchReleased;
        }
    }
}