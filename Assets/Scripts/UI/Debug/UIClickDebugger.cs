using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Debug tool to help identify UI clicking issues
/// Shows what's blocking clicks and provides detailed raycast information
/// </summary>
public class UIClickDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugMode = true;
    [SerializeField] private bool showDebugInConsole = true;
    [SerializeField] private bool showDebugOnScreen = true;
    [SerializeField] private KeyCode debugKey = KeyCode.F12;

    [Header("Visual Settings")]
    [SerializeField] private Color raycastHitColor = Color.green;
    [SerializeField] private Color raycastMissColor = Color.red;
    [SerializeField] private float debugLineDuration = 2f;

    private Camera currentCamera;
    private List<RaycastResult> raycastResults = new List<RaycastResult>();
    private string lastDebugInfo = "";

    // On-screen GUI
    private bool showGUI = false;
    private Vector2 scrollPosition;

    private void Start()
    {
        currentCamera = Camera.main;
        if (currentCamera == null)
        {
            currentCamera = FindFirstObjectByType<Camera>();
        }
    }

    private void Update()
    {
        if (!enableDebugMode) return;

        // Toggle GUI with debug key
        if (Input.GetKeyDown(debugKey))
        {
            showGUI = !showGUI;
        }

        // Debug on mouse click
        if (Input.GetMouseButtonDown(0))
        {
            DebugMouseClick();
        }
    }

    private void DebugMouseClick()
    {
        Vector2 mousePosition = Input.mousePosition;

        // Clear previous results
        raycastResults.Clear();

        // Create pointer event data
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = mousePosition
        };

        // Perform raycast
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        // Build debug information
        string debugInfo = BuildDebugInfo(mousePosition, raycastResults);
        lastDebugInfo = debugInfo;

        if (showDebugInConsole)
        {
            Debug.Log(debugInfo);
        }

        // Visual debug lines
        DrawDebugLines(mousePosition, raycastResults);
    }

    private string BuildDebugInfo(Vector2 mousePosition, List<RaycastResult> results)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("=== UI CLICK DEBUG ===");
        sb.AppendLine($"Mouse Position: {mousePosition}");
        sb.AppendLine($"Screen Resolution: {Screen.width}x{Screen.height}");
        sb.AppendLine($"Time: {Time.time:F2}");
        sb.AppendLine();

        // Check EventSystem
        if (EventSystem.current == null)
        {
            sb.AppendLine("❌ ERROR: No EventSystem found!");
            return sb.ToString();
        }

        sb.AppendLine($"EventSystem: {EventSystem.current.name}");
        sb.AppendLine($"Current Selected: {(EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : "None")}");
        sb.AppendLine();

        // Check GraphicRaycaster components
        var raycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None);
        sb.AppendLine($"Found {raycasters.Length} GraphicRaycaster(s):");

        foreach (var raycaster in raycasters)
        {
            Canvas canvas = raycaster.GetComponent<Canvas>();
            sb.AppendLine($"  • {raycaster.name} - Enabled: {raycaster.enabled}");
            if (canvas != null)
            {
                sb.AppendLine($"    Canvas Sort Order: {canvas.sortingOrder}");
                sb.AppendLine($"    Canvas Render Mode: {canvas.renderMode}");
                sb.AppendLine($"    Canvas Enabled: {canvas.enabled}");

                if (canvas.worldCamera != null)
                {
                    sb.AppendLine($"    World Camera: {canvas.worldCamera.name}");
                }
            }
        }
        sb.AppendLine();

        // Raycast results
        if (results.Count == 0)
        {
            sb.AppendLine("❌ NO UI ELEMENTS HIT");
            sb.AppendLine("Possible causes:");
            sb.AppendLine("  • UI element has Raycast Target disabled");
            sb.AppendLine("  • UI element is behind another invisible element");
            sb.AppendLine("  • Canvas Group is blocking raycasts");
            sb.AppendLine("  • GraphicRaycaster is disabled");
            sb.AppendLine("  • Canvas is disabled or has wrong sorting order");
        }
        else
        {
            sb.AppendLine($"✅ HIT {results.Count} UI ELEMENT(S):");

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                sb.AppendLine($"  {i + 1}. {GetGameObjectPath(result.gameObject)}");
                sb.AppendLine($"     Distance: {result.distance:F2}");
                sb.AppendLine($"     Sort Order: {result.sortingOrder}");
                sb.AppendLine($"     Module: {result.module?.GetType().Name}");

                // Check if it's clickable
                var button = result.gameObject.GetComponent<Button>();
                var selectable = result.gameObject.GetComponent<Selectable>();
                var eventTrigger = result.gameObject.GetComponent<EventTrigger>();

                bool isClickable = button != null || selectable != null || eventTrigger != null;
                sb.AppendLine($"     Clickable: {(isClickable ? "✅" : "❌")}");

                if (button != null)
                {
                    sb.AppendLine($"     Button Interactable: {button.interactable}");
                    sb.AppendLine($"     Button Listeners: {button.onClick.GetPersistentEventCount()}");
                }

                // Check CanvasGroup blocking
                var canvasGroups = result.gameObject.GetComponentsInParent<CanvasGroup>();
                foreach (var cg in canvasGroups)
                {
                    if (!cg.interactable || cg.blocksRaycasts == false)
                    {
                        sb.AppendLine($"     ⚠️ CanvasGroup '{cg.name}' blocking: Interactable={cg.interactable}, BlocksRaycasts={cg.blocksRaycasts}");
                    }
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private string GetGameObjectPath(GameObject obj)
    {
        if (obj == null) return "null";

        string path = obj.name;
        Transform parent = obj.transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private void DrawDebugLines(Vector2 mousePosition, List<RaycastResult> results)
    {
        if (currentCamera == null) return;

        Vector3 worldMousePos = currentCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, 5f));

        if (results.Count == 0)
        {
            // Draw red line for no hits
            Debug.DrawRay(worldMousePos, Vector3.forward * 2f, raycastMissColor, debugLineDuration);
        }
        else
        {
            // Draw green line for hits
            Debug.DrawRay(worldMousePos, Vector3.forward * 2f, raycastHitColor, debugLineDuration);

            // Draw lines to each hit object
            foreach (var result in results)
            {
                if (result.gameObject != null)
                {
                    Vector3 targetPos = result.gameObject.transform.position;
                    Debug.DrawLine(worldMousePos, targetPos, raycastHitColor, debugLineDuration);
                }
            }
        }
    }

    private void OnGUI()
    {
        if (!showGUI || !showDebugOnScreen) return;

        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

        GUILayout.BeginVertical("box");
        GUILayout.Label($"UI Click Debugger (Press {debugKey} to toggle)", EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).label);
        GUILayout.Label("Click anywhere to debug that position");

        if (GUILayout.Button("Force Debug Current Mouse Position"))
        {
            DebugMouseClick();
        }

        GUILayout.Space(10);

        // Show debug info
        if (!string.IsNullOrEmpty(lastDebugInfo))
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
            GUILayout.TextArea(lastDebugInfo, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    // Public methods for manual debugging
    public void DebugPosition(Vector2 screenPosition)
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        string debugInfo = BuildDebugInfo(screenPosition, raycastResults);
        lastDebugInfo = debugInfo;
        Debug.Log(debugInfo);
    }

    public void ListAllCanvases()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Debug.Log($"=== ALL CANVASES ({canvases.Length}) ===");

        System.Array.Sort(canvases, (a, b) => b.sortingOrder.CompareTo(a.sortingOrder));

        foreach (var canvas in canvases)
        {
            Debug.Log($"Canvas: {canvas.name} | Sort Order: {canvas.sortingOrder} | Enabled: {canvas.enabled} | Render Mode: {canvas.renderMode}");
        }
    }
}