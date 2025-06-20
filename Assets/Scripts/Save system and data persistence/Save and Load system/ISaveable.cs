using UnityEngine;

/// <summary>
/// Interface for any object that can be saved and loaded
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// Unique identifier for this saveable object
    /// Should be consistent between saves/loads
    /// </summary>
    string SaveID { get; }

    /// <summary>
    /// Category determines whether this object's data is scene-dependent or player-dependent
    /// </summary>
    SaveDataCategory SaveCategory { get; }

    /// <summary>
    /// Returns the data that should be saved for this object
    /// </summary>
    object GetDataToSave();

    /// <summary>
    /// Extracts relevant data from the provided save container
    /// This is used to filter out only the data that this object cares about
    /// </summary>
    object ExtractRelevantData(object saveContainer);

    /// <summary>
    /// Loads the provided data into this object
    /// </summary>
    void LoadSaveData(object data);

    /// <summary>
    /// Optional: Called before save data is collected
    /// Use for any preparation needed before saving
    /// </summary>
    void OnBeforeSave() { }

    /// <summary>
    /// Optional: Called after save data has been loaded
    /// Use for any setup needed after loading
    /// </summary>
    void OnAfterLoad() { }
}

/// <summary>
/// Defines whether save data is scene-dependent or player-dependent 
/// </summary>
public enum SaveDataCategory
{
    /// <summary>
    /// Scene-dependent data - this data contains information about the current scene and when the player leaves the scene, it should be saved so that it can be restored when the player returns to that scene.
    /// Examples: Enemy states, pickup collections, door locks, environmental changes
    /// </summary>
    SceneDependent,

    /// <summary>
    /// Player-dependent data - this data should persist across scenes unless the player loads into a previous save, wherein it should be restored to that save's state.
    /// Examples: Player health, inventory, stats, quest progress
    /// </summary>
    PlayerDependent
}

/// <summary>
/// Optional interface for saveable components that need context-aware restoration
/// This allows components to handle restoration differently based on the context
/// (doorway transition vs save file load vs new game)
/// </summary>
public interface IContextAwareSaveable : ISaveable
{
    /// <summary>
    /// Load save data with awareness of the restoration context
    /// This allows components to make context-specific decisions about what to restore
    /// 
    /// Examples:
    /// - PlayerSaveComponent: Don't restore position during doorway transitions
    /// - InventoryComponent: Always restore inventory regardless of context
    /// - QuestComponent: Reset active quests on new game but preserve on transitions
    /// </summary>
    /// <param name="data">The data to load</param>
    /// <param name="context">The context for this restoration operation</param>
    void LoadSaveDataWithContext(object data, RestoreContext context);
}