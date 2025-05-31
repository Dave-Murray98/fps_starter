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

        // Subscribe to input events
        if (inputManager != null)
        {
            inputManager.OnJumpPressed += HandleJumpInput;
            inputManager.OnCrouchPressed += HandleCrouchInput;
            inputManager.OnCrouchReleased += HandleCrouchReleased;
        }

        // Lock cursor for first-person
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("PlayerController initialized");
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