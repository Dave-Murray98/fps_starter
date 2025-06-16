using UnityEngine;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// Debug helper to diagnose interaction and save system issues
/// </summary>
public class InteractionDebugHelper : MonoBehaviour
{
    // [Header("Debug Controls")]
    // [SerializeField] private bool enableAutoDebug = true;
    // [SerializeField] private bool verboseLogging = true;

    // private void Start()
    // {
    //     if (enableAutoDebug)
    //     {
    //         // Wait a bit for everything to initialize, then debug
    //         Invoke(nameof(DebugSceneInteractables), 1f);
    //     }
    // }

    // [Button("Debug All Interactables")]
    // public void DebugSceneInteractables()
    // {
    //     Debug.Log("=== SCENE INTERACTABLES DEBUG ===");

    //     var allInteractables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
    //         .OfType<IInteractable>()
    //         .ToArray();

    //     Debug.Log($"Found {allInteractables.Length} interactables in scene");

    //     foreach (var interactable in allInteractables)
    //     {
    //         if (interactable is ItemPickupInteractable pickup)
    //         {
    //             DebugItemPickup(pickup);
    //         }
    //         else if (interactable is DoorInteractable door)
    //         {
    //             DebugDoor(door);
    //         }
    //         else if (interactable is InteractableBase baseInteractable)
    //         {
    //             DebugGenericInteractable(baseInteractable);
    //         }
    //     }

    //     Debug.Log("=== END INTERACTABLES DEBUG ===");
    // }

    // private void DebugItemPickup(ItemPickupInteractable pickup)
    // {
    //     Debug.Log($"[ITEM PICKUP] {pickup.name}:");
    //     Debug.Log($"  - ID: {pickup.InteractableID}");
    //     Debug.Log($"  - Active: {pickup.gameObject.activeInHierarchy}");
    //     Debug.Log($"  - IsPickedUp: {pickup.IsPickedUp}");
    //     Debug.Log($"  - HasBeenUsed: {pickup.HasBeenUsed}");
    //     Debug.Log($"  - CanInteract: {pickup.CanInteract}");
    //     Debug.Log($"  - ItemData: {pickup.GetItemData()?.itemName ?? "NULL"}");
    //     Debug.Log($"  - SaveCategory: {pickup.SaveCategory}");
    // }

    // private void DebugDoor(DoorInteractable door)
    // {
    //     Debug.Log($"[DOOR] {door.name}:");
    //     Debug.Log($"  - ID: {door.InteractableID}");
    //     Debug.Log($"  - Active: {door.gameObject.activeInHierarchy}");
    //     Debug.Log($"  - IsOpen: {door.IsOpen}");
    //     Debug.Log($"  - IsLocked: {door.IsLocked}");
    //     Debug.Log($"  - HasBeenUsed: {door.HasBeenUsed}");
    //     Debug.Log($"  - CanInteract: {door.CanInteract}");
    // }

    // private void DebugGenericInteractable(InteractableBase interactable)
    // {
    //     Debug.Log($"[GENERIC] {interactable.name} ({interactable.GetType().Name}):");
    //     Debug.Log($"  - ID: {interactable.InteractableID}");
    //     Debug.Log($"  - Active: {interactable.gameObject.activeInHierarchy}");
    //     Debug.Log($"  - HasBeenUsed: {interactable.HasBeenUsed}");
    //     Debug.Log($"  - CanInteract: {interactable.CanInteract}");
    // }

    // [Button("Debug Save System")]
    // public void DebugSaveSystem()
    // {
    //     Debug.Log("=== SAVE SYSTEM DEBUG ===");

    //     var saveManager = SaveManager.Instance;
    //     var sceneDataManager = SceneDataManager.Instance;

    //     Debug.Log($"SaveManager exists: {saveManager != null}");
    //     Debug.Log($"SceneDataManager exists: {sceneDataManager != null}");
    //     Debug.Log($"Current scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

    //     if (ES3.FileExists("GameSave.es3"))
    //     {
    //         Debug.Log("Save file exists");
    //     }
    //     else
    //     {
    //         Debug.Log("No save file found");
    //     }
    // }

    // [Button("Force Save Scene Data")]
    // public void ForceSaveSceneData()
    // {
    //     if (SceneDataManager.Instance != null)
    //     {
    //         // This will save current scene data
    //         var sceneData = SceneDataManager.Instance.GetSceneDataForSaving();
    //         Debug.Log($"Forced scene save - found {sceneData.Count} scenes with data");

