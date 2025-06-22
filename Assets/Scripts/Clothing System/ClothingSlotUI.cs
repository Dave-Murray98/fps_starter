using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// Individual clothing slot UI component that handles display and drag/drop interaction.
/// Represents a single clothing layer where items can be equipped.
/// Supports drag and drop from inventory and unequipping back to inventory.
/// </summary>
public class ClothingSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image itemImage;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private Image conditionBar;
    [SerializeField] private GameObject conditionBarContainer;

    [Header("Visual Settings")]
    [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
    [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.5f, 0.9f);
    [SerializeField] private Color validDropColor = new Color(0.2f, 0.8f, 0.2f, 0.7f);
    [SerializeField] private Color invalidDropColor = new Color(0.8f, 0.2f, 0.2f, 0.7f);

    [Header("Condition Bar Colors")]
    [SerializeField] private Color goodConditionColor = Color.green;
    [SerializeField] private Color fairConditionColor = Color.yellow;
    [SerializeField] private Color poorConditionColor = Color.red;

    [Header("Animation Settings")]
    [SerializeField] private float hoverAnimationDuration = 0.2f;
    [SerializeField] private float equipAnimationDuration = 0.3f;

    // Configuration
    [SerializeField] private ClothingLayer targetLayer;
    private bool isInitialized = false;

    // References
    private ClothingManager clothingManager;
    private InventoryManager inventoryManager;

    // State
    private bool isHovering = false;
    private bool isDragOver = false;

    public ClothingLayer TargetLayer => targetLayer;

    private void Awake()
    {
        SetupUIComponents();
    }

    private void Start()
    {
        clothingManager = ClothingManager.Instance;
        inventoryManager = InventoryManager.Instance;

        if (!isInitialized)
        {
            Debug.LogWarning($"ClothingSlotUI {name} was not properly initialized!");
        }
    }

    /// <summary>
    /// Initialize the clothing slot UI for a specific layer
    /// </summary>
    public void Initialize(ClothingLayer layer)
    {
        targetLayer = layer;
        isInitialized = true;

        SetupUIComponents();
        UpdateSlotLabel();
        RefreshDisplay();

        Debug.Log($"ClothingSlotUI initialized for layer: {layer}");
    }

    /// <summary>
    /// Setup UI components if they're not assigned
    /// </summary>
    private void SetupUIComponents()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (itemImage == null)
        {
            // Look for child image component
            var images = GetComponentsInChildren<Image>();
            if (images.Length > 1)
                itemImage = images[1]; // First is background, second is item
        }

        if (slotLabel == null)
            slotLabel = GetComponentInChildren<TextMeshProUGUI>();

        if (conditionBar == null)
        {
            // Look for condition bar in children
            var conditionBarObj = transform.Find("ConditionBar");
            if (conditionBarObj != null)
            {
                conditionBar = conditionBarObj.GetComponent<Image>();
                conditionBarContainer = conditionBarObj.gameObject;
            }
        }

        // Create missing components if needed
        CreateMissingComponents();
    }

    /// <summary>
    /// Create missing UI components
    /// </summary>
    private void CreateMissingComponents()
    {
        // Ensure we have a background image
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.GetComponent<Image>();
            if (backgroundImage == null)
            {
                Debug.LogWarning($"ClothingSlotUI {name} is missing a background image! creating new one");
                backgroundImage = gameObject.AddComponent<Image>();
            }
        }

        // Create item image if missing
        if (itemImage == null)
        {
            GameObject itemImageObj = new GameObject("ItemImage");
            itemImageObj.transform.SetParent(transform, false);

            var rectTransform = itemImageObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(5, 5);
            rectTransform.offsetMax = new Vector2(-5, -5);

            itemImage = itemImageObj.AddComponent<Image>();
            itemImage.raycastTarget = false;
            itemImage.preserveAspect = true;
            Debug.Log($"ClothingSlotUI {name} is missing an item image! creating new one");
        }

        // Create slot label if missing
        if (slotLabel == null)
        {
            GameObject labelObj = new GameObject("SlotLabel");
            labelObj.transform.SetParent(transform, false);

            var rectTransform = labelObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 0.3f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            slotLabel = labelObj.AddComponent<TextMeshProUGUI>();
            slotLabel.text = "Slot";
            slotLabel.fontSize = 8f;
            slotLabel.color = Color.white;
            slotLabel.alignment = TextAlignmentOptions.Center;
            slotLabel.raycastTarget = false;
            Debug.Log($"ClothingSlotUI {name} is missing a slot label! creating new one");
        }

        // Create condition bar if missing
        if (conditionBar == null)
        {
            CreateConditionBar();
            Debug.Log($"ClothingSlotUI {name} is missing a condition bar! creating new one");
        }
    }

    /// <summary>
    /// Create the condition bar UI
    /// </summary>
    private void CreateConditionBar()
    {
        // Create container
        GameObject containerObj = new GameObject("ConditionBarContainer");
        containerObj.transform.SetParent(transform, false);

        var containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0.7f);
        containerRect.anchorMax = new Vector2(1, 0.8f);
        containerRect.offsetMin = new Vector2(2, 0);
        containerRect.offsetMax = new Vector2(-2, 0);

        conditionBarContainer = containerObj;

        // Create background bar
        GameObject backgroundBarObj = new GameObject("ConditionBarBackground");
        backgroundBarObj.transform.SetParent(containerObj.transform, false);

        var backgroundRect = backgroundBarObj.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var backgroundBarImage = backgroundBarObj.AddComponent<Image>();
        backgroundBarImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        backgroundBarImage.raycastTarget = false;

        // Create condition bar
        GameObject conditionBarObj = new GameObject("ConditionBar");
        conditionBarObj.transform.SetParent(containerObj.transform, false);

        var conditionRect = conditionBarObj.AddComponent<RectTransform>();
        conditionRect.anchorMin = Vector2.zero;
        conditionRect.anchorMax = Vector2.one;
        conditionRect.offsetMin = Vector2.zero;
        conditionRect.offsetMax = Vector2.zero;

        conditionBar = conditionBarObj.AddComponent<Image>();
        conditionBar.color = goodConditionColor;
        conditionBar.type = Image.Type.Filled;
        conditionBar.fillMethod = Image.FillMethod.Horizontal;
        conditionBar.raycastTarget = false;
    }

    /// <summary>
    /// Update the slot label text
    /// </summary>
    private void UpdateSlotLabel()
    {
        if (slotLabel == null || !isInitialized) return;

        string labelText = GetShortLayerName(targetLayer);
        slotLabel.text = labelText;
    }

    /// <summary>
    /// Get a short display name for the clothing layer
    /// </summary>
    private string GetShortLayerName(ClothingLayer layer)
    {
        return layer switch
        {
            ClothingLayer.HeadUpper => "Hat",
            ClothingLayer.HeadLower => "Scarf",
            ClothingLayer.TorsoInner => "Shirt",
            ClothingLayer.TorsoOuter => "Jacket",
            ClothingLayer.LegsInner => "Under",
            ClothingLayer.LegsOuter => "Pants",
            ClothingLayer.Hands => "Gloves",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => layer.ToString()
        };
    }

    /// <summary>
    /// Refresh the slot's visual display based on current state
    /// </summary>
    public void RefreshDisplay()
    {
        if (!isInitialized || clothingManager == null) return;

        var slot = clothingManager.GetSlot(targetLayer);
        if (slot == null) return;

        bool isEmpty = slot.IsEmpty;
        var equippedItem = slot.GetEquippedItem();

        // Update background color
        Color targetColor = isEmpty ? emptySlotColor : occupiedSlotColor;
        if (isHovering)
            targetColor = hoverColor;

        if (backgroundImage != null)
            backgroundImage.color = targetColor;

        // Update item image
        if (itemImage != null)
        {
            if (isEmpty || equippedItem?.ItemData?.itemSprite == null)
            {
                itemImage.sprite = null;
                itemImage.color = Color.clear;
            }
            else
            {
                itemImage.sprite = equippedItem.ItemData.itemSprite;
                itemImage.color = Color.white;
            }
        }

        // Update condition bar
        UpdateConditionBar(slot);
    }

    /// <summary>
    /// Update the condition bar based on equipped item
    /// </summary>
    private void UpdateConditionBar(ClothingSlot slot)
    {
        if (conditionBarContainer == null || conditionBar == null) return;

        bool isEmpty = slot.IsEmpty;
        conditionBarContainer.SetActive(!isEmpty);

        if (!isEmpty)
        {
            var clothingData = slot.GetEquippedClothingData();
            if (clothingData != null)
            {
                float conditionPercentage = clothingData.ConditionPercentage;
                conditionBar.fillAmount = conditionPercentage;

                // Set color based on condition
                if (conditionPercentage >= 0.7f)
                    conditionBar.color = goodConditionColor;
                else if (conditionPercentage >= 0.3f)
                    conditionBar.color = fairConditionColor;
                else
                    conditionBar.color = poorConditionColor;
            }
        }
    }

    /// <summary>
    /// Get the ID of the currently equipped item
    /// </summary>
    public string GetEquippedItemId()
    {
        if (!isInitialized || clothingManager == null) return "";

        var slot = clothingManager.GetSlot(targetLayer);
        return slot?.equippedItemId ?? "";
    }

    #region Event Handlers

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Right click to unequip
            UnequipItem();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        if (backgroundImage != null)
        {
            backgroundImage.DOColor(hoverColor, hoverAnimationDuration);
        }

        // Could show tooltip here in the future
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (!isDragOver && backgroundImage != null)
        {
            Color targetColor = GetCurrentTargetColor();
            backgroundImage.DOColor(targetColor, hoverAnimationDuration);
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        isDragOver = false;

        var draggedObject = eventData.pointerDrag;
        if (draggedObject == null) return;

        var dragHandler = draggedObject.GetComponent<InventoryItemDragHandler>();
        if (dragHandler == null) return;

        // Get the item data from the drag handler
        var itemData = GetItemDataFromDragHandler(dragHandler);
        if (itemData == null || itemData.ItemData?.itemType != ItemType.Clothing)
        {
            Debug.Log("Cannot equip - not a clothing item");
            RefreshDisplay();
            return;
        }

        var clothingData = itemData.ItemData.ClothingData;
        if (clothingData == null || !clothingData.CanEquipToLayer(targetLayer))
        {
            Debug.Log($"Cannot equip {itemData.ItemData.itemName} to {targetLayer} - not compatible");
            RefreshDisplay();
            return;
        }

        // Equip the item
        bool success = clothingManager.EquipItemToLayer(itemData.ID, targetLayer);
        if (success)
        {
            Debug.Log($"Successfully equipped {itemData.ItemData.itemName} to {targetLayer}");

            // Animate the equipping
            AnimateItemEquipped();
        }
        else
        {
            Debug.LogWarning($"Failed to equip {itemData.ItemData.itemName} to {targetLayer}");
        }

        RefreshDisplay();
    }

    /// <summary>
    /// Get item data from drag handler using reflection
    /// </summary>
    private InventoryItemData GetItemDataFromDragHandler(InventoryItemDragHandler dragHandler)
    {
        // Use reflection to access the private itemData field
        var field = dragHandler.GetType().GetField("itemData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return field?.GetValue(dragHandler) as InventoryItemData;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Unequip the currently equipped item
    /// </summary>
    private void UnequipItem()
    {
        if (!isInitialized || clothingManager == null) return;

        bool success = clothingManager.UnequipItemFromLayer(targetLayer);
        if (success)
        {
            Debug.Log($"Unequipped item from {targetLayer}");
            RefreshDisplay();
        }
    }

    /// <summary>
    /// Get the current target color for the background
    /// </summary>
    private Color GetCurrentTargetColor()
    {
        if (!isInitialized || clothingManager == null) return emptySlotColor;

        var slot = clothingManager.GetSlot(targetLayer);
        return (slot?.IsEmpty ?? true) ? emptySlotColor : occupiedSlotColor;
    }

    /// <summary>
    /// Animate the item being equipped
    /// </summary>
    private void AnimateItemEquipped()
    {
        if (itemImage != null)
        {
            // Scale animation to show item being equipped
            itemImage.transform.localScale = Vector3.zero;
            itemImage.transform.DOScale(Vector3.one, equipAnimationDuration)
                .SetEase(Ease.OutBack);
        }
    }

    #endregion

    #region Debug

    /// <summary>
    /// Get debug information about this slot
    /// </summary>
    public string GetDebugInfo()
    {
        if (!isInitialized) return $"Slot[{name}]: Not initialized";

        var slot = clothingManager?.GetSlot(targetLayer);
        if (slot == null) return $"Slot[{targetLayer}]: No clothing slot found";

        return slot.GetDebugInfo();
    }

    #endregion
}