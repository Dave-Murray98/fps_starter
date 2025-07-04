using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;
using Sirenix.OdinInspector;

public class PlayerCamera : MonoBehaviour
{
    [Header("Cinemachine")]
    public CinemachineVirtualCameraBase virtualCamera;
    public CinemachineInputAxisController inputProvider;

    [Header("Camera Effects")]
    public bool enableCameraBob = true;
    public bool enableCameraSway = true;
    public bool enableFOVEffects = true;
    public bool enableCameraShake = true;

    [Header("FOV Settings")]
    public float normalFOV = 75f;
    public float sprintingFOVIncrease = 5f;
    public float crouchingFOVDecrease = 5f;
    public float fovTransitionDuration = 0.3f;

    [Header("Camera Bob")]
    public float bobFrequency = 2f;
    public float walkBobAmount = 0.1f;
    public float sprintBobAmount = 0.15f;
    public float crouchBobAmount = 0.05f;

    [Header("Camera Sway")]
    public float swayAmount = 0.02f;
    public float maxSwayAmount = 0.05f;
    public float swaySmooth = 4f;

    [Header("Camera Shake")]
    public float landingShakeIntensity = 0.3f;
    public float landingShakeDuration = 0.2f;

    // Private variables
    private PlayerController controller;
    private PlayerData playerData;
    private Vector2 lookInput;

    // Cinemachine components
    private CinemachinePanTilt cinemachinePOV;
    private CinemachineBasicMultiChannelPerlin noiseComponent;
    private CinemachineCamera cinemachineCamera; // For FOV access in CM 3.x

    // Camera effects
    private Vector3 originalCameraOffset;
    private Vector3 crouchCameraOffset;
    private Tweener crouchTweener; // For smooth crouch transitions
    private float bobTimer;
    private Vector3 swayPosition;
    private Tweener fovTweener;

    // Properties for external access
    public Vector3 Forward => virtualCamera ? virtualCamera.transform.forward : transform.forward;
    public Vector3 Right => virtualCamera ? virtualCamera.transform.right : transform.right;
    public float CurrentFOV => GetCurrentFOV();

    public void Initialize(PlayerController playerController)
    {
        controller = playerController;
        playerData = GameManager.Instance.playerData;

        SetupCinemachine();
    }

    private void SetupCinemachine()
    {
        // Find or setup Cinemachine components
        if (virtualCamera == null)
            virtualCamera = GetComponentInChildren<CinemachineVirtualCameraBase>();

        if (virtualCamera != null)
        {
            // Try to get CinemachineCamera for FOV control (CM 3.x)
            cinemachineCamera = virtualCamera as CinemachineCamera;

            // Get PanTilt component (replaces POV in CM 3.x)
            cinemachinePOV = virtualCamera.GetComponent<CinemachinePanTilt>();
            if (cinemachinePOV != null)
            {
                // Configure PanTilt axis ranges (InputAxis doesn't have MaxSpeed)
                var panAxis = cinemachinePOV.PanAxis;
                var tiltAxis = cinemachinePOV.TiltAxis;

                // Set tilt limits based on player data
                tiltAxis.Range = new Vector2(-(playerData?.verticalLookLimit ?? 90f), playerData?.verticalLookLimit ?? 90f);
                tiltAxis.Center = 0f;

                // Pan can rotate 360 degrees
                panAxis.Range = new Vector2(-180f, 180f);
                panAxis.Wrap = true; // Allow wrapping for smooth 360 rotation
                panAxis.Center = 0f;

                // Disable recentering since we want manual control
                var recenterSettings = Unity.Cinemachine.InputAxis.RecenteringSettings.Default;
                recenterSettings.Enabled = false;

                panAxis.Recentering = recenterSettings;
                tiltAxis.Recentering = recenterSettings;

                // Apply the modified axes back
                cinemachinePOV.PanAxis = panAxis;
                cinemachinePOV.TiltAxis = tiltAxis;
            }

            // Get noise component for camera shake
            noiseComponent = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noiseComponent != null)
            {
                noiseComponent.AmplitudeGain = 0f; // Start with no shake
                noiseComponent.FrequencyGain = 1f;
                //Debug.Log("Camera shake component found and initialized");
            }
            else
            {
                //Debug.LogWarning("CinemachineBasicMultiChannelPerlin component not found on virtual camera!");
            }

            // Set initial FOV
            SetFOV(normalFOV);
            // Debug.Log($"Initial FOV set to: {normalFOV}");

            // Store original camera transform position for bob effects
            originalCameraOffset = virtualCamera.transform.localPosition;

            // Calculate crouch camera offset (lower the camera when crouching)
            float crouchHeightDifference = (playerData?.crouchHeightMultiplier ?? 0.5f);
            crouchCameraOffset = originalCameraOffset + Vector3.down * (1f - crouchHeightDifference);

            //Debug.Log($"Camera offsets - Original: {originalCameraOffset}, Crouch: {crouchCameraOffset}");
        }

