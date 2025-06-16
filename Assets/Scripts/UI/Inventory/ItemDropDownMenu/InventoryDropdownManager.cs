using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple manager that creates and configures the inventory dropdown menu
/// Much cleaner approach without unnecessary complexity
/// </summary>
public class InventoryDropdownManager : MonoBehaviour
{
    [Header("Dropdown Settings")]
    [SerializeField] private GameObject dropdownMenuPrefab;
    [SerializeField] private Transform dropdownMenuParent;

    [Header("Styling")]
    [SerializeField] private Color menuBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
    [SerializeField] private Color normalButtonColor = new Color(0.95f, 0.95f, 0.95f);
    [SerializeField] private Color hoverButtonColor = new Color(0.8f, 0.8f, 1f);
    [SerializeField] private Color disabledButtonColor = new Color(0.6f, 0.6f, 0.6f);


    private InventoryDropdownMenu dropdownInstance;

    private void Start()
    {
        CreateDropdownMenu();
    }

    /// <summary>
    /// Create the dropdown menu
    /// </summary>
    private void CreateDropdownMenu()
    {
        GameObject dropdownObj;

        if (dropdownMenuPrefab != null)
        {
            dropdownObj = Instantiate(dropdownMenuPrefab, dropdownMenuParent);
        }
        else
        {
            dropdownObj = CreateDefaultDropdownMenu();
        }

        dropdownInstance = dropdownObj.GetComponent<InventoryDropdownMenu>();

        if (dropdownInstance == null)
        {
            dropdownInstance = dropdownObj.AddComponent<InventoryDropdownMenu>();
        }

        ConfigureDropdownMenu();
        Debug.Log("Simple dropdown menu created and configured");
    }

    /// <summary>
    /// Create a basic dropdown menu if no prefab is provided
    /// </summary>
    private GameObject CreateDefaultDropdownMenu()
    {
        // Main dropdown panel
        GameObject dropdownPanel = new GameObject("InventoryDropdownMenu");
        dropdownPanel.transform.SetParent(transform, false);

        var rectTransform = dropdownPanel.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(160, 140); // Better initial size
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0, 1);

        // Background
        var backgroundImage = dropdownPanel.AddComponent<Image>();
        backgroundImage.color = menuBackgroundColor;

        // Canvas group for fading
        var canvasGroup = dropdownPanel.AddComponent<CanvasGroup>();

        // Button container
        GameObject buttonContainer = new GameObject("ButtonContainer");
        buttonContainer.transform.SetParent(dropdownPanel.transform, false);

        var containerRect = buttonContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = new Vector2(10, 10);
        containerRect.offsetMax = new Vector2(-10, -10);

        var layoutGroup = buttonContainer.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 4f; // Increased spacing between buttons
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;

        // Add ContentSizeFitter to help with automatic sizing
        var contentSizeFitter = buttonContainer.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Set up the dropdown menu component
        var dropdownMenu = dropdownPanel.AddComponent<InventoryDropdownMenu>();

        // Set references via reflection (temporary)
        SetPrivateField(dropdownMenu, "dropdownPanel", dropdownPanel);
        SetPrivateField(dropdownMenu, "buttonContainer", buttonContainer.transform);
        SetPrivateField(dropdownMenu, "canvasGroup", canvasGroup);

        return dropdownPanel;
    }

    /// <summary>
    /// Configure the dropdown menu styling
    /// </summary>
    private void ConfigureDropdownMenu()
    {
        if (dropdownInstance == null) return;

        // Apply styling via reflection (temporary)
        SetPrivateField(dropdownInstance, "normalButtonColor", normalButtonColor);
        SetPrivateField(dropdownInstance, "hoverButtonColor", hoverButtonColor);
        SetPrivateField(dropdownInstance, "disabledButtonColor", disabledButtonColor);
    }

    /// <summary>
    /// Get the dropdown menu instance (creates if needed)
    /// </summary>
    public InventoryDropdownMenu DropdownMenu
    {
        get
        {
            if (dropdownInstance == null)
                CreateDropdownMenu();
            return dropdownInstance;
        }
    }

    /// <summary>
    /// Helper method to set private fields via reflection
    /// </summary>
    private void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(target, value);
    }

    /// <summary>
    /// Register dropdown with all existing drag handlers
    /// </summary>
    public void RegisterWithDragHandlers()
    {
        var dragHandlers = FindObjectsByType<InventoryItemDragHandler>(FindObjectsSortMode.None);

        foreach (var handler in dragHandlers)
        {
            SetPrivateField(handler, "dropdownMenu", DropdownMenu);
        }

        Debug.Log($"Registered dropdown with {dragHandlers.Length} drag handlers");
    }

    private void OnEnable()
    {
        // Register with drag handlers when enabled
        if (dropdownInstance != null)
        {
            RegisterWithDragHandlers();
        }
    }
}