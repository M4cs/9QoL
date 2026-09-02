using System;
using System.Collections.Generic;
using Core.Itens;
using Core.Kingdoms;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace NineQoL;

/// <summary>
/// Always-visible level label on every occupied plot ("Lv 2/4"). Each label
/// is a world-space TextMeshPro object parented to the plot, using the same
/// font (and sorting layer) as the plot's own damage text so it renders on
/// top of the plot like the game's numbers do. Labels refresh on level-up /
/// place / clear and on a slow periodic sweep as a safety net.
/// </summary>
[HarmonyPatch]
internal static class PlotLevelLabels
{
    private sealed class Label
    {
        public GameObject Go;
        public TextMeshPro Text;
        public string LastText;
        public bool MaxTint;
    }

    private static readonly Dictionary<int, Label> Labels = new();
    private static readonly Color NormalColor = Color.white;
    private static readonly Color MaxColor = new(1f, 0.84f, 0.3f);
    private static float _nextSweep;
    private static bool _dirty;
    private static bool _dumped;
    private static float _errorMuteUntil;

    [HarmonyPatch(typeof(KingdomArea), nameof(KingdomArea.LevelUpInternal))]
    [HarmonyPostfix]
    private static void LevelUp_Postfix() => _dirty = true;

    [HarmonyPatch(typeof(KingdomArea), nameof(KingdomArea.SetCard))]
    [HarmonyPostfix]
    private static void SetCard_Postfix() => _dirty = true;

    [HarmonyPatch(typeof(KingdomArea), nameof(KingdomArea.ClearInternal))]
    [HarmonyPostfix]
    private static void Clear_Postfix() => _dirty = true;

    [HarmonyPatch(typeof(KingdomArea), nameof(KingdomArea.LevelDown))]
    [HarmonyPostfix]
    private static void LevelDown_Postfix() => _dirty = true;

    /// <summary>Called from NineQoLBehaviour.Update.</summary>
    internal static void Tick()
    {
        float now = Time.unscaledTime;
        if (!_dirty && now < _nextSweep)
            return;
        _nextSweep = now + 0.5f;
        _dirty = false;

        try
        {
            if (!NineQoLPlugin.PlotLevelLabels.Value)
            {
                HideAll();
                return;
            }

            var areas = KingdomAreaManager.areas;
            if (areas == null)
            {
                HideAll();
                return;
            }

            for (int i = 0; i < areas.Length; i++)
            {
                var area = areas[i];
                if (area != null)
                    UpdateArea(area);
            }
        }
        catch (Exception ex)
        {
            if (now >= _errorMuteUntil)
            {
                _errorMuteUntil = now + 30f;
                NineQoLPlugin.Logger.LogWarning($"Plot level labels failed (muted for 30s): {ex}");
            }
        }
    }

    private static void HideAll()
    {
        if (Labels.Count == 0)
            return;
        foreach (var label in Labels.Values)
        {
            try
            {
                if (label.Go != null && label.Go.activeSelf)
                    label.Go.SetActive(false);
            }
            catch
            {
                // destroyed with its scene
            }
        }
    }

    private static void UpdateArea(KingdomArea area)
    {
        int id = area.GetInstanceID();
        Labels.TryGetValue(id, out var label);
        if (label != null && label.Go == null)
        {
            Labels.Remove(id);
            label = null;
        }

        CardSo placed = null;
        bool show = false;
        try
        {
            show = area.Unlocked && area.HasPlaced && area.gameObject.activeInHierarchy;
            if (show)
            {
                placed = area.Placed;
                show = placed != null;
            }
        }
        catch
        {
            show = false;
        }

        if (!show)
        {
            if (label != null && label.Go.activeSelf)
                label.Go.SetActive(false);
            return;
        }

        int level = SafeLevel(placed, out int max, out bool limitless);
        string text = FormatLabel(level, max, limitless);
        bool atMax = !limitless && max > 0 && max < 99 && level >= max;

        if (label == null)
        {
            label = Create(area);
            if (label == null)
                return;
            Labels[id] = label;
        }

        if (!string.Equals(text, label.LastText, StringComparison.Ordinal))
        {
            label.Text.text = text;
            label.LastText = text;
        }
        if (atMax != label.MaxTint)
        {
            label.Text.color = atMax ? MaxColor : NormalColor;
            label.MaxTint = atMax;
        }

        Place(area, label);
        if (!label.Go.activeSelf)
            label.Go.SetActive(true);
    }

    private static int SafeLevel(CardSo placed, out int max, out bool limitless)
    {
        int level = 0;
        max = 0;
        limitless = false;
        try { level = placed.TooltipLevel; } catch { }
        if (level <= 0)
        {
            try { level = placed.CardLevel; } catch { }
        }
        try { max = placed.MaxLevel; } catch { }
        try { limitless = placed.LimitlessLevels; } catch { }
        return level;
    }

    private static string FormatLabel(int level, int max, bool limitless)
    {
        var format = NineQoLPlugin.PlotLevelLabelFormat.Value;
        if (string.IsNullOrWhiteSpace(format))
            format = "Lv {level}/{max}";
        // The game uses 99 as "effectively unlimited" for some cards.
        bool hasMax = !limitless && max > 0 && max < 99;
        if (!hasMax)
        {
            // Drop the "/{max}" part (and any bare "{max}") when there is no cap.
            format = format.Replace("/{max}", string.Empty).Replace("{max}", string.Empty);
        }
        return format.Replace("{level}", level.ToString())
                     .Replace("{max}", max.ToString())
                     .Trim();
    }

