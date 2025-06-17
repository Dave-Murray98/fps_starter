using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// UI component for a single hotkey slot
/// UPDATED: Now gets item data directly from inventory
/// </summary>
public class HotkeySlotUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI keyNumberText;
    [SerializeField] private TextMeshProUGUI stackCountText;
    [SerializeField] private GameObject equippedIndicator;

    [Header("Visual Settings")]
    [SerializeField] private Color emptySlotColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color assignedSlotColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    [SerializeField] private Color equippedSlotColor = new Color(0f, 1f, 0f, 0.8f);

    private int slotNumber;
    private HotkeyBinding currentBinding;
    private ItemData currentItemData;
    private bool isCurrentlyEquipped = false;

    public void Initialize(int slot)
    {
        slotNumber = slot;

        // Set key number display
        if (keyNumberText != null)
        {
            keyNumberText.text = slot == 10 ? "0" : slot.ToString();
        }

        ClearSlot();
    }

    public void SetAssignedItem(HotkeyBinding binding, ItemData itemData)
    {
        currentBinding = binding;
        currentItemData = itemData;

        if (itemData != null)
        {
            // Update item icon
            if (itemIcon != null)
            {
                itemIcon.sprite = itemData.itemSprite;
                itemIcon.gameObject.SetActive(itemData.itemSprite != null);
            }

            // Update stack count
            if (stackCountText != null)
            {
                string stackInfo = binding.GetStackInfo();
                stackCountText.text = stackInfo;
                stackCountText.gameObject.SetActive(!string.IsNullOrEmpty(stackInfo));
            }

            // Update slot appearance
            if (slotBackground != null)
            {
                slotBackground.color = assignedSlotColor;
            }
        }

        RefreshEquippedState();
    }

    public void ClearSlot()
    {
        currentBinding = null;
        currentItemData = null;

        // Clear item icon
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.gameObject.SetActive(false);
        }

        // Clear stack count
        if (stackCountText != null)
        {
            stackCountText.text = "";
            stackCountText.gameObject.SetActive(false);
        }

        // Update slot appearance
        if (slotBackground != null)
        {
            slotBackground.color = emptySlotColor;
        }

        // Hide equipped indicator
        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(false);
        }

        isCurrentlyEquipped = false;
    }

    public void RefreshEquippedState()
    {
        if (EquippedItemManager.Instance == null || currentBinding == null) return;

        var equippedItem = EquippedItemManager.Instance.CurrentEquippedItem;
        isCurrentlyEquipped = equippedItem.isEquipped &&
                              equippedItem.isEquippedFromHotkey &&
                              equippedItem.sourceHotkeySlot == slotNumber;

        // Update visual state
        if (slotBackground != null)
        {
            slotBackground.color = isCurrentlyEquipped ? equippedSlotColor : assignedSlotColor;
        }

        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(isCurrentlyEquipped);
        }
    }
}