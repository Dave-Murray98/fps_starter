using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// UI component that displays the hotkey bar (1-0 slots)
/// FIXED: Now properly refreshes after save/load and scene transitions
/// </summary>
public class HotkeyBarUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform hotkeyContainer;
    [SerializeField] private GameObject hotkeySlotPrefab;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = false;

    private Dictionary<int, HotkeySlotUI> hotkeySlots = new Dictionary<int, HotkeySlotUI>();
    private bool isInitialized = false;

    private void Start()
    {
        // Delay initialization to ensure managers are ready
        StartCoroutine(InitializeWithDelay());
    }

    private void OnEnable()
    {
        // Re-subscribe and refresh when enabled (important for scene transitions)
        StartCoroutine(RefreshOnEnable());
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    /// <summary>
    /// Initialize with a slight delay to ensure all managers are ready
    /// </summary>
    private IEnumerator InitializeWithDelay()
    {
        // Wait a frame to ensure all managers are initialized
        yield return null;

        // Wait for managers to be ready
        yield return new WaitUntil(() => EquippedItemManager.Instance != null && InventoryManager.Instance != null);

        CreateHotkeySlots();
        SubscribeToEvents();

        // Wait another frame then refresh
        yield return null;
        RefreshAllSlots();

        isInitialized = true;
        DebugLog("HotkeyBarUI initialized successfully");
    }

    /// <summary>
    /// Refresh when component is enabled (important for scene transitions)
    /// </summary>
    private IEnumerator RefreshOnEnable()
    {
        if (!isInitialized) yield break;

        // Wait for managers to be ready after scene transition
        yield return new WaitUntil(() => EquippedItemManager.Instance != null && InventoryManager.Instance != null);

        // Re-subscribe to events (they might have been lost during scene transition)
        UnsubscribeFromEvents();
        SubscribeToEvents();

        // Wait a frame then refresh
        yield return null;
        RefreshAllSlots();

        DebugLog("HotkeyBarUI refreshed on enable");
    }

    private void CreateHotkeySlots()
    {
        if (hotkeyContainer == null || hotkeySlotPrefab == null)
        {
            Debug.LogError("HotkeyBarUI: Missing references - hotkeyContainer or hotkeySlotPrefab is null!");
            return;
        }

        // Clear existing slots
        foreach (var slot in hotkeySlots.Values)
        {
            if (slot != null && slot.gameObject != null)
                DestroyImmediate(slot.gameObject);
        }
        hotkeySlots.Clear();

        // Create 10 slots (1-0)
        for (int i = 1; i <= 10; i++)
        {
            GameObject slotObj = Instantiate(hotkeySlotPrefab, hotkeyContainer);
            slotObj.name = $"HotkeySlot_{i}";

            var slotUI = slotObj.GetComponent<HotkeySlotUI>();
            if (slotUI == null)
            {
                slotUI = slotObj.AddComponent<HotkeySlotUI>();
            }

            slotUI.Initialize(i);
            hotkeySlots[i] = slotUI;
        }

        DebugLog($"Created {hotkeySlots.Count} hotkey slots");
    }

    private void SubscribeToEvents()
    {
        if (EquippedItemManager.Instance != null)
        {
            EquippedItemManager.Instance.OnHotkeyAssigned -= OnHotkeyAssigned;
            EquippedItemManager.Instance.OnHotkeyCleared -= OnHotkeyCleared;
            EquippedItemManager.Instance.OnItemEquipped -= OnItemEquipped;

            EquippedItemManager.Instance.OnHotkeyAssigned += OnHotkeyAssigned;
            EquippedItemManager.Instance.OnHotkeyCleared += OnHotkeyCleared;
            EquippedItemManager.Instance.OnItemEquipped += OnItemEquipped;

            DebugLog("Subscribed to EquippedItemManager events");
        }
        else
        {
            DebugLog("EquippedItemManager.Instance is null - cannot subscribe to events");
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (EquippedItemManager.Instance != null)
        {
            EquippedItemManager.Instance.OnHotkeyAssigned -= OnHotkeyAssigned;
            EquippedItemManager.Instance.OnHotkeyCleared -= OnHotkeyCleared;
            EquippedItemManager.Instance.OnItemEquipped -= OnItemEquipped;
        }
    }

    private void RefreshAllSlots()
    {
        if (EquippedItemManager.Instance == null)
        {
            DebugLog("Cannot refresh slots - EquippedItemManager.Instance is null");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            DebugLog("Cannot refresh slots - InventoryManager.Instance is null");
            return;
        }

        var bindings = EquippedItemManager.Instance.GetAllHotkeyBindings();
        if (bindings == null)
        {
            DebugLog("Cannot refresh slots - hotkey bindings is null");
            return;
        }

        DebugLog($"Refreshing all slots - found {bindings.Count} bindings");

        foreach (var binding in bindings)
        {
            if (hotkeySlots.TryGetValue(binding.slotNumber, out var slotUI))
            {
                if (binding.isAssigned)
                {
                    // FIXED: Better validation and error handling
                    var inventoryItem = InventoryManager.Instance.InventoryData.GetItem(binding.itemId);
                    if (inventoryItem?.ItemData != null)
                    {
                        slotUI.SetAssignedItem(binding, inventoryItem.ItemData);
                        DebugLog($"Refreshed slot {binding.slotNumber}: {inventoryItem.ItemData.itemName}");
                    }
                    else
                    {
                        // Item no longer exists in inventory - clear the binding
                        DebugLog($"Item {binding.itemId} no longer exists in inventory - clearing hotkey {binding.slotNumber}");

                        // Clear the binding in the manager
                        binding.ClearSlot();

                        // Clear the UI slot
                        slotUI.ClearSlot();

                        // Notify the manager about the change
                        EquippedItemManager.Instance.OnHotkeyCleared?.Invoke(binding.slotNumber);
                    }
                }
                else
                {
                    slotUI.ClearSlot();
                    DebugLog($"Cleared slot {binding.slotNumber}");
                }
            }
            else
            {
                DebugLog($"Could not find slot UI for slot number {binding.slotNumber}");
            }
        }

        DebugLog("All slots refreshed");
    }

    private void OnHotkeyAssigned(int slotNumber, HotkeyBinding binding)
    {
        DebugLog($"Hotkey assigned event received for slot {slotNumber}");

        if (hotkeySlots.TryGetValue(slotNumber, out var slotUI))
        {
            if (InventoryManager.Instance != null)
            {
                var inventoryItem = InventoryManager.Instance.InventoryData.GetItem(binding.itemId);
                if (inventoryItem?.ItemData != null)
                {
                    slotUI.SetAssignedItem(binding, inventoryItem.ItemData);
                    DebugLog($"Updated slot {slotNumber} with {inventoryItem.ItemData.itemName}");
                }
                else
                {
                    // Item doesn't exist - clear the slot
                    slotUI.ClearSlot();
                    DebugLog($"Item not found for slot {slotNumber} - cleared slot");
                }
            }
            else
            {
                DebugLog("InventoryManager.Instance is null in OnHotkeyAssigned");
            }
        }
        else
        {
            DebugLog($"Slot UI not found for slot number {slotNumber}");
        }
    }

    private void OnHotkeyCleared(int slotNumber)
    {
        DebugLog($"Hotkey cleared event received for slot {slotNumber}");

        if (hotkeySlots.TryGetValue(slotNumber, out var slotUI))
        {
            slotUI.ClearSlot();
            DebugLog($"Cleared slot {slotNumber}");
        }
    }

    private void OnItemEquipped(EquippedItemData equippedItem)
    {
        DebugLog($"Item equipped event received");

        // Update visual state for currently equipped item
        foreach (var slot in hotkeySlots.Values)
        {
            slot.RefreshEquippedState();
        }
    }

    /// <summary>
    /// Force refresh all slots (useful for debugging or manual refresh)
    /// </summary>
    [ContextMenu("Force Refresh All Slots")]
    public void ForceRefreshAllSlots()
    {
        DebugLog("Force refreshing all slots...");
        RefreshAllSlots();
    }

    /// <summary>
    /// PUBLIC: Method to manually trigger a refresh (can be called by other systems)
    /// </summary>
    public void ManualRefresh()
    {
        if (!isInitialized)
        {
            DebugLog("HotkeyBarUI not initialized yet - cannot manual refresh");
            return;
        }

        StartCoroutine(ManualRefreshCoroutine());
    }

    private IEnumerator ManualRefreshCoroutine()
    {
        // Wait for managers to be ready
        yield return new WaitUntil(() => EquippedItemManager.Instance != null && InventoryManager.Instance != null);

        // Re-subscribe to events
        UnsubscribeFromEvents();
        SubscribeToEvents();

        // Refresh all slots
        RefreshAllSlots();

        DebugLog("Manual refresh completed");
    }

    /// <summary>
    /// Get debug information about current state
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"HotkeyBarUI Debug Info:");
        info.AppendLine($"  Initialized: {isInitialized}");
        info.AppendLine($"  Slot Count: {hotkeySlots.Count}");
        info.AppendLine($"  EquippedItemManager: {(EquippedItemManager.Instance != null ? "Found" : "NULL")}");
        info.AppendLine($"  InventoryManager: {(InventoryManager.Instance != null ? "Found" : "NULL")}");

        if (EquippedItemManager.Instance != null)
        {
            var bindings = EquippedItemManager.Instance.GetAllHotkeyBindings();
            var assignedCount = bindings?.FindAll(b => b.isAssigned)?.Count ?? 0;
            info.AppendLine($"  Assigned Hotkeys: {assignedCount}/10");
        }

        return info.ToString();
    }

    /// <summary>
    /// Check if a specific slot is properly set up
    /// </summary>
    public bool IsSlotValid(int slotNumber)
    {
        return hotkeySlots.ContainsKey(slotNumber) && hotkeySlots[slotNumber] != null;
    }

    /// <summary>
    /// Get the UI component for a specific slot
    /// </summary>
    public HotkeySlotUI GetSlotUI(int slotNumber)
    {
        return hotkeySlots.GetValueOrDefault(slotNumber);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HotkeyBarUI] {message}");
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        DebugLog("HotkeyBarUI destroyed");
    }
}