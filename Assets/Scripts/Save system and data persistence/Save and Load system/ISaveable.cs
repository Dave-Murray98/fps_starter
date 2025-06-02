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
    /// Returns the data that should be saved for this object
    /// </summary>
    object GetSaveData();

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