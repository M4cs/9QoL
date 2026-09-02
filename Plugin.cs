using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace NineQoL;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class NineQoLPlugin : BasePlugin
{
    public const string PluginGuid = "dev.oglabs.9kings.qol";
    public const string PluginName = "9 Qualities of Life";
    public const string PluginVersion = "1.4.0";

    internal static ManualLogSource Logger;

    internal static ConfigEntry<bool> ModEnabled;
    internal static ConfigEntry<int> MaxVisibleAllies;
    internal static ConfigEntry<int> MaxVisibleEnemies;
    internal static ConfigEntry<float> RefreshInterval;
    internal static ConfigEntry<bool> DisableHiddenAnimators;
    internal static ConfigEntry<bool> AlwaysShowBosses;
    internal static ConfigEntry<string> SpeedCycleSpeeds;
    internal static ConfigEntry<bool> DebugDumpOptionsMenu;
    internal static ConfigEntry<bool> VerboseLogging;
    internal static ConfigEntry<bool> ShowCardLevelBreakdown;
    internal static ConfigEntry<bool> UpgradeGlow;
    internal static ConfigEntry<string> UpgradeGlowStyle;
    internal static ConfigEntry<string> UpgradeGlowColor;
    internal static ConfigEntry<bool> HotkeysEnabled;
    internal static ConfigEntry<string> StartBattleKey;
    internal static ConfigEntry<string> KingRerollKey;
    internal static ConfigEntry<string> CataclysmPreviewKey;
    internal static ConfigEntry<bool> PlotLevelLabels;
    internal static ConfigEntry<string> PlotLevelLabelFormat;
    internal static ConfigEntry<float> PlotLevelLabelScale;
    internal static ConfigEntry<float> PlotLevelLabelOffset;
    internal static ConfigEntry<bool> FasterAnimations;
    internal static ConfigEntry<float> IdleTimeScale;
    internal static ConfigEntry<float> UiAnimatorSpeed;
    internal static ConfigEntry<float> EventDelayScale;
    internal static ConfigEntry<int> FpsLimit;

    public override void Load()
    {
        Logger = Log;
        // Guarantee that ConfigEntry.Value writes hit the disk immediately —
        // without this, in-game settings changes are lost on restart.
        Config.SaveOnConfigSet = true;

        ModEnabled = Config.Bind("General", "Enabled", true,
            "Master switch. When turned off, all troops are made visible again on the next refresh.");
        MaxVisibleAllies = Config.Bind("General", "MaxVisibleAllies", 150,
            new ConfigDescription(
                "Maximum number of allied troops that are rendered at once. Troops beyond this cap still exist and fight normally; they are just not drawn.",
                new AcceptableValueRange<int>(1, 5000)));
        MaxVisibleEnemies = Config.Bind("General", "MaxVisibleEnemies", 150,
            new ConfigDescription(
                "Maximum number of enemy troops that are rendered at once. Troops beyond this cap still exist and fight normally; they are just not drawn.",
                new AcceptableValueRange<int>(1, 5000)));
        RefreshInterval = Config.Bind("General", "RefreshInterval", 0.5f,
            new ConfigDescription(
                "Seconds between visibility passes. Lower values react faster to spawns/deaths but do slightly more work.",
                new AcceptableValueRange<float>(0.1f, 5f)));
        DisableHiddenAnimators = Config.Bind("Performance", "DisableHiddenAnimators", true,
            "Also disable the Unity Animator component on hidden troops. This is where most of the CPU savings come from. " +
            "Animators are re-enabled the moment a troop becomes visible again. Turn off if you notice any oddity with troop behaviour.");
        AlwaysShowBosses = Config.Bind("General", "AlwaysShowBosses", true,
            "Bosses are always rendered and never count toward the visibility caps.");

        SpeedCycleSpeeds = Config.Bind("Speed", "SpeedCycleSpeeds", "3,4,10",
            "Comma-separated game speeds the triple-speed button cycles through on repeated clicks. " +
            "The first entry must be the game's own SuperFast speed (3).");

        FpsLimit = Config.Bind("Performance", "FpsLimit", 0,
            new ConfigDescription(
                "Frame rate cap applied via Application.targetFrameRate. 0 = off (game default). " +
                "Note: with VSync enabled, the display refresh rate wins.",
                new AcceptableValueRange<int>(0, 240)));

        ShowCardLevelBreakdown = Config.Bind("Cards", "ShowCardLevelBreakdown", true,
            "Show a per-level count (e.g. 'Lv. 1: 2   Lv. 2: 1') under loot/shop cards for plots that already hold the same card, " +
            "so you can spot buildings/troops that could use a level up.");
        UpgradeGlow = Config.Bind("Cards", "UpgradeGlow", true,
            "While holding a card, every plot that the card would upgrade (same card placed there, not max level) is marked.");
        UpgradeGlowStyle = Config.Bind("Cards", "UpgradeGlowStyle", "Marker",
            "How upgradeable plots are marked. Marker = the game's own plot marker (the one blessing/cataclysm previews use). " +
            "Tint = tint the whole plot with UpgradeGlowColor. Outline = the card effect outline.");
        UpgradeGlowColor = Config.Bind("Cards", "UpgradeGlowColor", "#4C8CFF",
            "Tint colour for UpgradeGlowStyle = Tint, as #RRGGBB or #RRGGBBAA (alpha = strength). " +
            "'auto' samples the game's blessing popup colour instead.");

        HotkeysEnabled = Config.Bind("Hotkeys", "Enabled", true,
            "Keyboard shortcuts during the placing phase: start the battle and reroll the king offer.");
        StartBattleKey = Config.Bind("Hotkeys", "StartBattleKey", "Space",
            "Key that presses the START BATTLE button (Unity InputSystem key name, e.g. Space, Enter, F1). 'None' disables.");
        KingRerollKey = Config.Bind("Hotkeys", "KingRerollKey", "F",
            "Key that presses the king reroll button when it is available. 'None' disables. " +
            "(R is taken by the game's own Quick Restart.)");
        CataclysmPreviewKey = Config.Bind("Hotkeys", "CataclysmPreviewKey", "V",
            "Hold this key during the placing phase to mark the plots the upcoming blessing/cataclysm will hit, " +
            "the same way hovering the calendar entry does. 'None' disables.");

        PlotLevelLabels = Config.Bind("Plots", "PlotLevelLabels", true,
            "Show an always-visible level label on every occupied plot.");
        PlotLevelLabelFormat = Config.Bind("Plots", "PlotLevelLabelFormat", "Lv {level}/{max}",
            "Label text. {level} = current level, {max} = max level (dropped automatically for cards without a cap).");
        PlotLevelLabelScale = Config.Bind("Plots", "PlotLevelLabelScale", 1f,
            new ConfigDescription("Label size multiplier.", new AcceptableValueRange<float>(0.2f, 4f)));
        PlotLevelLabelOffset = Config.Bind("Plots", "PlotLevelLabelVerticalOffset", -0.3f,
            new ConfigDescription("Vertical position of the label relative to the plot sprite's centre, in plot sprite heights " +
                                  "(0 = centre, -0.5 = bottom corner, 0.5 = top corner).",
                new AcceptableValueRange<float>(-0.8f, 0.8f)));

        FasterAnimations = Config.Bind("Speed", "FasterAnimations", true,
            "Master switch for the animation speed-ups below (idle time-scale boost, UI animator speed, event pacing).");
        IdleTimeScale = Config.Bind("Speed", "IdleTimeScale", 2f,
            new ConfigDescription("Game speed held while no battle is running (loot screen, shop, placing phase). " +
                                  "Speeds up card reveals, fades, popups and level-up chains. 1 = off.",
                new AcceptableValueRange<float>(1f, 5f)));
        UiAnimatorSpeed = Config.Bind("Speed", "UiAnimatorSpeed", 1.5f,
            new ConfigDescription("Playback speed of the gameplay UI's Unity animators (card hover pops, panels, buttons). 1 = game default.",
                new AcceptableValueRange<float>(0.5f, 4f)));
        EventDelayScale = Config.Bind("Speed", "EventDelayScale", 0.5f,
            new ConfigDescription("Multiplier for the wave options' DelayBetweenEvents, TimeToNextWave and plot popup delays. " +
                                  "0.5 = twice as fast. The countdown before enemies arrive is not changed.",
                new AcceptableValueRange<float>(0.1f, 1f)));

        VerboseLogging = Config.Bind("Debug", "VerboseLogging", false,
            "Log details about card level breakdowns and other QoL features. Only needed for troubleshooting.");
        DebugDumpOptionsMenu = Config.Bind("Debug", "DebugDumpOptionsMenu", false,
            "Logs the options menu UI hierarchy to LogOutput.log when the menu is built. Only needed for troubleshooting.");

        ClassInjector.RegisterTypeInIl2Cpp<NineQoLBehaviour>();
        AddComponent<NineQoLBehaviour>();

        SpeedCyclePatches.LoadConfiguredSpeeds();
        new Harmony(PluginGuid).PatchAll(typeof(NineQoLPlugin).Assembly);

        Log.LogInfo($"{PluginName} {PluginVersion} loaded. Caps: {MaxVisibleAllies.Value} allies / {MaxVisibleEnemies.Value} enemies visible.");
    }
}
