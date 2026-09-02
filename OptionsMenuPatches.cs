using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UserInterface;

namespace NineQoL;

/// <summary>
/// Adds a "9 Qualities of Life" category to the game's options menu with:
///   - "MAX VISIBLE TROOPS": slider driving the mod's troop visibility caps.
///   - "FPS LIMIT": clone of the game's (shipped-but-disabled) framerate row,
///     proxied into the original slider so the game's own apply/persist
///     pipeline does the work.
///   - "CARD LEVEL COUNTS" and "UPGRADE HOVER GLOW" toggles for the card QoL.
/// The category is a clone of the Gameplay category shell, so it inherits the
/// game's styling, and is built for every OptionsUI instance (main menu and
/// pause menu).
/// </summary>
[HarmonyPatch]
internal static class OptionsMenuPatches
{
    private sealed class ModRows
    {
        public GameObject Category;
        public TMP_Text CategoryTitle;
        public GameObject TroopRow;
        public Slider TroopSlider;
        public TMP_Text TroopTitle;
        public GameObject FpsRow;
        public Slider FpsSlider;
        public TMP_Text FpsTitle;
        public Toggle BreakdownToggle;
        public TMP_Text BreakdownTitle;
        public Toggle GlowToggle;
        public TMP_Text GlowTitle;
        public Toggle HotkeysToggle;
        public TMP_Text HotkeysTitle;
        public Toggle LevelLabelsToggle;
        public TMP_Text LevelLabelsTitle;
        public Toggle FasterToggle;
        public TMP_Text FasterTitle;
    }

    private const string CategoryName = "9 Qualities of Life";
    private const int TroopSliderMin = 25;
    private const int TroopSliderMax = 1000;
    private const int FpsSliderMin = 30;
    private const int FpsSliderMax = 241; // top position = OFF (no cap)

    private static readonly Dictionary<int, ModRows> RowsPerMenu = new();

    [HarmonyPatch(typeof(OptionsUI), nameof(OptionsUI.Start))]
    [HarmonyPostfix]
    private static void Start_Postfix(OptionsUI __instance) => BuildOrSync(__instance);

    [HarmonyPatch(typeof(OptionsUI), nameof(OptionsUI.DisableNonGameplayComponents))]
    [HarmonyPostfix]
    private static void DisableNonGameplayComponents_Postfix(OptionsUI __instance) => BuildOrSync(__instance);

    [HarmonyPatch(typeof(OptionsUI), nameof(OptionsUI.LoadSettings))]
    [HarmonyPostfix]
    private static void LoadSettings_Postfix(OptionsUI __instance) => BuildOrSync(__instance);

    [HarmonyPatch(typeof(OptionsUI), nameof(OptionsUI.OnEnable))]
    [HarmonyPostfix]
    private static void OnEnable_Postfix(OptionsUI __instance) => BuildOrSync(__instance);

    private static void BuildOrSync(OptionsUI ui)
    {
        try
        {
            if (ui == null)
                return;
            int id = ui.GetInstanceID();
            if (!RowsPerMenu.TryGetValue(id, out var rows))
            {
                if (NineQoLPlugin.DebugDumpOptionsMenu.Value)
                {
                    DumpHierarchy(ui.m_AudioCategory, "AudioCategory");
                    DumpHierarchy(ui.m_GraphicsCategory, "GraphicsCategory");
                    DumpHierarchy(ui.m_GameplayCategory, "GameplayCategory");
                }
                rows = Build(ui);
                if (rows == null)
                    return;
                RowsPerMenu[id] = rows;
                NineQoLPlugin.Logger.LogInfo("Added '9 Qualities of Life' category to options menu.");
            }
            Sync(rows);
        }
        catch (Exception ex)
        {
            NineQoLPlugin.Logger.LogWarning($"Options menu integration failed: {ex}");
        }
    }

