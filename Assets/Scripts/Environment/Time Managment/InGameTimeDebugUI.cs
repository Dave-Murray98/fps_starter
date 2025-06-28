using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;

/// <summary>
/// Debug UI component for displaying Day/Night Cycle information in the scene.
/// Automatically connects to InGameTimeManager and updates Text Mesh Pro components
/// with current time, date, season, and temperature information.
/// </summary>
public class InGameTimeDebugUI : MonoBehaviour
{
    [Header("Text Components")]
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private TextMeshProUGUI seasonText;
    //[SerializeField] private TextMeshProUGUI combinedText; // For single display

    [Header("Display Settings")]
    [SerializeField] private bool showTime = true;
    [SerializeField] private bool showDate = true;
    [SerializeField] private bool showSeason = true;
    [SerializeField] private bool showTemperature = true;
    [SerializeField] private bool use24HourFormat = true;
    [SerializeField] private bool showSeconds = false;


    [Header("Update Settings")]
    [SerializeField] private float updateInterval = 0.1f; // How often to update (seconds)
    [SerializeField] private bool enableUpdates = true;

    [Header("Debug Info")]
    [ShowInInspector, ReadOnly] private bool isConnected = false;
    [ShowInInspector, ReadOnly] private float lastUpdateTime = 0f;

    // Cached values to avoid unnecessary updates
    private float lastTimeOfDay = -1f;
    private int lastDayOfSeason = -1;
    private SeasonType lastSeason = (SeasonType)(-1);

    private void Start()
    {
        ConnectToDayNightCycle();

        // Force initial update
        lastUpdateTime = -updateInterval;
    }

    private void Update()
    {
        if (enableUpdates && Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDisplays();
            lastUpdateTime = Time.time;
        }
    }

    private void OnEnable()
    {
        ConnectToDayNightCycle();
    }

    #region Connection Management

    /// <summary>
    /// Connects to the InGameTimeManager and subscribes to events.
    /// </summary>
    private void ConnectToDayNightCycle()
    {
        if (InGameTimeManager.Instance != null)
        {
            isConnected = true;
            //    Debug.Log("[InGameTimeManagerUI] Connected to InGameTimeManager");
        }
        else
        {
            isConnected = false;
            //     Debug.Log("[InGameTimeManagerUI] InGameTimeManager not found - will retry");

            // Retry connection after a short delay
            Invoke(nameof(ConnectToDayNightCycle), 0.5f);
        }
    }

    #endregion

    #region Display Updates

    /// <summary>
    /// Updates all text displays with current information.
    /// </summary>
    private void UpdateDisplays()
    {
        if (!isConnected || InGameTimeManager.Instance == null)
        {
            // Try to reconnect
            ConnectToDayNightCycle();
            return;
        }

        // Get current values
        float currentTime = InGameTimeManager.Instance.GetCurrentTimeOfDay();
        int currentDay = InGameTimeManager.Instance.GetCurrentDayOfSeason();
        SeasonType currentSeason = InGameTimeManager.Instance.GetCurrentSeason();

        // Check if values have changed (avoid unnecessary string operations)
        bool timeChanged = Mathf.Abs(currentTime - lastTimeOfDay) > 0.01f;
        bool dayChanged = currentDay != lastDayOfSeason;
        bool seasonChanged = currentSeason != lastSeason;

        bool anyChanged = timeChanged || dayChanged || seasonChanged;

        if (!anyChanged) return; // No updates needed

        // Update individual text components
        if (showTime && timeText != null && timeChanged)
        {
            timeText.text = GetTimeString(currentTime);
        }

        if (showDate && dateText != null && dayChanged)
        {
            dateText.text = GetDateString(currentDay);
        }

        if (showSeason && seasonText != null && seasonChanged)
        {
            seasonText.text = GetSeasonString(currentSeason);
        }

        // // Update combined text if available
        // if (combinedText != null)
        // {
        //     combinedText.text = GetCombinedString(currentTime, currentDay, currentSeason, totalTemperature, tempModifier);
        // }

        // Cache current values
        lastTimeOfDay = currentTime;
        lastDayOfSeason = currentDay;
        lastSeason = currentSeason;
    }

    #endregion

    #region String Formatting

