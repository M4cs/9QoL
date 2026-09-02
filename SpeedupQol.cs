using System;
using System.Collections.Generic;
using Cards;
using Core;
using Core.Levels;
using GameplayInterface;
using HarmonyLib;
using UnityEngine;

namespace NineQoL;

/// <summary>
/// Makes the non-combat parts of a run move faster. Three independent levers:
///
///  1. Idle time-scale boost: between battles (loot screen, shop, policy
///     picks, placing phase) Time.timeScale is held at IdleTimeScale. Almost
///     every UI fade / card reveal / plot popup in the game is driven by the
///     game's own Progress helper on *scaled* time, so this speeds all of them
///     at once without touching combat. It never fires while the pause menu
///     is up, while the game itself paused (timeScale 0), or during a battle.
///  2. UI animator speed: Unity Animators under the gameplay UI (card hover
///     pops, panels, buttons) get Animator.speed = UiAnimatorSpeed.
///  3. Event pacing: the wave options' DelayBetweenEvents / TimeToNextWave /
///     area popup delays are multiplied by EventDelayScale. The countdown
///     before enemies arrive (TimeBeforeWave) is deliberately left alone.
/// </summary>
[HarmonyPatch]
internal static class SpeedupQol
{
    private const float BattleSpeedThreshold = 0.01f;

    private static float _nextTick;
    private static float _appliedAnimatorSpeed = 1f;
    private static float _errorMuteUntil;

    // Wave option originals (the ScriptableObject is shared for the session).
    private static WaveOptionsSo _optionsSeen;
    private static float _origDelayBetweenEvents, _origTimeToNextWave, _origAreaDefaultPopupDelay, _origAreaFastPopupDelay;
    private static float _appliedEventScale = -1f;

    internal static bool Enabled => NineQoLPlugin.FasterAnimations.Value;

    // ---------------- 1. idle time-scale boost (from LateUpdate) ----------------

    internal static void EnforceIdleTimeScale()
    {
        try
        {
            if (!Enabled)
                return;
            float factor = Mathf.Clamp(NineQoLPlugin.IdleTimeScale.Value, 1f, 5f);
            if (factor <= 1.001f)
                return;
            if (GameplayUI.Instance == null)
                return; // main menu etc.
            if (GameplayUI_PauseMenu.PauseMenuOnScreen)
                return;

            float ts = Time.timeScale;
            if (ts <= BattleSpeedThreshold)
                return; // the game paused time on purpose

            bool idle = InIdlePhase(out string reason);
            if (idle != _boostActive)
            {
                _boostActive = idle;
                if (NineQoLPlugin.VerboseLogging.Value)
                    NineQoLPlugin.Logger.LogInfo($"[Speedup] idle boost {(idle ? "ON" : "OFF")} ({reason}); timeScale={ts:0.##} enemiesAlive={SafeEnemiesAlive()}");
            }
            if (!idle)
                return;

            if (Mathf.Abs(ts - factor) > 0.001f)
                Time.timeScale = factor;
        }
        catch (Exception ex)
        {
            Mute("Idle time-scale boost failed", ex);
        }
    }

    private static bool _boostActive;

    /// <summary>
    /// "Idle" = a known between-battle screen is up and no enemy is on the
    /// field. The speed controls stay visible during the placing phase, so
    /// they are useless as a battle signal; the placing view / loot / shop /
    /// policy / prophet screens are positive evidence instead.
    /// </summary>
    private static bool InIdlePhase(out string reason)
    {
        if (SafeEnemiesAlive() > 0)
        {
            reason = "enemies alive";
            return false;
        }

        try
        {
            var placing = GameplayUI.PlacingView;
            if (placing != null && placing.gameObject.activeInHierarchy)
            {
                reason = "placing view";
                return true;
            }
        }
        catch
        {
            // fall through
        }

        try
        {
            if (GameplayUI_DiplomatView.OnScreen) { reason = "loot screen"; return true; }
            if (GameplayUI_ShopView.OnScreen) { reason = "shop"; return true; }
            if (GameplayUI_ProphetView.OnScreen) { reason = "prophet"; return true; }
            if (GameplayUI_PolicyView.IsVisible) { reason = "policy view"; return true; }
        }
        catch
        {
            // fall through
        }

        reason = "no idle screen";
        return false;
    }

    private static int SafeEnemiesAlive()
    {
        try { return Wave.EnemiesAlive; } catch { return 0; }
    }

    private static bool SafeOnGameplay()
    {
        try { return Wave.OnGameplay; } catch { return false; }
    }

    // ---------------- 2. + 3. periodic work (from Update) ----------------

