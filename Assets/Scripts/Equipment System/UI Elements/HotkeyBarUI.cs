using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UI component that displays the hotkey bar (1-0 slots)
/// FIXED: Now properly gets item data from inventory
/// </summary>
public class HotkeyBarUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform hotkeyContainer;
    [SerializeField] private GameObject hotkeySlotPrefab;

    // [Header("Settings")]
    // [SerializeField] private bool showEmptySlots = true;
    // [SerializeField] private bool autoHideWhenEmpty = false;
    // [SerializeField] private float slotSpacing = 5f;

    private Dictionary<int, HotkeySlotUI> hotkeySlots = new Dictionary<int, HotkeySlotUI>();

    private void Start()
    {
        CreateHotkeySlots();

        // Subscribe to equipment events
        if (EquippedItemManager.Instance != null)
        {
            EquippedItemManager.Instance.OnHotkeyAssigned += OnHotkeyAssigned;
            EquippedItemManager.Instance.OnHotkeyCleared += OnHotkeyCleared;
            EquippedItemManager.Instance.OnItemEquipped += OnItemEquipped;
        }

        RefreshAllSlots();
    }

    private void CreateHotkeySlots()
    {
        if (hotkeyContainer == null || hotkeySlotPrefab == null) return;

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
    }

    private void RefreshAllSlots()
    {
        if (EquippedItemManager.Instance == null || InventoryManager.Instance == null) return;

        var bindings = EquippedItemManager.Instance.GetAllHotkeyBindings();

        foreach (var binding in bindings)
        {
            if (hotkeySlots.TryGetValue(binding.slotNumber, out var slotUI))
            {
                if (binding.isAssigned)
                {
                    // Get the actual inventory item instead of loading from Resources
                    var inventoryItem = InventoryManager.Instance.InventoryData.GetItem(binding.itemId);
                    if (inventoryItem?.ItemData != null)
                    {
                        slotUI.SetAssignedItem(binding, inventoryItem.ItemData);
                    }
                    else
                    {
                        // Item no longer exists in inventory - clear the binding
                        Debug.LogWarning($"Item {binding.itemId} no longer exists in inventory - clearing hotkey {binding.slotNumber}");
                        binding.ClearSlot();
                        slotUI.ClearSlot();
                    }
                }
                else
                {
                    slotUI.ClearSlot();
                }
            }
        }
    }

    private void OnHotkeyAssigned(int slotNumber, HotkeyBinding binding)
    {
        if (hotkeySlots.TryGetValue(slotNumber, out var slotUI))
        {
            if (InventoryManager.Instance != null)
            {
                var inventoryItem = InventoryManager.Instance.InventoryData.GetItem(binding.itemId);
                if (inventoryItem?.ItemData != null)
                {
                    slotUI.SetAssignedItem(binding, inventoryItem.ItemData);
                }
                else
                {
                    // Item doesn't exist - clear the slot
                    slotUI.ClearSlot();
                }
            }
        }
    }

    private void OnHotkeyCleared(int slotNumber)
    {
        if (hotkeySlots.TryGetValue(slotNumber, out var slotUI))
        {
            slotUI.ClearSlot();
        }
    }

    private void OnItemEquipped(EquippedItemData equippedItem)
    {
        // Update visual state for currently equipped item
        foreach (var slot in hotkeySlots.Values)
        {
            slot.RefreshEquippedState();
        }
    }

    private void OnDestroy()
    {
        if (EquippedItemManager.Instance != null)
        {
            EquippedItemManager.Instance.OnHotkeyAssigned -= OnHotkeyAssigned;
            EquippedItemManager.Instance.OnHotkeyCleared -= OnHotkeyCleared;
            EquippedItemManager.Instance.OnItemEquipped -= OnItemEquipped;
        }
    }
}