    /// <summary>
    /// Formats the time of day string.
    /// </summary>
    private string GetTimeString(float timeOfDay)
    {
        int hours = Mathf.FloorToInt(timeOfDay);
        int minutes = Mathf.FloorToInt((timeOfDay - hours) * 60f);
        int seconds = showSeconds ? Mathf.FloorToInt(((timeOfDay - hours) * 60f - minutes) * 60f) : 0;

        if (use24HourFormat)
        {
            if (showSeconds)
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            else
                return $"{hours:D2}:{minutes:D2}";
        }
        else
        {
            // 12-hour format
            string period = hours >= 12 ? "PM" : "AM";
            int displayHours = hours == 0 ? 12 : (hours > 12 ? hours - 12 : hours);

            if (showSeconds)
                return $"{displayHours}:{minutes:D2}:{seconds:D2} {period}";
            else
                return $"{displayHours}:{minutes:D2} {period}";
        }
    }

    /// <summary>
    /// Formats the date string.
    /// </summary>
    private string GetDateString(int dayOfSeason)
    {
        return $"Day {dayOfSeason}";
    }

    /// <summary>
    /// Formats the season string.
    /// </summary>
    private string GetSeasonString(SeasonType season)
    {
        return season.ToString();
    }

    // /// <summary>
    // /// Formats the combined information string.
    // /// </summary>
    // private string GetCombinedString(float timeOfDay, int dayOfSeason, SeasonType season, float modifier)
    // {
    //     var info = new System.Text.StringBuilder();

    //     if (showTime)
    //         info.AppendLine($"Time: {GetTimeString(timeOfDay)}");

    //     if (showDate)
    //         info.AppendLine($"Date: {GetDateString(dayOfSeason)} of {GetSeasonString(season)}");
    //     else if (showSeason)
    //         info.AppendLine($"Season: {GetSeasonString(season)}");



    //     // Add day/night indicator
    //     bool isDaytime = InGameTimeManager.Instance.IsDaytime();
    //     info.AppendLine($"Period: {(isDaytime ? "Day" : "Night")}");

    //     // Add total days elapsed
    //     int totalDays = InGameTimeManager.Instance.GetTotalDaysElapsed();
    //     info.AppendLine($"Total Days: {totalDays}");

    //     return info.ToString().TrimEnd();
    // }

    #endregion

    #region Manual Controls

    /// <summary>
    /// Manually forces an update of all displays.
    /// </summary>
    [Button("Force Update")]
    public void ForceUpdate()
    {
        // Reset cached values to force update
        lastTimeOfDay = -1f;
        lastDayOfSeason = -1;
        lastSeason = (SeasonType)(-1);

        UpdateDisplays();
        Debug.Log("[DayNightDebugUI] Forced display update");
    }

    /// <summary>
    /// Toggles display updates on/off.
    /// </summary>
    [Button("Toggle Updates")]
    public void ToggleUpdates()
    {
        enableUpdates = !enableUpdates;
        Debug.Log($"[DayNightDebugUI] Updates {(enableUpdates ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Refreshes text component references.
    /// </summary>
    [Button("Refresh Text References")]
    public void RefreshTextReferences()
    {
        ForceUpdate();
    }

    /// <summary>
    /// Clears all text displays.
    /// </summary>
    [Button("Clear Displays")]
    public void ClearDisplays()
    {
        if (timeText != null) timeText.text = "";
        if (dateText != null) dateText.text = "";
        if (seasonText != null) seasonText.text = "";
        //if (combinedText != null) combinedText.text = "";

        Debug.Log("[DayNightDebugUI] All displays cleared");
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Sets the update interval for display refreshes.
    /// </summary>
    public void SetUpdateInterval(float interval)
    {
        updateInterval = Mathf.Max(0.01f, interval);
    }

    /// <summary>
    /// Configures which information to display.
    /// </summary>
    public void ConfigureDisplay(bool time, bool date, bool season, bool temperature)
    {
        showTime = time;
        showDate = date;
        showSeason = season;
        showTemperature = temperature;
        ForceUpdate();
    }

    #endregion

    #region Public Getters

    /// <summary>
    /// Gets the current formatted time string.
    /// </summary>
    public string GetCurrentTimeString()
    {
        if (InGameTimeManager.Instance != null)
        {
            return GetTimeString(InGameTimeManager.Instance.GetCurrentTimeOfDay());
        }
        return "No Connection";
    }

    /// <summary>
    /// Gets the current formatted date string.
    /// </summary>
    public string GetCurrentDateString()
    {
        if (InGameTimeManager.Instance != null)
        {
            return $"{GetDateString(InGameTimeManager.Instance.GetCurrentDayOfSeason())} of {GetSeasonString(InGameTimeManager.Instance.GetCurrentSeason())}";
        }
        return "No Connection";
    }


    /// <summary>
    /// Checks if the debug UI is connected to the day/night cycle manager.
    /// </summary>
    public bool IsConnected() => isConnected;

    #endregion

    private void OnValidate()
    {
        // Clamp update interval
        updateInterval = Mathf.Max(0.01f, updateInterval);
    }
}