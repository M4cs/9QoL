using System;
using System.Collections.Generic;
using System.Text;
using Cards;
using Core;
using Core.Itens;
using Core.Kingdoms;
using GameplayInterface;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace NineQoL;

/// <summary>
/// Card / plot quality-of-life:
///  1. Loot-screen cards show a per-level breakdown ("Lv. 1: 2   Lv. 2: 1") of plots
///     that already hold the same card, so you can spot pending level-ups.
///  2. While a card is being held, every plot that the card would UPGRADE
///     (same card already placed there, not yet max level) is marked. By
///     default the marking reuses the game's own plot marker (the one the
///     cataclysm / blessing preview switches on), so it looks exactly like a
///     blessing marking; alternatively the plot can be tinted blue or get
///     the effect outline (see the UpgradeGlowStyle setting).
/// </summary>
[HarmonyPatch]
internal static class CardQolPatches
{
    // ---------- Feature 1: level breakdown under loot/shop cards ----------
    // The game uses several standalone card widgets: DiplomatCard for the
    // post-battle loot offer, MerchantCard in the shop, CardPreview elsewhere.
    // All get the same treatment.

    private static readonly Dictionary<int, TMP_Text> BreakdownLabels = new();

    [HarmonyPatch(typeof(DiplomatCard), nameof(DiplomatCard.Setup))]
    [HarmonyPostfix]
    private static void DiplomatSetup_Postfix(DiplomatCard __instance, CardSo card)
        => HandleCardWidget(__instance, card, "DiplomatCard");

    [HarmonyPatch(typeof(MerchantCard), nameof(MerchantCard.Setup))]
    [HarmonyPostfix]
    private static void MerchantSetup_Postfix(MerchantCard __instance, CardSo card)
        => HandleCardWidget(__instance, card, "MerchantCard");

    [HarmonyPatch(typeof(CardPreview), nameof(CardPreview.SetCard))]
    [HarmonyPostfix]
    private static void SetCard_Postfix(CardPreview __instance, CardSo card)
        => HandleCardWidget(__instance, card, "CardPreview");

    // The post-battle "PICK YOUR LOOT" screen uses HandCard widgets.
    [HarmonyPatch(typeof(HandCard), nameof(HandCard.Setup))]
    [HarmonyPostfix]
    private static void HandCardSetup_Postfix(HandCard __instance, CardSo card)
        => HandleCardWidget(__instance, card, "HandCard.Setup");

    [HarmonyPatch(typeof(HandCard), nameof(HandCard.SetupAsVisualOnly))]
    [HarmonyPostfix]
    private static void HandCardVisual_Postfix(HandCard __instance, CardSo card)
        => HandleCardWidget(__instance, card, "HandCard.SetupAsVisualOnly");

    private static void HandleCardWidget(Component widget, CardSo card, string source)
    {
        try
        {
            if (!NineQoLPlugin.ShowCardLevelBreakdown.Value || widget == null)
                return;

            string text = BuildBreakdownText(card, source);

            int id = widget.GetInstanceID();
            BreakdownLabels.TryGetValue(id, out var label);
            if (label == null)
            {
                if (text == null)
                    return; // nothing to show and no label yet
                label = CreateBreakdownLabel(widget);
                if (label == null)
                {
                    if (NineQoLPlugin.VerboseLogging.Value)
                        NineQoLPlugin.Logger.LogInfo($"[CardQoL] {source}: could not create breakdown label.");
                    return;
                }
                BreakdownLabels[id] = label;
            }

            if (text == null)
            {
                label.gameObject.SetActive(false);
            }
            else
            {
                label.text = text;
                label.gameObject.SetActive(true);
            }
        }
        catch (Exception ex)
        {
            NineQoLPlugin.Logger.LogWarning($"Card level breakdown failed ({source}): {ex}");
        }
    }

