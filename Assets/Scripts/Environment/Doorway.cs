using UnityEngine;

/// <summary>
/// SIMPLIFIED: Doorway now delegates everything to SceneTransitionManager
/// Much cleaner - no direct interaction with save systems
/// SceneTransitionManager handles all the orchestration
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

    private void Start()
    {
        if (string.IsNullOrEmpty(doorwayID))
        {
            Debug.LogWarning("Doorway ID not set, generating based on position.");
            doorwayID = $"Door_{transform.position.x}_{transform.position.z}";
        }
    }

    public void UseDoorway()
    {
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError("Target scene is not set for doorway: " + doorwayID);
            return;
        }

        Debug.Log($"[Doorway] Using doorway {doorwayID} to transition to {targetScene}");

        // SIMPLIFIED: Just tell SceneTransitionManager to handle the doorway transition
        // It will orchestrate all the data saving, scene loading, and data restoration
        if (SceneTransitionManager.Instance != null)
        {
            // SceneTransitionManager will:
            // 1. Tell PlayerPersistenceManager to save current player data
            // 2. Tell SceneDataManager to save current scene data  
            // 3. Load the target scene
            // 4. Restore scene data (excluding player position)
            // 5. Restore player data (excluding position)
            // 6. Position player at target doorway
            SceneTransitionManager.Instance.TransitionThroughDoorway(targetScene, targetDoorwayID);
        }
        else
        {
            Debug.LogError("[Doorway] SceneTransitionManager not found!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
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
            // Could hide interaction UI here if needed
        }
    }

    /// <summary>
    /// Get doorway information for debugging
    /// </summary>
    public string GetDoorwayInfo()
    {
        return $"Doorway '{doorwayID}' -> Scene: {targetScene}, Target: {targetDoorwayID}";
    }

    /// <summary>
    /// Validate doorway configuration
    /// </summary>
    public bool IsValid()
    {
        bool isValid = !string.IsNullOrEmpty(doorwayID) &&
                      !string.IsNullOrEmpty(targetScene) &&
                      !string.IsNullOrEmpty(targetDoorwayID);

        if (!isValid)
        {
            Debug.LogWarning($"Doorway validation failed: ID='{doorwayID}', Scene='{targetScene}', Target='{targetDoorwayID}'");
        }

        return isValid;
    }

    private void OnValidate()
    {
        // Auto-generate doorway ID in editor if empty
        if (string.IsNullOrEmpty(doorwayID))
        {
            doorwayID = $"Door_{name}";
        }
    }

    private void OnDrawGizmos()
    {
        // Draw doorway visualization in scene view
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 3f, 0.5f));

        // Draw arrow pointing to target
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        // Display doorway info
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
            $"ID: {doorwayID}\n→ {targetScene}:{targetDoorwayID}");
#endif
    }
}