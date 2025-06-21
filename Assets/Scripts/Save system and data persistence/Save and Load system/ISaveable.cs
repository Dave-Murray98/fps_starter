using UnityEngine;

/// <summary>
/// Interface for any object that can be saved and loaded
/// UPDATED: Now all saveable objects are context-aware by default
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
    /// Loads the provided data into this object with awareness of the restoration context
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
/// Enhanced interface for player-dependent save components that need to handle unified save data
/// This makes the system truly modular by letting components handle their own data mapping
/// </summary>
public interface IPlayerDependentSaveable : ISaveable
{
    /// <summary>
    /// Extract this component's data from a unified save structure
    /// Each component knows how to get its data from PlayerPersistentData
    /// </summary>
    /// <param name="unifiedData">The unified player data structure</param>
    /// <returns>Component-specific data extracted from the unified structure</returns>
    object ExtractFromUnifiedSave(PlayerPersistentData unifiedData);

    /// <summary>
    /// Create default data for this component (used for new games)
    /// Each component knows what its default state should be
    /// </summary>
    /// <returns>Default data for this component</returns>
    object CreateDefaultData();

    /// <summary>
    /// Contribute this component's data to a unified save structure
    /// Each component knows how to store its data in PlayerPersistentData
    /// </summary>
    /// <param name="componentData">This component's data to contribute</param>
    /// <param name="unifiedData">The unified structure to contribute to</param>
    void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData);
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
/// Defines the context for data restoration operations
/// This tells restoration systems WHY they're being called and what they should restore
/// </summary>
public enum RestoreContext
{
    /// <summary>
    /// Player is transitioning through a doorway/portal
    /// - Restore player stats, inventory, equipment, abilities
    /// - Do NOT restore player position (doorway will set position)
    /// - Restore scene-dependent data for the target scene
    /// </summary>
    DoorwayTransition,

    /// <summary>
    /// Player is loading from a save file
    /// - Restore ALL player data INCLUDING position
    /// - Restore scene-dependent data from save file
    /// - This is a complete state restoration
    /// </summary>
    SaveFileLoad,

    /// <summary>
    /// New game initialization
    /// - Set default player stats
    /// - Clear inventory/equipment
    /// - Set starting position
    /// </summary>
    NewGame
}