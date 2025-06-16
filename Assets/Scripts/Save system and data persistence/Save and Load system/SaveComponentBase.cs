using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Base class for all save components
/// Provides common functionality and enforces consistent patterns
/// </summary>
public abstract class SaveComponentBase : MonoBehaviour, ISaveable
{
    [Header("Save Component Settings")]
    [SerializeField] protected string saveID;
    [SerializeField] protected bool autoGenerateID = true;
    [SerializeField] protected bool enableDebugLogs = false;

    // ISaveable implementation
    public virtual string SaveID
    {
        get
        {
            if (autoGenerateID && string.IsNullOrEmpty(saveID))
            {
                saveID = GenerateUniqueID();
            }
            return saveID;
        }
    }

    [ShowInInspector] public virtual SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    public abstract object GetDataToSave();
    public abstract void LoadSaveData(object data);

    public virtual void OnBeforeSave()
    {
        DebugLog("Preparing to save");
    }

    public virtual void OnAfterLoad()
    {
        DebugLog("Finished loading");
    }

    // Utility methods
    protected virtual string GenerateUniqueID()
    {
        // Generate unique ID based on object type and position
        string typeName = GetType().Name;
        string position = transform.position.ToString("F2");
        string sceneId = GetComponentInParent<Transform>()?.GetSiblingIndex().ToString() ?? "0";
        return $"{typeName}_{sceneId}_{position}";
    }

    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[{GetType().Name}:{SaveID}] {message}");
        }
    }

    protected virtual void Awake()
    {
        // Auto-register with save manager if it exists
        if (SaveManager.Instance != null)
        {
            //DebugLog("Registered with SaveManager");
        }
    }

    protected virtual void OnValidate()
    {
        // In editor, auto-generate ID if needed
        if (autoGenerateID && string.IsNullOrEmpty(saveID))
        {
            saveID = GenerateUniqueID();
        }
    }

    public virtual object ExtractRelevantData(object saveContainer)
    {
        // Default implementation returns the entire save container
        // Override in derived classes to filter data as needed
        return saveContainer;
    }
}