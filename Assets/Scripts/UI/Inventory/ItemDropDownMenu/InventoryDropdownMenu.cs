using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Fixed dropdown menu system for inventory items
/// FIXED: Proper sizing calculation and layout timing
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

    [Header("Size Settings")]
    [SerializeField] private float buttonHeight = 35f;
    [SerializeField] private float buttonSpacing = 5f;
    [SerializeField] private float containerPadding = 15f;
    [SerializeField] private float minWidth = 140f;

    [Header("Click Detection")]
    [SerializeField] private bool detectClicksOutside = true;

    [Header("Position Settings")]
    public Vector2 positionOffset = new Vector2(10f, -10f);

    // Static reference for single dropdown policy
    private static InventoryDropdownMenu currentlyOpen = null;

    // State
    private InventoryItemData currentItem;
    private RectTransform rectTransform;
    private bool isVisible = false;
    private List<GameObject> currentButtons = new List<GameObject>();
    private bool ignoreNextClick = false; // To prevent immediate closure on opening

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
        // Subscribe to inventory close events only (removed pause game events)
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

    private void Update()
    {
        // Check for clicks outside the dropdown to close it (both left and right mouse buttons)
        if (isVisible && detectClicksOutside && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            CheckForClickOutside();
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
    /// Check if the mouse click was outside the dropdown area
    /// UPDATED: Now handles both left and right mouse button clicks
    /// </summary>
    private void CheckForClickOutside()
    {
        // Skip the check on the frame we just opened to prevent immediate closure
        if (ignoreNextClick)
        {
            ignoreNextClick = false;
            return;
        }

        Vector2 mousePosition = Input.mousePosition;

        // Convert mouse position to local coordinates relative to our canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 localPoint;

        // Convert screen point to canvas local point
        bool isInCanvas = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, mousePosition, canvas.worldCamera, out localPoint);

        if (!isInCanvas) return;

        // Convert canvas local point to dropdown local point
        Vector2 dropdownLocalPoint;
        bool isInDropdown = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, mousePosition, canvas.worldCamera, out dropdownLocalPoint);

        // If the click is not within our dropdown bounds, close the menu
        if (!isInDropdown || !rectTransform.rect.Contains(dropdownLocalPoint))
        {
            //  Debug.Log("[DropdownMenu] Click detected outside dropdown (left or right button) - closing menu");
            HideMenu();
        }
    }

    /// <summary>
    /// Show dropdown menu for the specified item at the specified screen position
    /// FIXED: Better timing for size calculation and proper GameObject activation
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

        // Debug the position offset being applied
        //        Debug.Log($"[DropdownMenu] ShowMenu called with screenPosition: {screenPosition}, offset: {positionOffset}, final position: {screenPosition + positionOffset}");

        // CRITICAL: Ensure the GameObject and dropdown panel are active before starting coroutine
        gameObject.SetActive(true);
        if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(true);
        }

        // Clear and create buttons
        ClearButtons();
        CreateButtonsForItemType(item.ItemData.itemType);

        // Start the showing process with proper timing
        StartCoroutine(ShowMenuCoroutine(screenPosition));
    }

    /// <summary>
    /// Coroutine to handle proper timing for showing menu
    /// FIXED: Proper GameObject state management and click ignore timing
    /// </summary>
    private IEnumerator ShowMenuCoroutine(Vector2 screenPosition)
    {
        // Set flag to ignore clicks during the opening process
        ignoreNextClick = true;

        // Set initial state - visible but transparent
        isVisible = true;
        canvasGroup.alpha = 0f;
        transform.localScale = Vector3.zero;

        // Wait for layout to calculate
        yield return new WaitForEndOfFrame();

        // Force layout rebuild
        if (buttonContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonContainer as RectTransform);
        }

        // Wait one more frame for layout to settle
        yield return new WaitForEndOfFrame();

        // Now calculate and set the proper size
        CalculateAndSetSize();

        // Position the dropdown
        PositionAtScreenPoint(screenPosition);

        // Finally animate in
        AnimateIn();

        // Wait for animation to complete, then allow click detection
        yield return new WaitForSeconds(fadeInDuration + 0.1f);

        // Reset the ignore flag after the dropdown is fully shown and animation is complete
        ignoreNextClick = false;
    }

    /// <summary>
    /// Calculate and set the proper size based on buttons
    /// FIXED: More reliable size calculation
    /// </summary>
    private void CalculateAndSetSize()
    {
        if (currentButtons.Count == 0)
        {
            // Default size if no buttons
            rectTransform.sizeDelta = new Vector2(minWidth, 60f);
            return;
        }

        // Calculate height based on button count
        float totalHeight = (currentButtons.Count * buttonHeight) +
                           ((currentButtons.Count - 1) * buttonSpacing) +
                           (containerPadding * 2);

        // Calculate width - use minimum width or content width, whichever is larger
        float calculatedWidth = minWidth;

        // Check if we can get actual button widths
        foreach (var button in currentButtons)
        {
            var buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                // Use the preferred width if it's larger
                var textComponent = button.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    float textWidth = textComponent.preferredWidth + 20f; // Add padding
                    calculatedWidth = Mathf.Max(calculatedWidth, textWidth);
                }
            }
        }

        // Set the calculated size
        Vector2 newSize = new Vector2(calculatedWidth, totalHeight);
        rectTransform.sizeDelta = newSize;

        // Debug.Log($"[DropdownMenu] Set size to: {newSize} for {currentButtons.Count} buttons (height: {buttonHeight}, spacing: {buttonSpacing}, padding: {containerPadding})");
    }

    /// <summary>
    /// Animate the dropdown in
    /// </summary>
    private void AnimateIn()
    {
        // Fade in
        canvasGroup.DOFade(1f, fadeInDuration);

        // Scale animation
        transform.DOScale(Vector3.one, fadeInDuration).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// Hide the dropdown menu
    /// FIXED: Proper GameObject deactivation
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
            if (dropdownPanel != null)
            {
                dropdownPanel.SetActive(false);
            }
            canvasGroup.alpha = 0f;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false); // Deactivate the entire GameObject
        }
        else
        {
            canvasGroup.DOFade(0f, fadeOutDuration)
                .OnComplete(() =>
                {
                    if (dropdownPanel != null)
                    {
                        dropdownPanel.SetActive(false);
                    }
                    transform.localScale = Vector3.zero;
                    gameObject.SetActive(false); // Deactivate after animation
                });
        }
    }

    /// <summary>
    /// Position the dropdown at the specified screen position with offset
    /// UPDATED: Now applies configurable position offset with debug logging
    /// </summary>
    private void PositionAtScreenPoint(Vector2 screenPosition)
    {
        // Apply the position offset to the screen position
        Vector2 adjustedScreenPosition = screenPosition + positionOffset;

        // Debug.Log($"[DropdownMenu] PositionAtScreenPoint - Original: {screenPosition}, Offset: {positionOffset}, Adjusted: {adjustedScreenPosition}");

        // Convert screen position to local position in the canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, adjustedScreenPosition, canvas.worldCamera, out localPoint);

        // Debug.Log($"[DropdownMenu] Converted to local point: {localPoint}");

        // Set position
        rectTransform.localPosition = localPoint;

        // Keep dropdown on screen
        ClampToScreen(canvasRect);

        //Debug.Log($"[DropdownMenu] Final position after clamping: {rectTransform.localPosition}");
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
    /// Create buttons based on item type
    /// </summary>
    private void CreateButtonsForItemType(ItemType itemType)
    {
        List<DropdownAction> actions = GetActionsForItemType(itemType);

        foreach (var action in actions)
        {
            CreateActionButton(action);
        }
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
    /// FIXED: Better button creation with proper sizing
    /// </summary>
    private void CreateActionButton(DropdownAction action)
    {
        GameObject buttonObj = Instantiate(buttonPrefab, buttonContainer);

        // Set the button size immediately
        var buttonRect = buttonObj.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.sizeDelta = new Vector2(minWidth - 10f, buttonHeight);
        }

        var button = buttonObj.GetComponent<Button>();
        var buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        var buttonImage = buttonObj.GetComponent<Image>();

        if (buttonText != null)
        {
            buttonText.text = action.displayName;
            buttonText.fontSize = 14f; // Ensure readable font size
        }

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
    /// Create default button prefab
    /// FIXED: Better default button with proper sizing
    /// </summary>
    private void CreateDefaultButtonPrefab()
    {
        GameObject button = new GameObject("DropdownButton");

        var rect = button.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(minWidth, buttonHeight);

        var buttonImage = button.AddComponent<Image>();
        buttonImage.color = normalButtonColor;

        var buttonComponent = button.AddComponent<Button>();
        buttonComponent.targetGraphic = buttonImage;

        // Add Layout Element to control sizing
        var layoutElement = button.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = buttonHeight;
        layoutElement.preferredWidth = minWidth;

        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(button.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Action";
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 14f;
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

    /// <summary>
    /// Public method to disable click-outside detection temporarily
    /// Useful when you want to prevent accidental closure
    /// </summary>
    public void SetClickOutsideDetection(bool enabled)
    {
        detectClicksOutside = enabled;
    }

    /// <summary>
    /// Set the position offset for this dropdown menu
    /// </summary>
    public void SetPositionOffset(Vector2 offset)
    {
        positionOffset = offset;
    }

    /// <summary>
    /// Get the current position offset
    /// </summary>
    public Vector2 GetPositionOffset()
    {
        return positionOffset;
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