    private static Label Create(KingdomArea area)
    {
        TMP_Text template = null;
        try { template = area.m_DamageTxt; } catch { }
        if (template == null)
        {
            try { template = area.m_CoinText; } catch { }
        }

        var go = new GameObject("ModPlotLevelLabel");
        go.transform.SetParent(area.transform, false);
        var text = go.AddComponent<TextMeshPro>();
        if (text == null)
        {
            UnityEngine.Object.Destroy(go);
            return null;
        }

        try
        {
            if (template != null && template.font != null)
                text.font = template.font;
        }
        catch
        {
            // default TMP font then
        }

        Vector2 size = PlotSize(area);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.05f;
        text.fontSizeMax = 200f;
        text.fontStyle = FontStyles.Bold;
        text.color = NormalColor;
        try
        {
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(0, 0, 0, 255);
        }
        catch
        {
            // shader without outline support; plain text is fine
        }

        // Render above the plot art, on the same layer the game's own plot
        // texts use when we can find it.
        ApplySorting(area, template, text);

        // Compensate any parent scale so the rect is in world units.
        var lossy = area.transform.lossyScale;
        go.transform.localScale = new Vector3(
            lossy.x != 0f ? 1f / lossy.x : 1f,
            lossy.y != 0f ? 1f / lossy.y : 1f,
            1f);
        go.transform.rotation = Quaternion.identity;

        float scale = Mathf.Clamp(NineQoLPlugin.PlotLevelLabelScale.Value, 0.2f, 4f);
        text.rectTransform.sizeDelta = new Vector2(size.x * 0.9f, size.y * 0.26f * scale);

        var label = new Label { Go = go, Text = text };
        MaybeDump(area, template, text, size);
        return label;
    }

    private static void ApplySorting(KingdomArea area, TMP_Text template, TextMeshPro text)
    {
        try
        {
            Canvas canvas = template != null ? template.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                text.sortingLayerID = canvas.sortingLayerID;
                text.sortingOrder = canvas.sortingOrder + 5;
                return;
            }
            var renderer = template != null ? template.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                text.sortingLayerID = renderer.sortingLayerID;
                text.sortingOrder = renderer.sortingOrder + 5;
                return;
            }
            var plotRenderer = area._renderer;
            if (plotRenderer != null)
                text.sortingLayerID = plotRenderer.sortingLayerID;
            text.sortingOrder = 500;
        }
        catch
        {
            text.sortingOrder = 500;
        }
    }

    /// <summary>World-space footprint of the plot sprite. KingdomArea.Center /
    /// Size are grid-space values (every plot reported a 4x4 at its grid
    /// coordinate), so the sprite renderer's bounds are used instead.</summary>
    private static Bounds PlotBounds(KingdomArea area)
    {
        try
        {
            var renderer = area._renderer;
            if (renderer != null)
            {
                var b = renderer.bounds;
                if (b.size.x > 0.05f && b.size.y > 0.05f)
                    return b;
            }
        }
        catch
        {
            // fall through
        }
        return new Bounds(area.transform.position, new Vector3(2f, 1f, 0f));
    }

    private static Vector2 PlotSize(KingdomArea area)
    {
        var size = PlotBounds(area).size;
        return new Vector2(size.x, size.y);
    }

    private static void Place(KingdomArea area, Label label)
    {
        var bounds = PlotBounds(area);
        Vector3 pos = bounds.center;
        float offset = Mathf.Clamp(NineQoLPlugin.PlotLevelLabelOffset.Value, -0.8f, 0.8f);
        pos.y += bounds.size.y * offset;
        pos.z = area.transform.position.z;
        label.Go.transform.position = pos;
    }

    private static void MaybeDump(KingdomArea area, TMP_Text template, TextMeshPro text, Vector2 size)
    {
        if (_dumped || !NineQoLPlugin.VerboseLogging.Value)
            return;
        _dumped = true;
        try
        {
            var placed = area.Placed;
            string templateInfo = template == null
                ? "<none>"
                : $"'{template.name}' type={template.GetIl2CppType().Name} font={(template.font != null ? template.font.name : "null")} " +
                  $"canvas={(template.GetComponentInParent<Canvas>() != null ? "yes" : "no")}";
            NineQoLPlugin.Logger.LogInfo(
                $"[PlotLevel] first label on '{area.name}': center={area.Center} size={size} lossyScale={area.transform.lossyScale} " +
                $"template={templateInfo} sortingLayer={text.sortingLayerID} order={text.sortingOrder} " +
                $"placed={(placed != null ? placed.ItemNameString : "null")} CardLevel={(placed != null ? placed.CardLevel : -1)} " +
                $"TooltipLevel={(placed != null ? placed.TooltipLevel : -1)} MaxLevel={(placed != null ? placed.MaxLevel : -1)} " +
                $"Limitless={(placed != null && placed.LimitlessLevels)}");
        }
        catch (Exception ex)
        {
            NineQoLPlugin.Logger.LogInfo($"[PlotLevel] debug dump failed: {ex.Message}");
        }
    }
}