    /// <summary>"Lv. 1: 2   Lv. 2: 1" per level for plots holding this card; null when
    /// the card is a spell or nothing matching is placed. Plots are matched by
    /// the card's ItemNameString — the placed CardSo is a separate runtime
    /// instance, so reference comparison would never match.</summary>
    private static string BuildBreakdownText(CardSo card, string source)
    {
        if (card == null || card.IsSpell)
            return null;

        string cardName;
        try { cardName = card.ItemNameString; } catch { return null; }
        if (string.IsNullOrEmpty(cardName))
            return null;

        var areas = KingdomAreaManager.areas;
        if (areas == null)
            return null;

        var counts = new SortedDictionary<int, int>();
        int total = 0;
        for (int i = 0; i < areas.Length; i++)
        {
            var area = areas[i];
            if (area == null)
                continue;
            var placed = area.Placed;
            if (placed == null || placed.ItemNameString != cardName)
                continue;
            int level = placed.CardLevel;
            counts.TryGetValue(level, out int n);
            counts[level] = n + 1;
            total++;
        }

        if (NineQoLPlugin.VerboseLogging.Value)
            NineQoLPlugin.Logger.LogInfo($"[CardQoL] {source} '{cardName}': {total} matching plots of {areas.Length} areas.");

        if (counts.Count == 0)
            return null;

        var sb = new StringBuilder();
        foreach (var pair in counts)
        {
            if (sb.Length > 0)
                sb.Append("   ");
            sb.Append("Lv. ").Append(pair.Key).Append(": ").Append(pair.Value);
        }
        return sb.ToString();
    }

    private static TMP_Text CreateBreakdownLabel(Component widget)
    {
        var parent = widget.transform;
        var template = widget.GetComponentInChildren<TMP_Text>(true);
        if (template == null || parent == null)
            return null;

        var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
        go.name = "ModCardLevelBreakdown";
        var label = go.GetComponent<TMP_Text>();
        if (label == null)
        {
            UnityEngine.Object.Destroy(go);
            return null;
        }

        // The clone may inherit components that hide it: a CanvasGroup at
        // alpha 0, or masking that clips anything outside the card art.
        var canvasGroup = go.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            UnityEngine.Object.Destroy(canvasGroup);
        label.maskable = false; // card widgets mask their content; opt out

        var rect = go.GetComponent<RectTransform>();
        var cardRect = parent.TryCast<RectTransform>();
        float cardHalfHeight = cardRect != null && cardRect.rect.height > 1f ? cardRect.rect.height * 0.5f : 120f;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -8f); // just below the card's bottom edge
        rect.sizeDelta = new Vector2(cardRect != null && cardRect.rect.width > 1f ? cardRect.rect.width : 200f, 40f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        go.transform.SetAsLastSibling();

        // Small and shrink-to-fit: the strip is exactly the card's width, and
        // auto-sizing scales the text down when several levels are listed.
        label.enableAutoSizing = true;
        label.fontSizeMax = Mathf.Max(12f, template.fontSize * 0.5f);
        label.fontSizeMin = 8f;
        label.fontSize = label.fontSizeMax;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.95f, 0.7f);
        label.enableWordWrapping = false;
        label.alpha = 1f;

