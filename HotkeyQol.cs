using System;
using Core;
using GameplayInterface;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NineQoL;

/// <summary>
/// Keyboard shortcuts for the placing phase, which the base game only offers
/// as mouse clicks:
///   - Start Battle      (default: Space) — presses the placing view's START button.
///   - King reroll       (default: F)     — presses the king-face reroll button.
///   - Event plot preview (default: V, hold) — shows the plots the upcoming
///     blessing/cataclysm will hit, exactly like hovering the calendar entry.
/// The click hotkeys only fire while the placing view is up, the button is
/// actually clickable, no card is being dragged, no menu/policy screen is open
/// and no text field has focus. Keys are configurable (Unity InputSystem
/// <see cref="Key"/> names, e.g. "Space", "Enter", "F", "F1"; "None" disables).
/// Note: the game already binds R (quick restart), C (compendium), I (pit),
/// K (perks), O (speed), P (menu), Q (auto attack), Tab and 1/2/3.
/// </summary>
internal static class HotkeyQol
{
    private static Key _startKey = Key.Space;
    private static Key _rerollKey = Key.F;
    private static Key _previewKey = Key.V;
    private static string _startKeyRaw;
    private static string _rerollKeyRaw;
    private static string _previewKeyRaw;
    private static bool _previewShown;
    private static float _errorMuteUntil;

    internal static void Tick()
    {
        try
        {
            if (!NineQoLPlugin.HotkeysEnabled.Value)
            {
                if (_previewShown)
                    SetPreview(false);
                return;
            }

            // No gameplay scene (main menu): nothing to do, and GameplayUI's
            // static accessors would throw every frame.
            if (GameplayUI.Instance == null)
            {
                _previewShown = false;
                return;
            }

            SyncKeys();
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            HandlePreview(keyboard);

            bool startPressed = _startKey != Key.None && keyboard[_startKey].wasPressedThisFrame;
            bool rerollPressed = _rerollKey != Key.None && keyboard[_rerollKey].wasPressedThisFrame;
            if (!startPressed && !rerollPressed)
                return;

            string blocked = WhyBlocked();
            if (blocked != null)
            {
                if (NineQoLPlugin.VerboseLogging.Value)
                    NineQoLPlugin.Logger.LogInfo($"[Hotkeys] ignored key press: {blocked}");
                return;
            }

            var placing = GameplayUI.PlacingView;
            if (placing == null || !placing.gameObject.activeInHierarchy)
                return;

            if (startPressed)
                TryClick(placing.m_StartBattleBtn, "start battle");

            if (rerollPressed && placing.IsKingRerollVisible)
                TryClick(placing.m_KingRerollBtn, "king reroll");
        }
        catch (Exception ex)
        {
            if (Time.unscaledTime >= _errorMuteUntil)
            {
                _errorMuteUntil = Time.unscaledTime + 30f;
                NineQoLPlugin.Logger.LogWarning($"Hotkey handling failed (muted for 30s): {ex}");
            }
        }
    }

    /// <summary>Hold-to-show: mirrors what hovering the calendar's event icon does.</summary>
    private static void HandlePreview(Keyboard keyboard)
    {
        bool held = _previewKey != Key.None && keyboard[_previewKey].isPressed;
        if (held && !_previewShown)
        {
            if (GameplayUI_PauseMenu.PauseMenuOnScreen || TextFieldFocused())
                return;
            var placing = GameplayUI.PlacingView;
            if (placing == null || !placing.gameObject.activeInHierarchy)
                return;
            SetPreview(true);
        }
        else if (!held && _previewShown)
        {
            SetPreview(false);
        }
    }

    private static void SetPreview(bool show)
    {
        _previewShown = show;
        try
        {
            var placing = GameplayUI.PlacingView;
            if (placing == null)
                return;
            if (show)
                placing.ShowCataclysmTerrains();
            else
                placing.HideCataclysmTerrains();
            if (NineQoLPlugin.VerboseLogging.Value)
                NineQoLPlugin.Logger.LogInfo($"[Hotkeys] event plot preview {(show ? "shown" : "hidden")}.");
        }
        catch (Exception ex)
        {
            if (Time.unscaledTime >= _errorMuteUntil)
            {
                _errorMuteUntil = Time.unscaledTime + 30f;
                NineQoLPlugin.Logger.LogWarning($"Event plot preview failed (muted for 30s): {ex}");
            }
        }
    }

    private static string WhyBlocked()
    {
        if (GameplayUI_PauseMenu.PauseMenuOnScreen)
            return "pause menu open";
        if (GameplayUI_PolicyView.IsVisible)
            return "policy view open";
        var hover = GameplayUI.HoverCardView;
        if (hover != null && hover.IsDragging)
            return "a card is being dragged";
        if (TextFieldFocused())
            return "a text field has focus";
        return null;
    }

    private static bool TextFieldFocused()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;
        var selected = eventSystem.currentSelectedGameObject;
        if (selected == null)
            return false;
        return selected.GetComponent<TMP_InputField>() != null
               || selected.GetComponent<InputField>() != null;
    }

    private static void TryClick(Button button, string what)
    {
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
        {
            if (NineQoLPlugin.VerboseLogging.Value)
                NineQoLPlugin.Logger.LogInfo($"[Hotkeys] {what}: button not clickable right now.");
            return;
        }
        button.onClick.Invoke();
        if (NineQoLPlugin.VerboseLogging.Value)
            NineQoLPlugin.Logger.LogInfo($"[Hotkeys] {what} triggered by keyboard.");
    }

    private static void SyncKeys()
    {
        var startRaw = NineQoLPlugin.StartBattleKey.Value;
        if (!ReferenceEquals(startRaw, _startKeyRaw))
        {
            _startKeyRaw = startRaw;
            _startKey = ParseKey(startRaw, Key.Space, "StartBattleKey");
        }
        var rerollRaw = NineQoLPlugin.KingRerollKey.Value;
        if (!ReferenceEquals(rerollRaw, _rerollKeyRaw))
        {
            _rerollKeyRaw = rerollRaw;
            _rerollKey = ParseKey(rerollRaw, Key.F, "KingRerollKey");
            if (_rerollKey == Key.R)
                NineQoLPlugin.Logger.LogWarning("KingRerollKey is R, which the game itself uses for Quick Restart. Consider another key (default: F).");
        }
        var previewRaw = NineQoLPlugin.CataclysmPreviewKey.Value;
        if (!ReferenceEquals(previewRaw, _previewKeyRaw))
        {
            _previewKeyRaw = previewRaw;
            _previewKey = ParseKey(previewRaw, Key.V, "CataclysmPreviewKey");
        }
    }

    internal static Key ParseKey(string raw, Key fallback, string settingName)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Key.None;
        var trimmed = raw.Trim();
        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("off", StringComparison.OrdinalIgnoreCase))
            return Key.None;
        if (Enum.TryParse(trimmed, true, out Key key))
            return key;
        NineQoLPlugin.Logger.LogWarning($"{settingName}: '{raw}' is not a known key name; using {fallback}.");
        return fallback;
    }

    internal static string KeyLabel(string raw, Key fallback)
    {
        var key = ParseKey(raw, fallback, "hotkey");
        return key == Key.None ? "OFF" : key.ToString().ToUpperInvariant();
    }
}
