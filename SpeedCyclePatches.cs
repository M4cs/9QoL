using System;
using Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NineQoL;

/// <summary>
/// Makes the triple-speed button multi-level: clicking it again while 3x is
/// active cycles 3x -> 4x -> 6x -> back to 3x.
///
/// The game LERPS Time.timeScale toward its target rather than setting it, so
/// we never sample a "base" value from it. Instead the configured speeds are
/// absolute targets, and NineQoLBehaviour.LateUpdate calls Enforce()
/// after all game updates each frame. The boost is only written while the
/// current timescale is already in SuperFast territory (above
/// EnforceThreshold), which keeps pause (0), normal (1), fast (2), and any
/// slow-motion effects completely untouched.
/// </summary>
[HarmonyPatch]
internal static class SpeedCyclePatches
{
    // Absolute game speeds per cycle level; index 0 must be the game's own 3x.
    private static float[] _speeds = { 3f, 4f, 10f };
    private static string[] _labels = { "3x", "4x", "10x" };

    private const float EnforceThreshold = 2.25f;

    private static readonly Color[] LevelTints =
    {
        Color.white,
        new Color(1f, 0.72f, 0.25f), // amber for level 1
        new Color(1f, 0.38f, 0.30f), // red for level 2
    };

    private static int _level;
    private static bool _superFastActive;
    private static bool _wasSuperFastBeforeClick;
    private static GameplayUI_ChangeSpeedView _view;

    internal static void LoadConfiguredSpeeds()
    {
        try
        {
            var parts = NineQoLPlugin.SpeedCycleSpeeds.Value.Split(',');
            if (parts.Length < 2)
                return;
            var speeds = new float[parts.Length];
            var labels = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                speeds[i] = float.Parse(parts[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                labels[i] = speeds[i] % 1f == 0f ? $"{(int)speeds[i]}x" : $"{speeds[i]:0.#}x";
            }
            _speeds = speeds;
            _labels = labels;
        }
        catch (Exception ex)
        {
            NineQoLPlugin.Logger.LogWarning($"Could not parse SpeedCycleSpeeds, using 3,4,10: {ex.Message}");
        }
    }

    /// <summary>Called from NineQoLBehaviour.LateUpdate every frame.</summary>
    internal static void Enforce()
    {
        if (!_superFastActive || _level == 0)
            return;
        float ts = Time.timeScale;
        float target = _speeds[_level];
        if (ts > EnforceThreshold && Mathf.Abs(ts - target) > 0.001f)
            Time.timeScale = target;
    }

    [HarmonyPatch(typeof(GameplayUI_ChangeSpeedView), nameof(GameplayUI_ChangeSpeedView.SetSuperFastSpeed))]
    [HarmonyPrefix]
    private static void SuperFastClicked_Prefix()
    {
        _wasSuperFastBeforeClick = _superFastActive;
    }

    [HarmonyPatch(typeof(GameplayUI_ChangeSpeedView), nameof(GameplayUI_ChangeSpeedView.SetSuperFastSpeed))]
    [HarmonyPostfix]
    private static void SuperFastClicked_Postfix(GameplayUI_ChangeSpeedView __instance)
    {
        try
        {
            _view = __instance;
            _level = _wasSuperFastBeforeClick ? (_level + 1) % _speeds.Length : 0;
            _superFastActive = true;
            UpdateButtonVisual();
            NineQoLPlugin.Logger.LogInfo($"Speed cycle: {_labels[_level]} (target speed {_speeds[_level]:0.##})");
        }
        catch (Exception ex)
        {
            NineQoLPlugin.Logger.LogWarning($"Speed cycle click failed: {ex}");
        }
    }

    [HarmonyPatch(typeof(GameplayUI_ChangeSpeedView), nameof(GameplayUI_ChangeSpeedView.EnableFastingForward))]
    [HarmonyPostfix]
    private static void EnableFastingForward_Postfix(GameplayUI_ChangeSpeedView __instance, GameplayUI_ChangeSpeedView.Speed value)
    {
        try
        {
            _view = __instance;
            if (value == GameplayUI_ChangeSpeedView.Speed.SuperFast)
            {
                // Keep the current cycle level across wave restarts etc.
                _superFastActive = true;
                UpdateButtonVisual();
            }
            else
            {
                // Leaving SuperFast (normal/fast/paused): reset the cycle and
                // let the game's own timescale handling stand untouched.
                _superFastActive = false;
                _level = 0;
                UpdateButtonVisual();
            }
        }
        catch (Exception ex)
        {
            NineQoLPlugin.Logger.LogWarning($"Speed state tracking failed: {ex}");
        }
    }

    private static void UpdateButtonVisual()
    {
        try
        {
            if (_view == null)
                return;
            var control = _view.m_SuperFastControl;
            if (control == null)
                return;

            var tint = LevelTints[Math.Min(_level, LevelTints.Length - 1)];
            TintImages(control.m_Image, tint);
            TintImages(control.m_Selected, tint);
        }
        catch
        {
            // Purely cosmetic; never let it break the speed logic.
        }
    }

    private static void TintImages(GameObject go, Color tint)
    {
        if (go == null)
            return;
        var images = go.GetComponentsInChildren<Image>(true);
        if (images == null)
            return;
        foreach (var image in images)
        {
            if (image != null)
                image.color = tint;
        }
    }
}
