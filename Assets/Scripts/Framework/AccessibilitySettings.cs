/**
 * @file AccessibilitySettings.cs
 * @brief Static utility for colorblind-accessible color palettes.
 *
 * @details
 * Provides per-mode happy/sad color pairs so UI elements remain
 * distinguishable for users with color vision deficiencies.
 *
 * Persistence:
 * - Saves/loads via PlayerPrefs key "Iris_ColorblindMode".
 * - Auto-loads on startup via [RuntimeInitializeOnLoadMethod].
 *
 * Pattern:
 * - Static utility like TimeScaleManager — no MonoBehaviour, no singleton.
 *
 * @ingroup framework
 */

using UnityEngine;

public static class AccessibilitySettings
{
    public enum ColorblindMode
    {
        Normal,
        Deuteranopia,
        Protanopia,
        Tritanopia
    }

    private const string PREFS_KEY = "Iris_ColorblindMode";

    private static ColorblindMode s_mode = ColorblindMode.Normal;

    public static ColorblindMode Mode => s_mode;

    // ── Color palettes per mode ──
    // Normal:      green / red
    // Deuteranopia: blue / orange
    // Protanopia:  blue / yellow-orange
    // Tritanopia:  magenta / cyan

    private static readonly Color[] s_happyColors =
    {
        new Color(0.3f, 1f, 0.4f, 1f),     // Normal: green
        new Color(0.2f, 0.5f, 1f, 1f),     // Deuteranopia: blue
        new Color(0.2f, 0.5f, 1f, 1f),     // Protanopia: blue
        new Color(1f, 0.3f, 0.85f, 1f)     // Tritanopia: magenta
    };

    private static readonly Color[] s_sadColors =
    {
        new Color(1f, 0.3f, 0.25f, 1f),    // Normal: red
        new Color(1f, 0.6f, 0.1f, 1f),     // Deuteranopia: orange
        new Color(1f, 0.7f, 0.2f, 1f),     // Protanopia: yellow-orange
        new Color(0f, 0.85f, 0.9f, 1f)     // Tritanopia: cyan
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        s_mode = ColorblindMode.Normal;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        int saved = PlayerPrefs.GetInt(PREFS_KEY, 0);
        if (saved >= 0 && saved < System.Enum.GetValues(typeof(ColorblindMode)).Length)
            s_mode = (ColorblindMode)saved;
        else
            s_mode = ColorblindMode.Normal;
    }

    public static void SetMode(ColorblindMode mode)
    {
        s_mode = mode;
        PlayerPrefs.SetInt(PREFS_KEY, (int)mode);
        PlayerPrefs.Save();
        Debug.Log($"[AccessibilitySettings] Colorblind mode set to {mode}");
    }

    public static Color GetHappyColor()
    {
        return s_happyColors[(int)s_mode];
    }

    public static Color GetSadColor()
    {
        return s_sadColors[(int)s_mode];
    }
}
