using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Combined Portal + Spawn Point = Doorway
/// Handles both entry and exit from scenes
/// </summary>
public class Doorway : MonoBehaviour
{
    [Header("Doorway Settings")]
    public string doorwayID = "";
    public string targetScene = "";
    public string targetDoorwayID = "";

    [Header("Interaction")]
    public bool requiresInteraction = true;
    public string interactionPrompt = "Press E to enter";

    private bool playerInRange = false;

    private void Start()
    {
        if (string.IsNullOrEmpty(doorwayID))
            doorwayID = $"Door_{transform.position.x}_{transform.position.z}";
    }

    private void Update()
    {
        if (playerInRange && requiresInteraction && Input.GetKeyDown(KeyCode.E))
        {
            UseDoorway();
        }
    }

    public void UseDoorway()
    {
        // Save player persistent data before transition
        PlayerPersistenceManager.Instance?.SavePlayerDataForTransition();

        // Then do the scene transition
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionThroughDoorway(targetScene, targetDoorwayID);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (!requiresInteraction)
            {
                UseDoorway();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
}
