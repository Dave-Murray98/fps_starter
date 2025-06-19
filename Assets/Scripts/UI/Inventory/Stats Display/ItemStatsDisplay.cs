using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// Displays detailed stats for selected inventory items
/// Shows stats when items are clicked, hovered, or being dragged
/// All-in-one component - no additional setup scripts needed
/// </summary>
public class ItemStatsDisplay : MonoBehaviour
{
    [Header("UI References - Assign from your prefab")]
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private TextMeshProUGUI itemStatsText;
    [SerializeField] private Image itemIconImage;
    [SerializeField] private GameObject noItemSelectedPanel;

    [Header("Styling")]
    [SerializeField] private Color defaultTextColor = Color.white;
    [SerializeField] private Color statValueColor = Color.cyan;
    [SerializeField] private Color degradationGoodColor = Color.green;
    [SerializeField] private Color degradationFairColor = Color.yellow;
    [SerializeField] private Color degradationPoorColor = new Color(195, 165, 0); //orange
    [SerializeField] private Color degradationCriticalColor = Color.red;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.15f;

    [Header("Debug/Testing")]
    [SerializeField] private ItemData testItem;

    private InventoryItemData currentDisplayedItem;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private bool isVisible = false;

    // Events
    public System.Action<InventoryItemData> OnItemDisplayed;
    public System.Action OnItemCleared;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Start hidden
        SetVisible(false, true);
    }

    private void Start()
    {
        // Subscribe to inventory system events
        SubscribeToInventoryEvents();

        // Setup initial UI state
        SetupInitialUI();

        // IMPORTANT: Auto-register with existing drag handlers
        RegisterExistingDragHandlers();
    }

    private void SubscribeToInventoryEvents()
    {
        // Subscribe to inventory manager events
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemRemoved += OnInventoryItemRemoved;
            InventoryManager.Instance.OnInventoryCleared += OnInventoryCleared;
        }

        // Subscribe to game events
        GameEvents.OnInventoryClosed += OnInventoryClosed;
    }

    private void SetupInitialUI()
    {
        // Show "no item selected" state initially
        ShowNoItemSelected();
    }

    /// <summary>
    /// Automatically register with any existing drag handlers in the scene
    /// This ensures the stats display works immediately, even with pre-existing items
    /// </summary>
    private void RegisterExistingDragHandlers()
    {
        // Wait a frame to ensure all inventory items are fully initialized
        StartCoroutine(RegisterExistingDragHandlersCoroutine());
    }

    private System.Collections.IEnumerator RegisterExistingDragHandlersCoroutine()
    {
        // Wait for inventory items to be fully set up
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame(); // Extra frame for safety

        var dragHandlers = FindObjectsByType<InventoryItemDragHandler>(FindObjectsSortMode.None);

        int registeredCount = 0;
        foreach (var handler in dragHandlers)
        {
            if (handler != null)
            {
                RegisterDragHandler(handler);
                registeredCount++;
            }
        }

        if (registeredCount > 0)
        {
            Debug.Log($"[ItemStatsDisplay] Auto-registered {registeredCount} existing drag handlers");
        }
        else
        {
            Debug.Log("[ItemStatsDisplay] No existing drag handlers found - will register new ones as they're created");
        }
    }

    /// <summary>
    /// Display stats for the specified item
    /// Called when an item is clicked, hovered, or being dragged
    /// </summary>
    public void DisplayItemStats(InventoryItemData itemData)
    {
        if (itemData?.ItemData == null)
        {
            ClearDisplay();
            return;
        }

        currentDisplayedItem = itemData;

        // Update item name
        if (itemNameText != null)
        {
            itemNameText.text = itemData.ItemData.itemName;
            itemNameText.color = GetItemNameColor(itemData.ItemData);
        }

        // Update item description
        if (itemDescriptionText != null)
        {
            itemDescriptionText.text = itemData.ItemData.description;
        }

        // Update item icon
        if (itemIconImage != null)
        {
            itemIconImage.sprite = itemData.ItemData.itemSprite;
            itemIconImage.gameObject.SetActive(itemData.ItemData.itemSprite != null);
        }

        // Update item stats
        if (itemStatsText != null)
        {
            itemStatsText.text = ItemStatsFormatter.FormatItemStats(itemData.ItemData);
        }

        // Show the stats panel
        ShowStatsPanel();
        SetVisible(true);

        // Fire event
        OnItemDisplayed?.Invoke(itemData);
    }

    /// <summary>
    /// Clear the stats display
    /// </summary>
    public void ClearDisplay()
    {
        currentDisplayedItem = null;
        ShowNoItemSelected();
        SetVisible(true); // Still show the panel, but with "no item selected"

        OnItemCleared?.Invoke();
    }

    /// <summary>
    /// Hide the entire stats display
    /// </summary>
    public void HideDisplay()
    {
        currentDisplayedItem = null;
        SetVisible(false);
    }

    private void ShowStatsPanel()
    {
        if (statsPanel != null)
        {
            statsPanel.SetActive(true);
        }

        if (noItemSelectedPanel != null)
        {
            noItemSelectedPanel.SetActive(false);
        }
    }

    private void ShowNoItemSelected()
    {
        if (statsPanel != null)
        {
            statsPanel.SetActive(false);
        }

        if (noItemSelectedPanel != null)
        {
            noItemSelectedPanel.SetActive(true);
        }
    }

    private void SetVisible(bool visible, bool immediate = false)
    {
        if (isVisible == visible && !immediate) return;

        isVisible = visible;

        if (immediate)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            gameObject.SetActive(visible);
        }
        else
        {
            if (visible)
            {
                gameObject.SetActive(true);
                canvasGroup.DOFade(1f, fadeInDuration);
            }
            else
            {
                canvasGroup.DOFade(0f, fadeOutDuration)
                    .OnComplete(() => gameObject.SetActive(false));
            }
        }
    }

    private Color GetItemNameColor(ItemData itemData)
    {
        // You can customize this based on item rarity, condition, etc.
        return defaultTextColor;
    }

    /// <summary>
    /// Register a drag handler to send events to this stats display
    /// Called by inventory drag handlers when they're created
    /// </summary>
    public void RegisterDragHandler(InventoryItemDragHandler dragHandler)
    {
        if (dragHandler != null)
        {
            dragHandler.OnItemSelected += DisplayItemStats;
            dragHandler.OnItemDeselected += ClearDisplay;
        }
    }

    /// <summary>
    /// Unregister a drag handler
    /// </summary>
    public void UnregisterDragHandler(InventoryItemDragHandler dragHandler)
    {
        if (dragHandler != null)
        {
            dragHandler.OnItemSelected -= DisplayItemStats;
            dragHandler.OnItemDeselected -= ClearDisplay;
        }
    }

    /// <summary>
    /// Check if an item is currently being displayed
    /// </summary>
    public bool IsDisplayingItem(InventoryItemData itemData)
    {
        return currentDisplayedItem == itemData;
    }

    /// <summary>
    /// Get the currently displayed item
    /// </summary>
    public InventoryItemData GetCurrentDisplayedItem()
    {
        return currentDisplayedItem;
    }

    /// <summary>
    /// Update the display if the current item's data has changed
    /// (e.g., condition changed due to use/repair)
    /// </summary>
    public void RefreshCurrentItem()
    {
        if (currentDisplayedItem != null)
        {
            DisplayItemStats(currentDisplayedItem);
        }
    }

    /// <summary>
    /// Static reference for easy access from other systems
    /// </summary>
    public static ItemStatsDisplay Instance { get; private set; }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Handle when an item is removed from inventory
    /// Clear display if we're showing the removed item
    /// </summary>
    private void OnInventoryItemRemoved(string itemId)
    {
        if (currentDisplayedItem != null && currentDisplayedItem.ID == itemId)
        {
            ClearDisplay();
        }
    }

    /// <summary>
    /// Handle when inventory is cleared
    /// </summary>
    private void OnInventoryCleared()
    {
        ClearDisplay();
    }

    /// <summary>
    /// Handle when inventory UI is closed
    /// </summary>
    private void OnInventoryClosed()
    {
        HideDisplay();
    }

    private void OnDestroy()
    {
        // Clean up any lingering tweens
        DOTween.Kill(this);

        // Unsubscribe from events
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemRemoved -= OnInventoryItemRemoved;
            InventoryManager.Instance.OnInventoryCleared -= OnInventoryCleared;
        }

        GameEvents.OnInventoryClosed -= OnInventoryClosed;
    }

    #region Setup and Testing Methods

    /// <summary>
    /// Test button to preview how stats will look
    /// </summary>
    [Button("Test Stats Display")]
    private void TestStatsDisplay()
    {
        if (testItem == null)
        {
            Debug.LogError("No test item assigned! Drag an ItemData into the Test Item field.");
            return;
        }

        // Create a test inventory item
        var testInventoryItem = new InventoryItemData("test_item", testItem, Vector2Int.zero);

        // Display its stats
        DisplayItemStats(testInventoryItem);

        Debug.Log($"Displaying stats for: {testItem.itemName}");
    }

    /// <summary>
    /// Clear the stats display
    /// </summary>
    [Button("Clear Display")]
    private void TestClearDisplay()
    {
        ClearDisplay();
        Debug.Log("Stats display cleared");
    }

    /// <summary>
    /// Hide the stats display completely
    /// </summary>
    [Button("Hide Display")]
    private void TestHideDisplay()
    {
        HideDisplay();
        Debug.Log("Stats display hidden");
    }

    /// <summary>
    /// Manually register all existing drag handlers with the stats display
    /// Useful if items are created after the stats display initializes
    /// </summary>
    [Button("Re-Register All Drag Handlers")]
    public void RegisterAllDragHandlers()
    {
        var dragHandlers = FindObjectsByType<InventoryItemDragHandler>(FindObjectsSortMode.None);

        int registeredCount = 0;
        foreach (var handler in dragHandlers)
        {
            if (handler != null)
            {
                RegisterDragHandler(handler);
                registeredCount++;
            }
        }

        Debug.Log($"[ItemStatsDisplay] Manually registered {registeredCount} drag handlers with stats display");

        if (registeredCount == 0)
        {
            Debug.LogWarning("No drag handlers found in scene. Make sure your inventory items have InventoryItemDragHandler components.");
        }
    }

    /// <summary>
    /// Static method to auto-register new drag handlers as they're created
    /// Call this from your InventoryGridVisual when it creates item visuals
    /// </summary>
    public static void AutoRegisterNewDragHandler(InventoryItemDragHandler dragHandler)
    {
        if (Instance != null)
        {
            Instance.RegisterDragHandler(dragHandler);
        }
    }

    /// <summary>
    /// Validate that all UI references are properly assigned
    /// </summary>
    [Button("Validate UI References")]
    private void ValidateUIReferences()
    {
        bool allValid = true;

        if (statsPanel == null)
        {
            Debug.LogError("Stats Panel is not assigned!");
            allValid = false;
        }

        if (itemNameText == null)
        {
            Debug.LogError("Item Name Text is not assigned!");
            allValid = false;
        }

        if (itemDescriptionText == null)
        {
            Debug.LogError("Item Description Text is not assigned!");
            allValid = false;
        }

        if (itemStatsText == null)
        {
            Debug.LogError("Item Stats Text is not assigned!");
            allValid = false;
        }

        if (itemIconImage == null)
        {
            Debug.LogWarning("Item Icon Image is not assigned (optional)");
        }

        if (noItemSelectedPanel == null)
        {
            Debug.LogWarning("No Item Selected Panel is not assigned (optional)");
        }

        if (allValid)
        {
            Debug.Log("✓ All essential UI references are properly assigned!");
        }
    }

    #endregion
}