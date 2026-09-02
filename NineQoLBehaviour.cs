using System;
using System.Collections.Generic;
using Core.Entities;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace NineQoL;

/// <summary>
/// Injected MonoBehaviour that periodically walks TroopSystem's troop list and
/// force-hides the renderers of troops beyond the configured visibility caps.
/// Uses Renderer.forceRenderingOff, which is purely a rendering flag — the
/// game's combat/movement logic never reads it, so hidden troops keep fighting
/// at their real numbers.
/// </summary>
public class NineQoLBehaviour : MonoBehaviour
{
    public NineQoLBehaviour(IntPtr ptr) : base(ptr) { }

    private TroopSystem _troopSystem;
    private float _nextRefresh;
    private float _nextSystemSearch;
    private bool _restorePending;
    private bool _wasEnabled = true;

    // Troops whose Animator WE disabled (by instance id), so we only ever
    // re-enable animators the game itself had running.
    private readonly HashSet<int> _animatorsDisabledByUs = new();

    // Reusable error throttle so a repeated exception doesn't flood the log.
    private float _errorMuteUntil;

    private void LateUpdate()
    {
        // Runs after every game Update, so this write always wins the frame
        // against the game's own timescale lerp.
        try { SpeedCyclePatches.Enforce(); } catch { }
        try { SpeedupQol.EnforceIdleTimeScale(); } catch { }
    }

    private float _nextFpsCheck;

    private void Update()
    {
        try
        {
            float now = Time.unscaledTime;

            // Card/plot and hotkey QoL run regardless of the troop-cap master
            // switch; each is throttled/guarded internally.
            CardQolPatches.TickGlow();
            HotkeyQol.Tick();
            PlotLevelLabels.Tick();
            SpeedupQol.Tick();

            // FPS limit: independent of the troop-cap master switch. When set,
            // re-assert it so the game's own framerate handling can't undo it.
            if (now >= _nextFpsCheck)
            {
                _nextFpsCheck = now + 1f;
                int fps = NineQoLPlugin.FpsLimit.Value;
                if (fps > 0 && Application.targetFrameRate != fps)
                    Application.targetFrameRate = fps;
            }

            bool enabledNow = NineQoLPlugin.ModEnabled.Value;
            if (!enabledNow)
            {
                if (_wasEnabled)
                {
                    _restorePending = true;
                    _wasEnabled = false;
                }
                if (_restorePending)
                {
                    _restorePending = !TryRestoreAll();
                }
                return;
            }
            _wasEnabled = true;

            if (now < _nextRefresh)
                return;
            _nextRefresh = now + NineQoLPlugin.RefreshInterval.Value;

            if (!EnsureTroopSystem(now))
                return;

            ApplyCaps();
        }
        catch (Exception ex)
        {
            if (Time.unscaledTime >= _errorMuteUntil)
            {
                _errorMuteUntil = Time.unscaledTime + 30f;
                NineQoLPlugin.Logger.LogWarning($"Troop visibility pass failed (muted for 30s): {ex}");
            }
        }
    }

    private bool EnsureTroopSystem(float now)
    {
        if (_troopSystem != null)
            return true;

        // Scene scans are not free; only look for the system every 2 seconds.
        if (now < _nextSystemSearch)
            return false;
        _nextSystemSearch = now + 2f;

        var found = UnityEngine.Object.FindFirstObjectByType(Il2CppType.Of<TroopSystem>());
        _troopSystem = found != null ? found.TryCast<TroopSystem>() : null;
        if (_troopSystem != null)
            NineQoLPlugin.Logger.LogInfo("TroopSystem found — troop visibility capping active.");
        return _troopSystem != null;
    }

    private void ApplyCaps()
    {
        var troops = _troopSystem._validTroops;
        if (troops == null)
            return;

        int allyCap = NineQoLPlugin.MaxVisibleAllies.Value;
        int enemyCap = NineQoLPlugin.MaxVisibleEnemies.Value;
        bool spareBosses = NineQoLPlugin.AlwaysShowBosses.Value;

        int visibleAllies = 0;
        int visibleEnemies = 0;

        int count = troops.Count;
        for (int i = 0; i < count; i++)
        {
            var troop = troops[i];
            if (troop == null)
                continue;

            bool show;
            if (spareBosses && troop.TryCast<Boss>() != null)
            {
                show = true;
            }
            else if (troop.IsEnemy)
            {
                show = visibleEnemies < enemyCap;
                if (show) visibleEnemies++;
            }
            else
            {
                show = visibleAllies < allyCap;
                if (show) visibleAllies++;
            }

            SetTroopVisible(troop, show);
        }
    }

    private void SetTroopVisible(Troop troop, bool visible)
    {
        var renderers = troop.m_Renderers;
        if (renderers != null)
        {
            int rc = renderers.Count;
            for (int r = 0; r < rc; r++)
            {
                var renderer = renderers[r];
                if (renderer != null)
                    renderer.forceRenderingOff = !visible;
            }
        }

        if (!NineQoLPlugin.DisableHiddenAnimators.Value)
            return;

        var animator = troop.animator;
        if (animator == null)
            return;

        int id = troop.GetInstanceID();
        if (!visible)
        {
            if (animator.enabled)
            {
                animator.enabled = false;
                _animatorsDisabledByUs.Add(id);
            }
        }
        else if (_animatorsDisabledByUs.Remove(id))
        {
            animator.enabled = true;
        }
    }

    /// <summary>Unhide everything we may have touched. Returns true on success.</summary>
    private bool TryRestoreAll()
    {
        try
        {
            if (_troopSystem == null)
            {
                var found = UnityEngine.Object.FindFirstObjectByType(Il2CppType.Of<TroopSystem>());
                _troopSystem = found != null ? found.TryCast<TroopSystem>() : null;
                if (_troopSystem == null)
                    return true; // nothing to restore
            }

            var troops = _troopSystem._allTroops ?? _troopSystem._validTroops;
            if (troops != null)
            {
                int count = troops.Count;
                for (int i = 0; i < count; i++)
                {
                    var troop = troops[i];
                    if (troop != null)
                        SetTroopVisibleForRestore(troop);
                }
            }
            _animatorsDisabledByUs.Clear();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SetTroopVisibleForRestore(Troop troop)
    {
        var renderers = troop.m_Renderers;
        if (renderers != null)
        {
            int rc = renderers.Count;
            for (int r = 0; r < rc; r++)
            {
                var renderer = renderers[r];
                if (renderer != null)
                    renderer.forceRenderingOff = false;
            }
        }

        var animator = troop.animator;
        if (animator != null && _animatorsDisabledByUs.Contains(troop.GetInstanceID()))
            animator.enabled = true;
    }
}
