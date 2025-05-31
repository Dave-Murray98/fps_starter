using System;
using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource footstepAudioSource;
    public AudioSource effectSource;

    [Header("Footstep Audio Clips")]
    public AudioClip[] defaultFootstepClips;
    public AudioClip[] grassFootstepClips;
    public AudioClip[] metalFootstepClips;
    public AudioClip[] woodFootstepClips;

    [Header("Effect Audio Clips")]
    public AudioClip jumpClip;
    public AudioClip landClip;


    //private variables
    private PlayerController playerController;
    private PlayerData playerData;
    private float footstepTimer;
    private float lastPlayedFootstep;

    public void Initialize(PlayerController playerControllerToAssign)
    {
        playerController = playerControllerToAssign;
        playerData = GameManager.Instance.playerData;

        //setup audio sources
        if (footstepAudioSource == null) footstepAudioSource = GetComponent<AudioSource>();
        if (effectSource == null) effectSource = footstepAudioSource;

        Debug.Log("PlayerAudio Initialized");
    }

    private void Update()
    {
        if (playerController == null) return;

        HandleFootsteps();
    }

    private void HandleFootsteps()
    {
        if (!playerController.IsMoving || playerController.IsCrouching) return;

        float footstepRate = playerData?.footstepRate ?? 0.5f;

        if (playerController.IsSprinting)
        {
            footstepRate *= 0.7f;
        }
        else if (playerController.IsCrouching)
        {
            footstepRate *= 1.5f;
        }

        footstepTimer += Time.deltaTime;

        if (footstepTimer > footstepRate)
        {
            PlayFootstep();
            footstepTimer = 0f;

        }
    }

    private void PlayFootstep()
    {
        if (footstepAudioSource == null) return;
        AudioClip[] footstepClips = GetFootstepClipsForSurface(playerController.movement.CurrentGroundType);

        if (footstepClips != null && footstepClips.Length > 0)
        {
            //pick random footstep clip
            AudioClip clipToPlay = footstepClips[UnityEngine.Random.Range(0, footstepClips.Length)];

            //vary pitch slightly for a more natural sound
            footstepAudioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);

            float volume = playerController.IsCrouching ? 0.3f : (playerController.IsSprinting ? 0.8f : 0.6f);

            footstepAudioSource.PlayOneShot(clipToPlay, volume);
        }
    }

    private AudioClip[] GetFootstepClipsForSurface(GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Grass:
                return grassFootstepClips;
            case GroundType.Metal:
                return metalFootstepClips;
            case GroundType.Wood:
                return woodFootstepClips;
            default:
                return defaultFootstepClips;
        }
    }

    public void PlayJumpSound()
    {
        if (effectSource != null && jumpClip != null)
            effectSource.PlayOneShot(jumpClip, 0.7f);
    }

    public void PlayLandSound()
    {
        if (effectSource != null && landClip != null)
            effectSource.PlayOneShot(landClip, 0.8f);
    }

    internal void OnMovementStateChanged(MovementState previousMovementState, MovementState newMovementState)
    {
        if (newMovementState == MovementState.Jumping && previousMovementState != MovementState.Jumping)
            PlayJumpSound();

        if (newMovementState == MovementState.Idle && previousMovementState == MovementState.Falling)
            PlayLandSound();
    }
}
