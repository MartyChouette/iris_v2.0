// File: Assets/Editor/HierarchyStyler.cs
// Custom colors + headers in the Hierarchy window by name tags.
// [HDR]  → full-width header row
// [RED]  → red item text
// [BLUE] → blue item text

using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HierarchyStyler
{
    //// Colors you can tweak
    //private static readonly Color HeaderBg = new Color(0.13f, 0.13f, 0.13f, 1f);
    //private static readonly Color HeaderText = new Color(1f, 0.9f, 0.4f, 1f);
    //private static readonly Color RedText = Color.red;
    //private static readonly Color BlueText = new Color(0.4f, 0.7f, 1f, 1f);
    //private static readonly Color RowClearBg = new Color(0.18f, 0.18f, 0.18f, 1f); // matches default-ish

    //// Approx width of the little prefab/icon area in the Hierarchy
    //private const float IconWidth = 18f;

    //static HierarchyStyler()
    //{
    //    EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    //}

    //private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    //{
    //    var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
    //    if (go == null) return;

    //    string name = go.name;

    //    if (name.StartsWith("[HDR]"))
    //    {
    //        DrawHeaderRow(name, selectionRect);
    //    }
    //    else if (name.StartsWith("[RED]"))
    //    {
    //        DrawColoredRow(name, selectionRect, RedText);
    //    }
    //    else if (name.StartsWith("[BLUE]"))
    //    {
    //        DrawColoredRow(name, selectionRect, BlueText);
    //    }
    //    // else: leave default appearance
    //}

    //// ────────────────────────────────────────────────────────────────
    //// HEADER / DIVIDER ROW
    //// ────────────────────────────────────────────────────────────────

    //private static void DrawHeaderRow(string rawName, Rect rect)
    //{
    //    // Full-width background (covers default label so it doesn't show through)
    //    Rect bgRect = rect;
    //    bgRect.x = 0;
    //    bgRect.width = Screen.width;
    //    EditorGUI.DrawRect(bgRect, HeaderBg);

    //    string label = rawName.Replace("[HDR]", "").Trim();
    //    if (string.IsNullOrEmpty(label))
    //        label = "────────────────────────────";

    //    GUIStyle style = new GUIStyle(EditorStyles.label)
    //    {
    //        fontStyle = FontStyle.Bold
    //    };
    //    style.normal.textColor = HeaderText;

    //    EditorGUI.LabelField(rect, label, style);
    //}

    //// ────────────────────────────────────────────────────────────────
    //// COLORED ROWS (KEEP ICON, REPLACE TEXT)
    //// ────────────────────────────────────────────────────────────────

    //private static void DrawColoredRow(string rawName, Rect rect, Color textColor)
    //{
    //    string label = rawName
    //        .Replace("[RED]", "")
    //        .Replace("[BLUE]", "")
    //        .Trim();

    //    // Only clear & draw over the text area, not the icon
    //    Rect labelRect = rect;
    //    labelRect.x += IconWidth;
    //    labelRect.width -= IconWidth;

    //    // Clear the default grey text underneath
    //    EditorGUI.DrawRect(labelRect, RowClearBg);

    //    GUIStyle style = new GUIStyle(EditorStyles.label);
    //    style.normal.textColor = textColor;

    //    EditorGUI.LabelField(labelRect, label, style);
    //}
}