    //         string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    //         if (sceneData.ContainsKey(currentScene))
    //         {
    //             var currentSceneData = sceneData[currentScene];
    //             Debug.Log($"Current scene '{currentScene}' has {currentSceneData.objectData.Count} saved objects");

    //             foreach (var kvp in currentSceneData.objectData)
    //             {
    //                 Debug.Log($"  - Saved object: {kvp.Key} ({kvp.Value?.GetType().Name})");
    //             }
    //         }
    //         else
    //         {
    //             Debug.Log($"No save data for current scene: {currentScene}");
    //         }
    //     }
    // }

    // [Button("Test Scene Save/Load Process")]
    // public void TestActiveVsInactiveObjects()
    // {
    //     Debug.Log("=== TESTING ACTIVE VS INACTIVE OBJECT DETECTION ===");

    //     // Test FindObjectsByType with active objects only
    //     var activeInteractables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
    //         .OfType<IInteractable>()
    //         .ToArray();

    //     Debug.Log($"Found {activeInteractables.Length} ACTIVE interactables");

    //     // Test FindObjectsOfType with inactive objects (includes inactive)
    //     var allInteractables = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
    //         .OfType<IInteractable>()
    //         .Where(obj => obj != null && obj.Transform != null && obj.Transform.gameObject.scene.isLoaded)
    //         .ToArray();

    //     Debug.Log($"Found {allInteractables.Length} TOTAL interactables (including inactive)");

    //     foreach (var interactable in allInteractables)
    //     {
    //         bool isActive = interactable.Transform.gameObject.activeInHierarchy;
    //         string status = isActive ? "ACTIVE" : "INACTIVE";

    //         if (interactable is ItemPickupInteractable pickup)
    //         {
    //             Debug.Log($"[{status}] ItemPickup: {pickup.name} - IsPickedUp: {pickup.IsPickedUp}");

    //             // Check visual components
    //             var renderers = pickup.GetComponentsInChildren<Renderer>();
    //             var colliders = pickup.GetComponentsInChildren<Collider>();
    //             bool visualsHidden = renderers.All(r => !r.enabled);
    //             bool collidersDisabled = colliders.All(c => !c.enabled);

    //             Debug.Log($"  Renderers enabled: {!visualsHidden}, Colliders enabled: {!collidersDisabled}");
    //         }
    //     }
    // }
    // public void TestSceneSaveLoadProcess()
    // {
    //     Debug.Log("=== TESTING SCENE SAVE/LOAD PROCESS ===");

    //     // Step 1: Find all item pickups and check their current state
    //     var pickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);
    //     Debug.Log($"Found {pickups.Length} item pickups in scene");

    //     foreach (var pickup in pickups)
    //     {
    //         Debug.Log($"BEFORE SAVE - {pickup.name} (ID: {pickup.InteractableID}):");
    //         Debug.Log($"  - Active: {pickup.gameObject.activeInHierarchy}");
    //         Debug.Log($"  - IsPickedUp: {pickup.IsPickedUp}");
    //         Debug.Log($"  - HasBeenUsed: {pickup.HasBeenUsed}");
    //     }

    //     // Step 2: Force save scene data
    //     if (SceneDataManager.Instance != null)
    //     {
    //         Debug.Log("Forcing scene data save...");
    //         var sceneData = SceneDataManager.Instance.GetSceneDataForSaving();

    //         string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    //         if (sceneData.ContainsKey(currentScene))
    //         {
    //             var currentSceneData = sceneData[currentScene];
    //             Debug.Log($"Scene data saved - {currentSceneData.objectData.Count} objects saved for scene '{currentScene}'");

    //             // Check what data was saved for each pickup
    //             foreach (var pickup in pickups)
    //             {
    //                 var savedData = currentSceneData.GetObjectData<InteractableSaveData>(pickup.SaveID);
    //                 if (savedData != null)
    //                 {
    //                     Debug.Log($"SAVED DATA for {pickup.name}: hasBeenUsed={savedData.hasBeenUsed}, canInteract={savedData.canInteract}");
    //                     if (savedData.customData is ItemPickupSaveData pickupSaveData)
    //                     {
    //                         Debug.Log($"  Custom data: isPickedUp={pickupSaveData.isPickedUp}");
    //                     }
    //                 }
    //                 else
    //                 {
    //                     Debug.LogError($"NO SAVE DATA found for {pickup.name} (ID: {pickup.SaveID})");
    //                 }
    //             }
    //         }
    //         else
    //         {
    //             Debug.LogError($"No scene data saved for current scene: {currentScene}");
    //         }
    //     }
    //     else
    //     {
    //         Debug.LogError("SceneDataManager not found!");
    //     }
    // }
}