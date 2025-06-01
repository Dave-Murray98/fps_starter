using UnityEngine;


/// <summary>
/// Triggers scene transitions when player interacts or enters trigger
/// </summary>
public class ScenePortal : MonoBehaviour
{
    [Header("Portal Settings")]
    public string targetScene = "TestLevel02";
    public string targetSpawnPointID = "DefaultSpawn";

    [Header("Interaction")]
    public bool requiresInteraction = false;
    public KeyCode interactionKey = KeyCode.E;
    public float interactionRange = 3f;

    [Header("Visual Feedback")]
    public GameObject interactionPrompt;
    public string promptText = "Press E to enter";

    private bool playerInRange = false;
    private PlayerController playerController;

    private void Update()
    {
        if (requiresInteraction && playerInRange)
        {
            if (Input.GetKeyDown(interactionKey))
            {
                TriggerPortal();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerController = other.GetComponent<PlayerController>();
            playerInRange = true;

            if (requiresInteraction)
            {
                ShowInteractionPrompt(true);
            }
            else
            {
                TriggerPortal();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            playerController = null;

            if (requiresInteraction)
            {
                ShowInteractionPrompt(false);
            }
        }
    }

    private void TriggerPortal()
    {
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene(targetScene, targetSpawnPointID);
        }
        else
        {
            Debug.LogError("SceneTransitionManager not found!");
        }
    }

    private void ShowInteractionPrompt(bool show)
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(show);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position, GetComponent<Collider>()?.bounds.size ?? Vector3.one);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"Portal to: {targetScene}");
#endif
    }
}