    private static ModRows Build(OptionsUI ui)
    {
        var gameplayCategory = ui.m_GameplayCategory;
        var musicSlider = ui.MusicSlider;
        var plotDamageToggle = ui.PlotDamageToggle;
        if (gameplayCategory == null || musicSlider == null || plotDamageToggle == null)
            return null;

        var rows = new ModRows();

        // ---- Our own category: clone the Gameplay category shell. ----
        rows.Category = UnityEngine.Object.Instantiate(gameplayCategory.gameObject, gameplayCategory.transform.parent);
        rows.Category.name = "ModCategory_NineQoL";
        rows.Category.transform.SetSiblingIndex(gameplayCategory.transform.GetSiblingIndex() + 1);

        var categoryRect = rows.Category.GetComponent<RectTransform>();
        var itemsContainer = FindItemsContainer(categoryRect);
        if (itemsContainer == null || itemsContainer.GetInstanceID() == categoryRect.transform.GetInstanceID())
        {
            NineQoLPlugin.Logger.LogWarning("Could not find items container in cloned category; aborting.");
            UnityEngine.Object.Destroy(rows.Category);
            return null;
        }

        // Drop the cloned Gameplay rows; our own rows replace them.
        for (int i = itemsContainer.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(itemsContainer.GetChild(i).gameObject);

        rows.CategoryTitle = FindTmpNamed(rows.Category, "Category Name Text");
        if (rows.CategoryTitle != null)
            rows.CategoryTitle.text = CategoryName;

        // ---- Troop limit row: clone the music volume row (plain slider). ----
        var musicRow = FindSingleControlRow(musicSlider.transform, ui.m_AudioCategory);
        rows.TroopRow = UnityEngine.Object.Instantiate(musicRow.gameObject, itemsContainer);
        rows.TroopRow.name = "ModRow_TroopLimit";
        rows.TroopRow.SetActive(true);

        var clonedSliders = rows.TroopRow.GetComponentsInChildren<Slider>(true);
        rows.TroopSlider = clonedSliders != null && clonedSliders.Length > 0 ? clonedSliders[0] : null;
        rows.TroopTitle = FirstTitleText(rows.TroopRow, null);
        if (rows.TroopSlider == null || clonedSliders.Length != 1)
        {
            NineQoLPlugin.Logger.LogWarning(
                $"Troop row clone contained {clonedSliders?.Length ?? 0} sliders (expected 1); not adding it.");
            UnityEngine.Object.Destroy(rows.TroopRow);
            rows.TroopRow = null;
        }
        else
        {
            SilenceExternalPersistentListeners(rows.TroopSlider.onValueChanged, rows.TroopRow.transform);
            rows.TroopSlider.onValueChanged.RemoveAllListeners();
            rows.TroopSlider.wholeNumbers = true;
            rows.TroopSlider.minValue = TroopSliderMin;
            rows.TroopSlider.maxValue = TroopSliderMax;

            var troopTitle = rows.TroopTitle;
            rows.TroopSlider.onValueChanged.AddListener((UnityAction<float>)((float v) =>
            {
                int n = (int)v;
                NineQoLPlugin.MaxVisibleAllies.Value = n;
                NineQoLPlugin.MaxVisibleEnemies.Value = n;
                if (troopTitle != null)
                    troopTitle.text = $"MAX VISIBLE TROOPS: {n}";
            }));
        }

        // ---- FPS limit row: another plain music-row clone, value shown in
        // the title just like the troop slider. Applies via
        // Application.targetFrameRate and persists in the mod config —
        // independent of the game's (shipped-but-disabled) framerate setting.
        rows.FpsRow = UnityEngine.Object.Instantiate(musicRow.gameObject, itemsContainer);
        rows.FpsRow.name = "ModRow_FpsLimit";
        rows.FpsRow.SetActive(true);

        rows.FpsSlider = rows.FpsRow.GetComponentInChildren<Slider>(true);
        rows.FpsTitle = FirstTitleText(rows.FpsRow, null);
        if (rows.FpsSlider != null)
        {
            SilenceExternalPersistentListeners(rows.FpsSlider.onValueChanged, rows.FpsRow.transform);
            rows.FpsSlider.onValueChanged.RemoveAllListeners();
            rows.FpsSlider.wholeNumbers = true;
            rows.FpsSlider.minValue = FpsSliderMin;
            rows.FpsSlider.maxValue = FpsSliderMax;

            var fpsTitle = rows.FpsTitle;
            rows.FpsSlider.onValueChanged.AddListener((UnityAction<float>)((float v) =>
            {
                int fps = v >= FpsSliderMax ? 0 : (int)v;
                NineQoLPlugin.FpsLimit.Value = fps;
                Application.targetFrameRate = fps > 0 ? fps : -1;
                if (fpsTitle != null)
                    fpsTitle.text = FpsTitleText(fps);
            }));
        }
        else
        {
            UnityEngine.Object.Destroy(rows.FpsRow);
            rows.FpsRow = null;
        }

        // ---- Feature toggles: clone the Plot Damage toggle row twice. ----
        var toggleRow = FindSingleControlRow(plotDamageToggle.transform, ui.m_GameplayCategory);
        rows.BreakdownToggle = BuildToggleRow(toggleRow, itemsContainer, "ModRow_CardLevelCounts",
            out rows.BreakdownTitle, (bool v) => NineQoLPlugin.ShowCardLevelBreakdown.Value = v);
        rows.GlowToggle = BuildToggleRow(toggleRow, itemsContainer, "ModRow_UpgradeGlow",
            out rows.GlowTitle, (bool v) => NineQoLPlugin.UpgradeGlow.Value = v);
        rows.HotkeysToggle = BuildToggleRow(toggleRow, itemsContainer, "ModRow_Hotkeys",
            out rows.HotkeysTitle, (bool v) => NineQoLPlugin.HotkeysEnabled.Value = v);
        rows.LevelLabelsToggle = BuildToggleRow(toggleRow, itemsContainer, "ModRow_PlotLevelLabels",
            out rows.LevelLabelsTitle, (bool v) => NineQoLPlugin.PlotLevelLabels.Value = v);
        rows.FasterToggle = BuildToggleRow(toggleRow, itemsContainer, "ModRow_FasterAnimations",
            out rows.FasterTitle, (bool v) => NineQoLPlugin.FasterAnimations.Value = v);

        return rows;
    }

    private static Toggle BuildToggleRow(Transform template, Transform parent, string name,
        out TMP_Text title, Action<bool> onChanged)
    {
        title = null;
        var row = UnityEngine.Object.Instantiate(template.gameObject, parent);
        row.name = name;
        row.SetActive(true);

        var toggle = row.GetComponentInChildren<Toggle>(true);
        if (toggle == null)
        {
            UnityEngine.Object.Destroy(row);
            return null;
        }

        title = FirstTitleText(row, null);
        SilenceExternalPersistentListeners(toggle.onValueChanged, row.transform);
        toggle.onValueChanged.AddListener((UnityAction<bool>)((bool v) => onChanged(v)));
        return toggle;
    }

    private static void Sync(ModRows rows)
    {
        if (rows.CategoryTitle != null)
            rows.CategoryTitle.text = CategoryName;

        if (rows.TroopSlider != null)
        {
            rows.TroopSlider.wholeNumbers = true;
            rows.TroopSlider.minValue = TroopSliderMin;
            rows.TroopSlider.maxValue = TroopSliderMax;

            int current = Mathf.Clamp(
                Math.Max(NineQoLPlugin.MaxVisibleAllies.Value, NineQoLPlugin.MaxVisibleEnemies.Value),
                TroopSliderMin, TroopSliderMax);
            rows.TroopSlider.SetValueWithoutNotify(current);
            if (rows.TroopTitle != null)
                rows.TroopTitle.text = $"MAX VISIBLE TROOPS: {current}";
        }

        if (rows.FpsRow != null && rows.FpsSlider != null)
        {
            rows.FpsSlider.wholeNumbers = true;
            rows.FpsSlider.minValue = FpsSliderMin;
            rows.FpsSlider.maxValue = FpsSliderMax;

            int fps = NineQoLPlugin.FpsLimit.Value;
            rows.FpsSlider.SetValueWithoutNotify(fps <= 0 ? FpsSliderMax : Mathf.Clamp(fps, FpsSliderMin, FpsSliderMax - 1));
            if (rows.FpsTitle != null)
                rows.FpsTitle.text = FpsTitleText(fps);
        }

        if (rows.BreakdownToggle != null)
        {
            // Set through isOn so the toggle's sprite/animator visuals update;
            // our listener writing the same value back is harmless.
            rows.BreakdownToggle.isOn = NineQoLPlugin.ShowCardLevelBreakdown.Value;
            if (rows.BreakdownTitle != null)
                rows.BreakdownTitle.text = "CARD LEVEL COUNTS";
        }

        if (rows.GlowToggle != null)
        {
            rows.GlowToggle.isOn = NineQoLPlugin.UpgradeGlow.Value;
            if (rows.GlowTitle != null)
                rows.GlowTitle.text = "UPGRADE PLOT GLOW";
        }

        if (rows.HotkeysToggle != null)
        {
            rows.HotkeysToggle.isOn = NineQoLPlugin.HotkeysEnabled.Value;
            if (rows.HotkeysTitle != null)
            {
                string start = HotkeyQol.KeyLabel(NineQoLPlugin.StartBattleKey.Value, UnityEngine.InputSystem.Key.Space);
                string reroll = HotkeyQol.KeyLabel(NineQoLPlugin.KingRerollKey.Value, UnityEngine.InputSystem.Key.F);
                string preview = HotkeyQol.KeyLabel(NineQoLPlugin.CataclysmPreviewKey.Value, UnityEngine.InputSystem.Key.V);
                rows.HotkeysTitle.text = $"HOTKEYS: BATTLE {start} / REROLL {reroll} / EVENT PLOTS {preview}";
            }
        }

        if (rows.LevelLabelsToggle != null)
        {
            rows.LevelLabelsToggle.isOn = NineQoLPlugin.PlotLevelLabels.Value;
            if (rows.LevelLabelsTitle != null)
                rows.LevelLabelsTitle.text = "PLOT LEVEL LABELS";
        }

        if (rows.FasterToggle != null)
        {
            rows.FasterToggle.isOn = NineQoLPlugin.FasterAnimations.Value;
            if (rows.FasterTitle != null)
                rows.FasterTitle.text = "FASTER ANIMATIONS";
        }
    }

    private static string FpsTitleText(int fps)
        => fps > 0 ? $"FPS LIMIT: {fps}" : "FPS LIMIT: OFF";

    private static TMP_Text FindTmpNamed(GameObject root, string name)
    {
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        if (texts == null)
            return null;
        foreach (var text in texts)
        {
            if (text != null && text.name == name)
                return text;
        }
        return null;
    }

    /// <summary>
    /// Walks up from a control to the largest ancestor that still contains
    /// exactly one interactive control — that subtree is the single settings
    /// row (label + control).
    /// </summary>
    private static Transform FindSingleControlRow(Transform controlTransform, RectTransform category)
    {
        var t = controlTransform;
        while (t.parent != null)
        {
            var parent = t.parent;
            if (category != null && parent.GetInstanceID() == category.transform.GetInstanceID())
                break;
            if (parent.GetComponent<Canvas>() != null)
                break;
            if (parent.GetComponentsInChildren<Selectable>(true).Length > 1)
                break;
            t = parent;
        }
        return t;
    }

    /// <summary>The category child that carries the row LayoutGroup ("Category Items").</summary>
    private static Transform FindItemsContainer(RectTransform category)
    {
        for (int i = 0; i < category.childCount; i++)
        {
            var child = category.GetChild(i);
            if (child.GetComponent<LayoutGroup>() != null)
                return child;
        }
        return category.transform;
    }

    /// <summary>First TMP text in the row that is not the value label.</summary>
    private static TMP_Text FirstTitleText(GameObject row, TMP_Text excluded)
    {
        var texts = row.GetComponentsInChildren<TMP_Text>(true);
        if (texts == null)
            return null;
        foreach (var text in texts)
        {
            if (text == null)
                continue;
            if (excluded != null && text.GetInstanceID() == excluded.GetInstanceID())
                continue;
            return text;
        }
        return null;
    }

    /// <summary>
    /// Disables persistent listeners whose target lives outside the row (e.g.
    /// OptionsUI.ApplyMusicVolume on a cloned row), while keeping listeners
    /// that target components inside the row (e.g. the framerate slider's own
    /// value-text updater).
    /// </summary>
    private static void SilenceExternalPersistentListeners(UnityEventBase unityEvent, Transform rowRoot)
    {
        if (unityEvent == null)
            return;
        int count = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            bool isInternal = false;
            try
            {
                var target = unityEvent.GetPersistentTarget(i);
                var component = target != null ? target.TryCast<Component>() : null;
                isInternal = component != null && component.transform.IsChildOf(rowRoot);
            }
            catch
            {
                // Treat unreadable targets as external.
            }
            if (!isInternal)
                unityEvent.SetPersistentListenerState(i, UnityEventCallState.Off);
        }
    }

    private static void DumpHierarchy(RectTransform root, string label)
    {
        var sb = new StringBuilder();
        sb.Append("[UIDump] ").Append(label);
        if (root == null)
        {
            NineQoLPlugin.Logger.LogInfo(sb.Append(": <null>").ToString());
            return;
        }
        sb.AppendLine($" activeSelf={root.gameObject.activeSelf}");
        DumpRecurse(root.transform, 1, sb);
        NineQoLPlugin.Logger.LogInfo(sb.ToString());
    }

    private static void DumpRecurse(Transform t, int depth, StringBuilder sb)
    {
        if (depth > 6)
            return;
        for (int i = 0; i < t.childCount; i++)
        {
            var child = t.GetChild(i);
            sb.Append(new string(' ', depth * 2));
            sb.Append(child.name).Append(" active=").Append(child.gameObject.activeSelf).Append(" [");
            var comps = child.GetComponents<Component>();
            for (int c = 0; c < comps.Length; c++)
            {
                if (comps[c] == null) continue;
                if (c > 0) sb.Append(',');
                sb.Append(comps[c].GetIl2CppType().Name);
            }
            sb.AppendLine("]");
            DumpRecurse(child, depth + 1, sb);
        }
    }
}
