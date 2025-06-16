using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// Simple dropdown menu system for inventory items
/// Much cleaner approach without unnecessary complexity
/// </summary>
public class InventoryDropdownMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dropdownPanel;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float fadeOutDuration = 0.1f;

    [Header("Button Settings")]
    [SerializeField] private Color normalButtonColor = Color.white;
    [SerializeField] private Color hoverButtonColor = new Color(0.8f, 0.8f, 1f);
    [SerializeField] private Color disabledButtonColor = Color.gray;

    // Static reference for single dropdown policy
    private static InventoryDropdownMenu currentlyOpen = null;

    // State
    private InventoryItemData currentItem;
    private RectTransform rectTransform;
    private bool isVisible = false;
    private List<GameObject> currentButtons = new List<GameObject>();

    // Events
    public System.Action<InventoryItemData, string> OnActionSelected;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // Create default button prefab if none assigned
        if (buttonPrefab == null)
            CreateDefaultButtonPrefab();

        // Initially hidden
        HideMenu(true);
    }

    private void Start()
    {
        // Subscribe to inventory close events
        GameEvents.OnInventoryClosed += OnInventoryClosed;
    }

    private void OnDestroy()
    {
        GameEvents.OnInventoryClosed -= OnInventoryClosed;

        // Clear static reference if this is the currently open dropdown
        if (currentlyOpen == this)
        {
            currentlyOpen = null;
        }
    }

    private void OnInventoryClosed()
    {
        if (isVisible)
        {
            HideMenu();
        }
    }

    /// <summary>
    /// Show dropdown menu for the specified item at the specified screen position
    /// </summary>
    public void ShowMenu(InventoryItemData item, Vector2 screenPosition)
    {
        if (item?.ItemData == null) return;

        // Close any currently open dropdown first
        if (currentlyOpen != null)
        {
            currentlyOpen.HideMenu(true); // Immediate close
        }

        // Set this as the currently open dropdown
        currentlyOpen = this;
        currentItem = item;

        // Clear and create buttons
        ClearButtons();
        CreateButtonsForItemType(item.ItemData.itemType);

        // Show the dropdown first (so layout can be calculated)
        ShowDropdown();

        // THEN position and size after buttons are created and laid out
        StartCoroutine(PositionAndSizeAfterLayout(screenPosition));
    }

    /// <summary>
    /// Position and size the dropdown after the layout has been calculated
    /// </summary>
    private System.Collections.IEnumerator PositionAndSizeAfterLayout(Vector2 screenPosition)
    {
        // Wait for layout to be calculated
        yield return new WaitForEndOfFrame();

        // Now adjust the size based on actual button layout
        AdjustDropdownSize();

        // Then position it properly
        PositionAtScreenPoint(screenPosition);
    }

    /// <summary>
    /// Hide the dropdown menu
    /// </summary>
    public void HideMenu(bool immediate = false)
    {
        if (!isVisible && !immediate) return;

        isVisible = false;
        currentItem = null;

        // Clear static reference if this was the currently open dropdown
        if (currentlyOpen == this)
        {
            currentlyOpen = null;
        }

        if (immediate)
        {
            dropdownPanel.SetActive(false);
            canvasGroup.alpha = 0f;
        }
        else
        {
            canvasGroup.DOFade(0f, fadeOutDuration)
                .OnComplete(() => dropdownPanel.SetActive(false));
        }
    }

    /// <summary>
    /// Position the dropdown at the specified screen position
    /// </summary>
    private void PositionAtScreenPoint(Vector2 screenPosition)
    {
        // Convert screen position to local position in the canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPosition, canvas.worldCamera, out localPoint);

        // Set position
        rectTransform.localPosition = localPoint;

        // Keep dropdown on screen
        ClampToScreen(canvasRect);
    }

    /// <summary>
    /// Ensure the dropdown stays within screen bounds
    /// </summary>
    private void ClampToScreen(RectTransform canvasRect)
    {
        Vector3 localPos = rectTransform.localPosition;
        Vector2 dropdownSize = rectTransform.sizeDelta;
        Vector2 canvasSize = canvasRect.sizeDelta;

        // Calculate bounds
        float minX = -canvasSize.x * 0.5f;
        float maxX = canvasSize.x * 0.5f - dropdownSize.x;
        float minY = -canvasSize.y * 0.5f + dropdownSize.y;
        float maxY = canvasSize.y * 0.5f;

        // Clamp position
        localPos.x = Mathf.Clamp(localPos.x, minX, maxX);
        localPos.y = Mathf.Clamp(localPos.y, minY, maxY);

        rectTransform.localPosition = localPos;
    }

    /// <summary>
    /// Show the dropdown with animation
    /// </summary>
    private void ShowDropdown()
    {
        isVisible = true;
        dropdownPanel.SetActive(true);

        // Animate in
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, fadeInDuration);

        // Scale animation
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, fadeInDuration).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// Create buttons based on item type
    /// </summary>
    private void CreateButtonsForItemType(ItemType itemType)
    {
        List<DropdownAction> actions = GetActionsForItemType(itemType);

        foreach (var action in actions)
        {
            CreateActionButton(action);
        }

        // Adjust dropdown size to fit buttons
        AdjustDropdownSize();
    }

    /// <summary>
    /// Get available actions for the specified item type
    /// </summary>
    private List<DropdownAction> GetActionsForItemType(ItemType itemType)
    {
        var actions = new List<DropdownAction>();

        switch (itemType)
        {
            case ItemType.Consumable:
                actions.Add(new DropdownAction("Consume", "consume", true));
                actions.Add(new DropdownAction("Equip", "equip", true));
                actions.Add(new DropdownAction("Assign Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.Weapon:
                actions.Add(new DropdownAction("Equip", "equip", true));
                actions.Add(new DropdownAction("Assign Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Unload", "unload", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.Equipment:
                actions.Add(new DropdownAction("Equip", "equip", true));
                actions.Add(new DropdownAction("Assign Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;

            case ItemType.KeyItem:
                actions.Add(new DropdownAction("Equip", "equip", true));
                actions.Add(new DropdownAction("Assign Hotkey", "assign_hotkey", true));
                // Note: Key items cannot be dropped
                break;

            case ItemType.Ammo:
                actions.Add(new DropdownAction("Equip", "equip", true));
                actions.Add(new DropdownAction("Assign Hotkey", "assign_hotkey", true));
                actions.Add(new DropdownAction("Drop", "drop", true));
                break;
        }

        return actions;
    }

    /// <summary>
    /// Create a button for the specified action
    /// </summary>
    private void CreateActionButton(DropdownAction action)
    {
        GameObject buttonObj = Instantiate(buttonPrefab, buttonContainer);

        var button = buttonObj.GetComponent<Button>();
        var buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        var buttonImage = buttonObj.GetComponent<Image>();

        if (buttonText != null)
            buttonText.text = action.displayName;

        if (buttonImage != null)
            buttonImage.color = action.isEnabled ? normalButtonColor : disabledButtonColor;

        if (button != null)
        {
            button.interactable = action.isEnabled;

            if (action.isEnabled)
            {
                button.onClick.AddListener(() => OnActionButtonClicked(action.actionId));

                // Add hover effects
                AddHoverEffects(buttonObj, buttonImage);
            }
        }

        currentButtons.Add(buttonObj);
    }

    /// <summary>
    /// Add hover effects to button
    /// </summary>
    private void AddHoverEffects(GameObject buttonObj, Image buttonImage)
    {
        var trigger = buttonObj.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = buttonObj.AddComponent<EventTrigger>();

        // Mouse enter
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((data) =>
        {
            if (buttonImage != null)
                buttonImage.DOColor(hoverButtonColor, 0.1f);
        });
        trigger.triggers.Add(enterEntry);

        // Mouse exit
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) =>
        {
            if (buttonImage != null)
                buttonImage.DOColor(normalButtonColor, 0.1f);
        });
        trigger.triggers.Add(exitEntry);
    }

    /// <summary>
    /// Handle button click
    /// </summary>
    private void OnActionButtonClicked(string actionId)
    {
        OnActionSelected?.Invoke(currentItem, actionId);
        HideMenu();
    }

    /// <summary>
    /// Clear all current buttons
    /// </summary>
    private void ClearButtons()
    {
        foreach (var button in currentButtons)
        {
            if (button != null)
                Destroy(button);
        }
        currentButtons.Clear();
    }

    /// <summary>
    /// Adjust dropdown size to fit buttons properly
    /// </summary>
    private void AdjustDropdownSize()
    {
        if (buttonContainer == null || currentButtons.Count == 0)
        {
            // Default size if no buttons
            rectTransform.sizeDelta = new Vector2(150, 50);
            return;
        }

        // Force layout rebuild to ensure button sizes are calculated
        LayoutRebuilder.ForceRebuildLayoutImmediate(buttonContainer as RectTransform);

        // Wait one more frame for layout to settle
        Canvas.ForceUpdateCanvases();

        // Calculate required size based on actual button dimensions
        float totalHeight = 0f;
        float maxWidth = 100f; // Minimum width

        foreach (var button in currentButtons)
        {
            var buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                totalHeight += buttonRect.sizeDelta.y;
                maxWidth = Mathf.Max(maxWidth, buttonRect.sizeDelta.x);
            }
        }

        // Add spacing between buttons (from VerticalLayoutGroup)
        var layoutGroup = buttonContainer.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null && currentButtons.Count > 1)
        {
            totalHeight += layoutGroup.spacing * (currentButtons.Count - 1);
        }

        // Add container padding
        totalHeight += 20f; // Top and bottom padding
        maxWidth += 20f;    // Left and right padding

        // Set the new size
        Vector2 newSize = new Vector2(maxWidth, totalHeight);
        rectTransform.sizeDelta = newSize;

        Debug.Log($"Adjusted dropdown size to: {newSize} for {currentButtons.Count} buttons");
    }

    /// <summary>
    /// Create default button prefab
    /// </summary>
    private void CreateDefaultButtonPrefab()
    {
        GameObject button = new GameObject("DropdownButton");

        var rect = button.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(140, 30); // Increased height from 25 to 30

        var buttonImage = button.AddComponent<Image>();
        buttonImage.color = normalButtonColor;

        var buttonComponent = button.AddComponent<Button>();
        buttonComponent.targetGraphic = buttonImage;

        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(button.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 2); // Increased padding
        textRect.offsetMax = new Vector2(-8, -2);

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Action";
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 14; // Increased from 12 to 14
        text.fontStyle = FontStyles.Normal;

        buttonPrefab = button;
    }

    /// <summary>
    /// Check if this dropdown is currently visible
    /// </summary>
    public bool IsVisible => isVisible;

    /// <summary>
    /// Static method to close any currently open dropdown
    /// </summary>
    public static void CloseAnyOpenDropdown()
    {
        if (currentlyOpen != null)
        {
            currentlyOpen.HideMenu(true);
        }
    }
}

/// <summary>
/// Simple dropdown action data
/// </summary>
[System.Serializable]
public class DropdownAction
{
    public string displayName;
    public string actionId;
    public bool isEnabled;

    public DropdownAction(string display, string id, bool enabled)
    {
        displayName = display;
        actionId = id;
        isEnabled = enabled;
    }
}