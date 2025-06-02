using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;


public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("Loading Screen UI")]
    public Canvas loadingScreenCanvas;
    public GameObject loadingPanel;
    public Slider progressSlider;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI progressText;

    [Header("Loading Settings")]
    public float minimumLoadTime = 1f;
    public float progressSmoothSpeed = 2f;

    [Header("Loading Messages")]
    public string[] doorwayMessages = {
        "Transitioning...",
        "Loading new area...",
        "Preparing environment..."
    };

    public string[] saveLoadMessages = {
        "Loading save data...",
        "Restoring game state...",
        "Rebuilding world..."
    };

    // Internal state
    private float targetProgress = 0f;
    private float currentProgress = 0f;
    private bool isLoading = false;
    private Coroutine loadingCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeLoadingScreen();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeLoadingScreen()
    {
        // Ensure loading screen starts hidden
        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.gameObject.SetActive(false);
        }

        // Initialize progress
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }

        Debug.Log("LoadingScreenManager initialized");
    }

    private void Update()
    {
        if (isLoading && progressSlider != null)
        {
            // Smooth progress bar animation
            currentProgress = Mathf.Lerp(currentProgress, targetProgress, progressSmoothSpeed * Time.unscaledDeltaTime);
            progressSlider.value = currentProgress;

            // Update progress text
            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(currentProgress * 100)}%";
            }
        }
    }

    /// <summary>
    /// Show loading screen for doorway transition
    /// </summary>
    public void ShowLoadingScreenForDoorway(string targetScene)
    {
        ShowLoadingScreen(GetRandomMessage(doorwayMessages), $"Entering {targetScene}");
    }

    /// <summary>
    /// Show loading screen for save load
    /// </summary>
    public void ShowLoadingScreenForSaveLoad(string targetScene)
    {
        ShowLoadingScreen(GetRandomMessage(saveLoadMessages), $"Loading {targetScene}");
    }

    /// <summary>
    /// Show loading screen with custom message
    /// </summary>
    public void ShowLoadingScreen(string mainMessage = "Loading...", string subMessage = "")
    {
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
        }

        isLoading = true;
        currentProgress = 0f;
        targetProgress = 0f;

        // Show UI
        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.gameObject.SetActive(true);
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        // Set messages
        if (loadingText != null)
        {
            loadingText.text = string.IsNullOrEmpty(subMessage) ? mainMessage : $"{mainMessage}\n{subMessage}";
        }

        // Reset progress
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }

        if (progressText != null)
        {
            progressText.text = "0%";
        }

        Debug.Log($"Loading screen shown: {mainMessage}");
    }

    /// <summary>
    /// Update loading progress (0-1)
    /// </summary>
    public void SetProgress(float progress)
    {
        targetProgress = Mathf.Clamp01(progress);
    }

    /// <summary>
    /// Hide loading screen
    /// </summary>
    public void HideLoadingScreen()
    {
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
        }

        loadingCoroutine = StartCoroutine(HideLoadingScreenCoroutine());
    }

    private IEnumerator HideLoadingScreenCoroutine()
    {
        // Ensure we show 100% briefly
        SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.3f);

        // Hide UI
        isLoading = false;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        if (loadingScreenCanvas != null)
        {
            loadingScreenCanvas.gameObject.SetActive(false);
        }

        Debug.Log("Loading screen hidden");
    }

    /// <summary>
    /// Check if currently loading
    /// </summary>
    public bool IsLoading => isLoading;

    private string GetRandomMessage(string[] messages)
    {
        if (messages == null || messages.Length == 0)
            return "Loading...";

        return messages[Random.Range(0, messages.Length)];
    }
}