    internal static void Tick()
    {
        float now = Time.unscaledTime;
        if (now < _nextTick)
            return;
        _nextTick = now + 2f; // hierarchy scan; keep it rare

        try
        {
            ApplyAnimatorSpeed();
            ApplyEventPacing();
        }
        catch (Exception ex)
        {
            Mute("Animation speed-up failed", ex);
        }
    }

    private static float TargetAnimatorSpeed()
        => Enabled ? Mathf.Clamp(NineQoLPlugin.UiAnimatorSpeed.Value, 0.5f, 4f) : 1f;

    private static void ApplyAnimatorSpeed()
    {
        float target = TargetAnimatorSpeed();
        var ui = GameplayUI.Instance;
        if (ui == null)
        {
            _appliedAnimatorSpeed = target;
            return;
        }

        var animators = ui.GetComponentsInChildren<Animator>(true);
        if (animators != null)
        {
            foreach (var animator in animators)
            {
                if (animator == null)
                    continue;
                float speed = animator.speed;
                // Only touch animators running at the default speed or at the
                // speed we set earlier, so game-driven values are respected.
                if (Mathf.Abs(speed - 1f) < 0.001f || Mathf.Abs(speed - _appliedAnimatorSpeed) < 0.001f)
                {
                    if (Mathf.Abs(speed - target) > 0.001f)
                        animator.speed = target;
                }
            }
        }
        _appliedAnimatorSpeed = target;
    }

    private static void ApplyEventPacing()
    {
        WaveOptionsSo options;
        try { options = Wave.Options; } catch { return; }
        if (options == null)
            return;

        if (_optionsSeen == null || _optionsSeen.Pointer != options.Pointer)
        {
            _optionsSeen = options;
            _origDelayBetweenEvents = options.DelayBetweenEvents;
            _origTimeToNextWave = options.TimeToNextWave;
            _origAreaDefaultPopupDelay = options.AreaDefaultPopupDelay;
            _origAreaFastPopupDelay = options.AreaFastPopupDelay;
            _appliedEventScale = -1f;
            if (NineQoLPlugin.VerboseLogging.Value)
                NineQoLPlugin.Logger.LogInfo(
                    $"[Speedup] wave options: DelayBetweenEvents={_origDelayBetweenEvents} TimeToNextWave={_origTimeToNextWave} " +
                    $"AreaDefaultPopupDelay={_origAreaDefaultPopupDelay} AreaFastPopupDelay={_origAreaFastPopupDelay} TimeBeforeWave={options.TimeBeforeWave}");
        }

        float scale = Enabled ? Mathf.Clamp(NineQoLPlugin.EventDelayScale.Value, 0.1f, 1f) : 1f;
        if (Mathf.Abs(scale - _appliedEventScale) < 0.0001f)
            return;
        _appliedEventScale = scale;
        options.DelayBetweenEvents = _origDelayBetweenEvents * scale;
        options.TimeToNextWave = _origTimeToNextWave * scale;
        options.AreaDefaultPopupDelay = _origAreaDefaultPopupDelay * scale;
        options.AreaFastPopupDelay = _origAreaFastPopupDelay * scale;
    }

    // Card widgets are spawned constantly (hand, loot, shop); set their
    // animator speed the moment they are set up instead of waiting for a sweep.
    [HarmonyPatch(typeof(HandCard), nameof(HandCard.Setup))]
    [HarmonyPostfix]
    private static void HandCardSetup_Postfix(HandCard __instance) => SpeedUpWidget(__instance != null ? __instance.Animator : null);

    [HarmonyPatch(typeof(HandCard), nameof(HandCard.SetupAsVisualOnly))]
    [HarmonyPostfix]
    private static void HandCardVisual_Postfix(HandCard __instance) => SpeedUpWidget(__instance != null ? __instance.Animator : null);

    [HarmonyPatch(typeof(DiplomatCard), nameof(DiplomatCard.Setup))]
    [HarmonyPostfix]
    private static void DiplomatSetup_Postfix(DiplomatCard __instance) => SpeedUpWidget(__instance != null ? __instance.Animator : null);

    private static void SpeedUpWidget(Animator animator)
    {
        try
        {
            if (animator == null)
                return;
            float target = TargetAnimatorSpeed();
            if (Mathf.Abs(animator.speed - target) > 0.001f)
                animator.speed = target;
        }
        catch
        {
            // cosmetic only
        }
    }

    private static void Mute(string what, Exception ex)
    {
        if (Time.unscaledTime < _errorMuteUntil)
            return;
        _errorMuteUntil = Time.unscaledTime + 30f;
        NineQoLPlugin.Logger.LogWarning($"{what} (muted for 30s): {ex}");
    }
}