        // Setup input provider - disable it since we handle input manually
        if (inputProvider != null)
        {
            inputProvider.enabled = false;
            //Debug.Log("CinemachineInputAxisController disabled - we handle input manually");
        }
    }

    private void Update()
    {
        if (controller == null || virtualCamera == null) return;

        HandleCinemachineInput();
        UpdateCameraEffects();
    }

    private void HandleCinemachineInput()
    {
        if (cinemachinePOV == null) return;

        // Apply look input directly to the InputAxis values
        if (lookInput.magnitude > 0.01f)
        {
            float sensitivity = playerData?.lookSensitivity ?? 2f;

            // Get current axes
            var panAxis = cinemachinePOV.PanAxis;
            var tiltAxis = cinemachinePOV.TiltAxis;

            // Update axis values directly (sensitivity controls how fast we rotate)
            panAxis.Value += lookInput.x * sensitivity * Time.deltaTime * 100f; // Scale for smooth rotation
            tiltAxis.Value -= lookInput.y * sensitivity * Time.deltaTime * 100f; // Inverted for FPS feel

            // Clamp tilt within range
            tiltAxis.Value = Mathf.Clamp(tiltAxis.Value, tiltAxis.Range.x, tiltAxis.Range.y);

            // Handle pan wrapping manually if needed
            if (panAxis.Wrap)
            {
                panAxis.Value = panAxis.ClampValue(panAxis.Value);
            }

            // Apply the modified axes back to the component
            cinemachinePOV.PanAxis = panAxis;
            cinemachinePOV.TiltAxis = tiltAxis;
        }
    }

    private void UpdateCameraEffects()
    {
        // Check if shake is currently active
        bool shakeActive = noiseComponent != null && noiseComponent.AmplitudeGain > 0.01f;

        // Get the appropriate base offset (original or crouch)
        Vector3 baseOffset = controller.IsCrouching ? crouchCameraOffset : originalCameraOffset;
        Vector3 finalOffset = baseOffset;

        // Only apply camera bob and sway when NOT shaking
        if (!shakeActive)
        {
            // Camera bob
            if (enableCameraBob && controller.IsMoving && controller.IsGrounded)
            {
                finalOffset += CalculateCameraBob();
            }

            // Camera sway
            if (enableCameraSway)
            {
                finalOffset += CalculateCameraSway();
            }

            // Normal smooth movement when no shake
            if (virtualCamera != null)
            {
                virtualCamera.transform.localPosition = Vector3.Lerp(
                    virtualCamera.transform.localPosition,
                    finalOffset,
                    Time.deltaTime * 10f
                );
            }
        }
        else
        {
            // During shake, set base position without bob/sway and let Cinemachine handle shake
            if (virtualCamera != null)
            {
                virtualCamera.transform.localPosition = baseOffset;
            }
        }
    }

    private Vector3 CalculateCameraBob()
    {
        bobTimer += Time.deltaTime * bobFrequency;

        float bobAmount = walkBobAmount;
        if (controller.IsSprinting) bobAmount = sprintBobAmount;
        else if (controller.IsCrouching) bobAmount = crouchBobAmount;

        float horizontal = Mathf.Sin(bobTimer) * bobAmount;
        float vertical = Mathf.Sin(bobTimer * 2f) * bobAmount * 0.5f;

        return new Vector3(horizontal, vertical, 0f);
    }

    private Vector3 CalculateCameraSway()
    {
        Vector3 targetSway = Vector3.zero;

        if (controller.IsMoving)
        {
            targetSway.x = -lookInput.x * swayAmount;
            targetSway.z = -lookInput.y * swayAmount;
            targetSway = Vector3.ClampMagnitude(targetSway, maxSwayAmount);
        }

        swayPosition = Vector3.Lerp(swayPosition, targetSway, Time.deltaTime * swaySmooth);
        return swayPosition;
    }

    // FOV methods for Cinemachine 3.x
    private float GetCurrentFOV()
    {
        // Try multiple methods to get FOV
        if (cinemachineCamera != null)
        {
            return cinemachineCamera.Lens.FieldOfView;
        }

        // Fallback: try to get from the actual camera via CinemachineBrain
        var brain = Camera.main?.GetComponent<CinemachineBrain>();
        if (brain != null && brain.OutputCamera != null)
        {
            return brain.OutputCamera.fieldOfView;
        }

        // Last resort: try any camera
        if (Camera.main != null)
        {
            return Camera.main.fieldOfView;
        }

        // Debug.LogWarning("Could not find any camera for FOV - returning default");
        return normalFOV;
    }

    private void SetFOV(float fov)
    {
        if (cinemachineCamera != null)
        {
            var lens = cinemachineCamera.Lens;
            lens.FieldOfView = fov;
            cinemachineCamera.Lens = lens;
        }
    }

    private void UpdateFOV(float targetFOV)
    {
        if (!enableFOVEffects) return;

        // Kill existing FOV tween
        fovTweener?.Kill();

        // Animate FOV change with DOTween
        fovTweener = DOTween.To(
            () => GetCurrentFOV(),
            x => SetFOV(x),
            targetFOV,
            fovTransitionDuration
        ).SetEase(Ease.OutQuart);
    }

    // Camera position methods
    public void SetCrouchCameraPosition(bool isCrouching, float duration = 0.3f)
    {
        if (virtualCamera == null) return;

        // Kill existing crouch tween
        crouchTweener?.Kill();

        Vector3 targetOffset = isCrouching ? crouchCameraOffset : originalCameraOffset;
        Vector3 startOffset = virtualCamera.transform.localPosition;

        //Debug.Log($"Camera crouch transition: {startOffset} → {targetOffset}");

        // Smooth transition to new camera height
        crouchTweener = DOTween.To(
            () => virtualCamera.transform.localPosition,
            x => virtualCamera.transform.localPosition = x,
            targetOffset,
            duration
        ).SetEase(Ease.OutQuart);
    }

    public void ShakeCamera(float intensity, float duration, bool useTimeScale = true)
    {
        if (!enableCameraShake || noiseComponent == null)
        {
            //Debug.LogWarning("Camera shake failed: enableCameraShake=" + enableCameraShake + ", noiseComponent=" + (noiseComponent != null));
            return;
        }

        //Debug.Log($"Starting camera shake: intensity={intensity}, duration={duration}, isMoving={controller.IsMoving}");

        // Set shake intensity
        noiseComponent.AmplitudeGain = intensity;

        // Animate shake intensity back to 0
        DOTween.To(
            () => noiseComponent.AmplitudeGain,
            x =>
            {
                noiseComponent.AmplitudeGain = x;
                //if (x > 0.01f) Debug.Log($"Shake amplitude: {x:F3}, cameraMoving: {controller.IsMoving}");
            },
            0f,
            duration
        ).SetUpdate(!useTimeScale);
        //.OnComplete(() => Debug.Log("Camera shake completed"));
    }

    public void PlayLandingShake()
    {
        ShakeCamera(landingShakeIntensity, landingShakeDuration);
    }

    // FOV effect methods
    public void SetRunningFOV()
    {
        UpdateFOV(normalFOV + sprintingFOVIncrease);
    }

    public void SetCrouchingFOV()
    {
        UpdateFOV(normalFOV - crouchingFOVDecrease);
    }

    public void SetNormalFOV()
    {
        UpdateFOV(normalFOV);
    }

    // Utility methods to get camera forward direction for movement
    public Vector3 GetCameraForward()
    {
        Vector3 forward = Forward;
        forward.y = 0f;
        return forward.normalized;
    }

    public Vector3 GetCameraRight()
    {
        Vector3 right = Right;
        right.y = 0f;
        return right.normalized;
    }

    // Input method
    public void SetLookInput(Vector2 input) => lookInput = input;

    // State change notification
    public void OnMovementStateChanged(MovementState previousState, MovementState newState)
    {
        // Reset bob timer when transitioning to/from idle
        if (previousState == MovementState.Idle || newState == MovementState.Idle)
        {
            bobTimer = 0f;
        }

        // Handle FOV changes based on state
        switch (newState)
        {
            case MovementState.Running:
                SetRunningFOV();
                break;
            case MovementState.Crouching:
                SetCrouchingFOV();
                break;
            case MovementState.Walking:
            case MovementState.Idle:
                SetNormalFOV();
                break;
        }

        // Camera shake on landing - trigger for ANY grounded state after falling
        if (previousState == MovementState.Falling &&
            (newState == MovementState.Idle || newState == MovementState.Walking || newState == MovementState.Running))
        {
            //Debug.Log($"Landing detected: {previousState} → {newState} - triggering shake");
            PlayLandingShake();
        }
    }
}