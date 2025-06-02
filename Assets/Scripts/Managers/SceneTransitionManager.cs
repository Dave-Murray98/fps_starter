using UnityEngine;


/// <summary>
/// Simplified save context - only two cases matter
/// </summary>
public enum TransitionType
{
    Doorway,      // Player using portal/doorway - use scene persistence
    SaveLoad     // Player loading a save file - override with save data
}

/// SIMPLIFIED Scene Transition Manager
/// Only handles transitions - no data management
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Transition Settings")]
    public bool showLoadingScreen = true;
    public float minLoadingTime = 1f;

    // Track if we're loading from a save (to handle positioning correctly)
    private bool isLoadingFromSave = false;

    // Events
    public System.Action<string> OnTransitionStarted;
    public System.Action<string> OnTransitionCompleted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Transition through a doorway (portal-based movement)
    /// </summary>
    public void TransitionThroughDoorway(string targetScene, string targetDoorwayID)
    {
        isLoadingFromSave = false; // This is a doorway transition
        StartCoroutine(DoTransition(targetScene, targetDoorwayID, TransitionType.Doorway));
    }

    /// <summary>
    /// Load scene from save file
    /// </summary>
    public void LoadSceneFromSave(string targetScene)
    {
        isLoadingFromSave = true; // This is a save load
        StartCoroutine(DoTransition(targetScene, "", TransitionType.SaveLoad));
    }

    private System.Collections.IEnumerator DoTransition(string targetScene, string targetDoorwayID, TransitionType transitionType)
    {
        OnTransitionStarted?.Invoke(targetScene);

        // Tell the save system what kind of transition this is
        SceneDataManager.Instance.PrepareSceneTransition(targetScene, targetDoorwayID, transitionType);

        if (showLoadingScreen)
        {
            yield return new WaitForSeconds(minLoadingTime);
        }

        // Load the scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);

        OnTransitionCompleted?.Invoke(targetScene);
    }

    // FIX: Handle scene loaded event to restore player position correctly
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (isLoadingFromSave)
        {
            // When loading from save, let SaveManager handle player positioning
            StartCoroutine(RestorePlayerPositionFromSave());
        }
        // For doorway transitions, SceneDataManager handles positioning
    }

    private System.Collections.IEnumerator RestorePlayerPositionFromSave()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        // Get the save data and restore player position
        if (SaveManager.Instance != null)
        {
            // The SaveManager will handle this in its LoadGameCoroutine
            // We just need to make sure we don't interfere
            Debug.Log("SceneTransitionManager: Letting SaveManager handle player positioning");
        }
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}