        if (NineQoLPlugin.VerboseLogging.Value)
            NineQoLPlugin.Logger.LogInfo(
                $"[CardQoL] Label created under '{widget.name}': cardRect={(cardRect != null ? cardRect.rect.ToString() : "null")}, " +
                $"fontSize={label.fontSize}, worldPos={go.transform.position}");
        MaybeDumpWidget(widget);
        return label;
    }

    private static readonly HashSet<string> DumpedWidgets = new();

    private static void MaybeDumpWidget(Component widget)
    {
        if (!NineQoLPlugin.VerboseLogging.Value)
            return;
        var typeName = widget.GetIl2CppType().Name;
        if (!DumpedWidgets.Add(typeName))
            return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[CardQoL] Widget dump {typeName} '{widget.name}':");
        DumpTransform(widget.transform, 1, sb);
        NineQoLPlugin.Logger.LogInfo(sb.ToString());
    }

    private static void DumpTransform(Transform t, int depth, System.Text.StringBuilder sb)
    {
        if (depth > 5)
            return;
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i);
            sb.Append(new string(' ', depth * 2)).Append(child.name)
              .Append(" active=").Append(child.gameObject.activeSelf).Append(" [");
            var comps = child.GetComponents<Component>();
            for (int c = 0; c < comps.Length; c++)
            {
                if (comps[c] == null) continue;
                if (c > 0) sb.Append(',');
                sb.Append(comps[c].GetIl2CppType().Name);
            }
            sb.AppendLine("]");
            DumpTransform(child, depth + 1, sb);
        }
    }

    // ---------- Feature 2: mark every plot the held card would upgrade ----------
    //
    // How the game does plot visuals (from the IL2CPP disassembly):
    //  - KingdomArea.Dragging(bool) is invoked for every plot through the
    //    GameplayUI_HoverCardView.Dragging event when a card is picked up or
    //    released. It only changes plot OPACITY (dims plots the card can't go
    //    on); it never colours anything.
    //  - MouseOver()/MouseNotOver() tint the hovered plot through
    //    SetColor(_onHover / _onCannotHover), which writes a "_Tint" colour into
    //    the plot's MaterialPropertyBlock. SetColor is a no-op while
    //    m_OverrideColor has a value, and SetColorOverride(null) clears it.
    //  - Cataclysm.PreviewTerrains (the blessing/cataclysm plot markings)
    //    simply calls EnableAlertIcon(true/false), i.e. toggles m_AlertIcon.
    //  - EnableEffectOutline toggles m_EffectShowcaseOutline (card effect
    //    showcase); KingdomAreaManager.ClearEffectOutlines turns those off.

    internal enum GlowStyle { Marker, Tint, Outline }

    private sealed class GlowState
    {
        public GlowStyle Style;
        public bool WasActiveBefore; // marker/outline object already on when we arrived
    }

    private static readonly Dictionary<int, GlowState> Glowing = new();
    private static readonly Dictionary<int, KingdomArea> GlowingAreas = new();
    private static readonly Color FallbackBlue = new(0.25f, 0.55f, 1f, 0.65f);
    private static float _nextGlowRefresh;
    private static GlowStyle _activeStyle = GlowStyle.Marker;
    private static string _cachedColorKey;
    private static Color _cachedColor;
    private static bool _glowDebugDumped;
    private static float _glowErrorMuteUntil;

    /// <summary>Card picked up / released: immediate reaction per plot.</summary>
    [HarmonyPatch(typeof(KingdomArea), nameof(KingdomArea.Dragging))]
    [HarmonyPostfix]
    private static void Dragging_Postfix(KingdomArea __instance, bool dragging)
    {
        try
        {
            if (__instance == null)
                return;
            if (dragging && NineQoLPlugin.UpgradeGlow.Value)
                SetGlow(__instance, IsUpgradeTarget(__instance, SelectedCard()));
            else
                SetGlow(__instance, false);
        }
        catch (Exception ex)
        {
            LogGlowError("Upgrade glow (drag) failed", ex);
        }
    }

    // Tint style only: the game's hover tint is blocked while an override
    // colour is set, so give the hovered plot a brighter shade ourselves.
    [HarmonyPatch(typeof(KingdomArea), nameof(KingdomArea.MouseOver))]
    [HarmonyPostfix]
    private static void MouseOver_Postfix(KingdomArea __instance)
    {
        try
        {
            if (__instance != null && Glowing.TryGetValue(__instance.GetInstanceID(), out var state)
                && state.Style == GlowStyle.Tint)
                __instance.SetColorOverride(new Il2CppSystem.Nullable<Color>(GlowColor(true)));
        }
        catch (Exception ex)
        {
            LogGlowError("Upgrade glow (hover) failed", ex);
        }
    }

    [HarmonyPatch(typeof(KingdomArea), nameof(KingdomArea.MouseNotOver))]
    [HarmonyPostfix]
    private static void MouseNotOver_Postfix(KingdomArea __instance)
    {
        try
        {
            if (__instance != null && Glowing.TryGetValue(__instance.GetInstanceID(), out var state)
                && state.Style == GlowStyle.Tint)
                __instance.SetColorOverride(new Il2CppSystem.Nullable<Color>(GlowColor(false)));
        }
        catch (Exception ex)
        {
            LogGlowError("Upgrade glow (hover end) failed", ex);
        }
    }

    /// <summary>
    /// Called from NineQoLBehaviour.Update. Keeps the markings in sync while a
    /// card is held (the game's own cataclysm preview or effect showcase may
    /// switch the same objects off underneath us) and clears everything the
    /// moment no card is held or the feature gets disabled.
    /// </summary>
    internal static void TickGlow()
    {
        float now = Time.unscaledTime;
        if (now < _nextGlowRefresh)
            return;
        _nextGlowRefresh = now + 0.15f;

        try
        {
            var style = CurrentStyle();
            if (style != _activeStyle)
            {
                ClearAllGlow();
                _activeStyle = style;
            }

            bool dragging = false;
            // GameplayUI's static accessors throw while no gameplay scene is
            // loaded (main menu), so check the instance first.
            if (NineQoLPlugin.UpgradeGlow.Value && GameplayUI.Instance != null)
            {
                var hover = GameplayUI.HoverCardView;
                dragging = hover != null && hover.IsDragging;
            }

            if (!dragging)
            {
                if (Glowing.Count > 0)
                    ClearAllGlow();
                return;
            }

            var card = SelectedCard();
            var areas = KingdomAreaManager.areas;
            if (areas == null)
                return;
            for (int i = 0; i < areas.Length; i++)
            {
                var area = areas[i];
                if (area != null)
                    SetGlow(area, IsUpgradeTarget(area, card));
            }
        }
        catch (Exception ex)
        {
            LogGlowError("Upgrade glow refresh failed", ex);
        }
    }

    internal static void ClearAllGlow()
    {
        if (GlowingAreas.Count == 0)
            return;
        var areas = new List<KingdomArea>(GlowingAreas.Values);
        foreach (var area in areas)
        {
            try
            {
                if (area != null)
                    SetGlow(area, false);
            }
            catch
            {
                // Destroyed plot (scene change); dropped below.
            }
        }
        Glowing.Clear();
        GlowingAreas.Clear();
    }

    private static CardSo SelectedCard()
    {
        CardSo card = null;
        var hover = GameplayUI.HoverCardView;
        if (hover != null)
            card = hover.SelectedCard;
        if (card == null)
            card = Card.Selected;
        return card;
    }

    /// <summary>A plot is an upgrade target when it already holds something
    /// and the game itself says the held card can go there (for an occupied
    /// plot that means: same card, not max level).</summary>
    private static bool IsUpgradeTarget(KingdomArea area, CardSo selected)
    {
        if (selected == null || selected.IsSpell)
            return false;
        if (!area.Unlocked || !area.HasPlaced)
            return false;
        return area.CanPlace(selected);
    }

    private static void SetGlow(KingdomArea area, bool on)
    {
        int id = area.GetInstanceID();
        bool has = Glowing.TryGetValue(id, out var state);

        if (on)
        {
            if (has)
            {
                Reassert(area, state);
                return;
            }

            state = new GlowState { Style = CurrentStyle() };
            if (state.Style == GlowStyle.Marker && area.m_AlertIcon == null)
                state.Style = GlowStyle.Tint; // plot prefab without a marker: fall back

            switch (state.Style)
            {
                case GlowStyle.Marker:
                {
                    var icon = area.m_AlertIcon;
                    state.WasActiveBefore = icon.activeSelf;
                    icon.SetActive(true);
                    break;
                }
                case GlowStyle.Tint:
                {
                    var existing = area.m_OverrideColor;
                    if (existing != null && existing.HasValue)
                        return; // the game owns this plot's colour right now (construction mode etc.)
                    area.SetColorOverride(new Il2CppSystem.Nullable<Color>(GlowColor(false)));
                    break;
                }
                case GlowStyle.Outline:
                {
                    var outline = area.m_EffectShowcaseOutline;
                    state.WasActiveBefore = outline != null && outline.activeSelf;
                    area.EnableEffectOutline(true);
                    break;
                }
            }

            Glowing[id] = state;
            GlowingAreas[id] = area;
            MaybeDumpGlowDebug(area);
        }
        else if (has)
        {
            Glowing.Remove(id);
            GlowingAreas.Remove(id);
            switch (state.Style)
            {
                case GlowStyle.Marker:
                    if (!state.WasActiveBefore && area.m_AlertIcon != null)
                        area.m_AlertIcon.SetActive(false);
                    break;
                case GlowStyle.Tint:
                    area.SetColorOverride(new Il2CppSystem.Nullable<Color>());
                    break;
                case GlowStyle.Outline:
                {
                    var showcased = KingdomAreaManager.m_EffectOutlinedAreas;
                    bool gameUsesIt = showcased != null && showcased.Contains(area);
                    if (!state.WasActiveBefore && !gameUsesIt)
                        area.EnableEffectOutline(false);
                    break;
                }
            }
        }
    }

    /// <summary>The game switched our marking off (cataclysm preview ended,
    /// ClearEffectOutlines ran, ...): switch it back on. Since the game turned
    /// it off, it's ours to turn off again later as well.</summary>
    private static void Reassert(KingdomArea area, GlowState state)
    {
        switch (state.Style)
        {
            case GlowStyle.Marker:
            {
                var icon = area.m_AlertIcon;
                if (icon != null && !icon.activeSelf)
                {
                    icon.SetActive(true);
                    state.WasActiveBefore = false;
                }
                break;
            }
            case GlowStyle.Tint:
            {
                var existing = area.m_OverrideColor;
                if (existing == null || !existing.HasValue)
                    area.SetColorOverride(new Il2CppSystem.Nullable<Color>(GlowColor(false)));
                break;
            }
            case GlowStyle.Outline:
            {
                var outline = area.m_EffectShowcaseOutline;
                if (outline != null && !outline.activeSelf)
                {
                    area.EnableEffectOutline(true);
                    state.WasActiveBefore = false;
                }
                break;
            }
        }
    }

    internal static GlowStyle CurrentStyle()
    {
        var raw = NineQoLPlugin.UpgradeGlowStyle.Value;
        if (!string.IsNullOrEmpty(raw) && Enum.TryParse(raw.Trim(), true, out GlowStyle style))
            return style;
        return GlowStyle.Marker;
    }

    /// <summary>Tint colour. "auto" samples the game's own blessing colour
    /// (GameplayUI_PlacingView.BlessingPopupHeaderColor); anything else is a
    /// hex colour (#RRGGBB or #RRGGBBAA). Alpha is the tint strength.</summary>
    private static Color GlowColor(bool hovered)
    {
        var key = NineQoLPlugin.UpgradeGlowColor.Value ?? string.Empty;
        if (key != _cachedColorKey)
        {
            _cachedColor = ResolveColor(key);
            _cachedColorKey = key;
        }
        var c = _cachedColor;
        if (hovered)
        {
            c = Color.Lerp(c, Color.white, 0.35f);
            c.a = Mathf.Min(1f, _cachedColor.a + 0.2f);
        }
        return c;
    }

    private static Color ResolveColor(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.Length == 0 || trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var placing = GameplayUI.PlacingView;
                if (placing != null)
                {
                    var c = placing.BlessingPopupHeaderColor;
                    if (c.r + c.g + c.b > 0.05f)
                    {
                        c.a = FallbackBlue.a;
                        return c;
                    }
                }
            }
            catch
            {
                // fall through to the fixed blue
            }
            return FallbackBlue;
        }

        string hex = trimmed.StartsWith("#") ? trimmed : "#" + trimmed;
        if (ColorUtility.TryParseHtmlString(hex, out var parsed))
        {
            if (hex.Length <= 7)
                parsed.a = FallbackBlue.a; // no alpha given: default strength
            return parsed;
        }
        return FallbackBlue;
    }

    private static void LogGlowError(string what, Exception ex)
    {
        if (Time.unscaledTime < _glowErrorMuteUntil)
            return;
        _glowErrorMuteUntil = Time.unscaledTime + 30f;
        NineQoLPlugin.Logger.LogWarning($"{what} (muted for 30s): {ex}");
    }

    /// <summary>One-time dump (VerboseLogging) of the plot's marker/outline
    /// objects and colours, to make tuning the look easy without a debugger.</summary>
    private static void MaybeDumpGlowDebug(KingdomArea area)
    {
        if (_glowDebugDumped || !NineQoLPlugin.VerboseLogging.Value)
            return;
        _glowDebugDumped = true;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[UpgradeGlow] style={CurrentStyle()} colour={GlowColor(false)} on plot '{area.name}'");
            sb.AppendLine($"  _onHover={area._onHover} _onCannotHover={area._onCannotHover} _normalCol={area._normalCol}");
            try
            {
                var placing = GameplayUI.PlacingView;
                if (placing != null)
                    sb.AppendLine($"  BlessingPopupHeaderColor={placing.BlessingPopupHeaderColor}");
            }
            catch
            {
                // optional info only
            }
            DumpVisual("m_AlertIcon", area.m_AlertIcon, sb);
            DumpVisual("m_EffectShowcaseOutline", area.m_EffectShowcaseOutline, sb);
            DumpVisual("m_DestroyEffectOutline", area.m_DestroyEffectOutline, sb);
            DumpVisual("m_PlaceCardIndicator", area.m_PlaceCardIndicator, sb);
            NineQoLPlugin.Logger.LogInfo(sb.ToString());
        }
        catch (Exception ex)
        {
            NineQoLPlugin.Logger.LogInfo($"[UpgradeGlow] debug dump failed: {ex.Message}");
        }
    }

    private static void DumpVisual(string label, GameObject go, StringBuilder sb)
    {
        if (go == null)
        {
            sb.AppendLine($"  {label}: <null>");
            return;
        }
        sb.AppendLine($"  {label}: '{go.name}' active={go.activeSelf}");
        var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                string sprite = r.sprite != null ? r.sprite.name : "<none>";
                sb.AppendLine($"    SpriteRenderer '{r.name}' sprite={sprite} color={r.color} enabled={r.enabled} order={r.sortingOrder}");
            }
        }
        DumpTransform(go.transform, 2, sb);
    }
}
