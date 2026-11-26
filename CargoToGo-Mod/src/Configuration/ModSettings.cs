using BepInEx.Configuration;
using KydlCore.Common;
using CargoToGo.Core;

namespace CargoToGo.Configuration;

/// <summary>The player config (PROJECT_RULES 11): [General] Enable Mod switches the whole mod, every functionality has its own
/// [Section] Enable, [DebugLog] Enable adds detail lines to the log. All entries also show on the mod's Settings tab in the KY hub.</summary>
internal static class ModSettings
{
    public static ConfigEntry<bool> EnableMod;
    public static ConfigEntry<float> WeightMultiplier;

    // [Containers]: where the materials of a cargo come from
    public static ConfigEntry<bool> PullFromContainers;
    public static ConfigEntry<float> ContainerRange;
    public static ConfigEntry<bool> UseAzuCraftyBoxes;
    public static ConfigEntry<bool> ContainersFirst;

    // one switch per functionality
    public static ConfigEntry<bool> PackInWorld;
    public static ConfigEntry<bool> Repack;
    public static ConfigEntry<bool> AltDestroyAnyPiece;
    public static ConfigEntry<bool> PackEffects;
    public static ConfigEntry<bool> ShowKeyHints;
    public static ConfigEntry<bool> ShowCargoTab;

    public static ConfigEntry<bool> DebugLog;

    /// <summary>Is the mod switched on ([General] Enable Mod)? Off = inert: no packing, no repacking, no hints, no tab.
    /// Cargos you already hold stay safe (they can still be unpacked, and still cannot be lost into a chest or the ground).</summary>
    public static bool On { get { return EnableMod == null || EnableMod.Value; } }

    public static bool Verbose { get { return DebugLog != null && DebugLog.Value; } }

    /// The value the player had in an entry that moved (still in the file as an orphan before DropRemoved deletes it): the first of the
    /// given (section, key, section, key, ...) pairs that holds a bool; true when there is none.
    private static bool Old(ConfigFile c, params string[] sectionAndKey)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"ModSettings.Old(c={c})");
        // OrphanedEntries is not a public member of every BepInEx 5 build (ConfigTools.DropRemoved reaches it the same way)
        System.Reflection.PropertyInfo orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var dict = orphans != null ? orphans.GetValue(c, null) as System.Collections.IDictionary : null;
        for (int i = 0; dict != null && i + 1 < sectionAndKey.Length; i += 2)
        {
            var def = new ConfigDefinition(sectionAndKey[i], sectionAndKey[i + 1]);
            bool b;
            if (dict.Contains(def) && bool.TryParse((dict[def] as string ?? "").Trim(), out b))
            {
                { var returnedAt50 = b; if (RuleLog.IsOn()) ModLog.Verbose($"ModSettings.Old: returns {returnedAt50} (line 50)"); return returnedAt50; }
            }
        }

        { if (RuleLog.IsOn()) ModLog.Verbose($"ModSettings.Old: returns true (line 54) - inputs (c={c})"); return true; }
    }

    /// The number the player had in an entry that was renamed (still in the file as an orphan before DropRemoved deletes it): the first of the
    /// given (section, key, section, key, ...) pairs that holds a number; the fallback when there is none.
    private static float OldFloat(ConfigFile c, float fallback, params string[] sectionAndKey)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"ModSettings.OldFloat(c={c}, fallback={fallback})");
        System.Reflection.PropertyInfo orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var dict = orphans != null ? orphans.GetValue(c, null) as System.Collections.IDictionary : null;
        for (int i = 0; dict != null && i + 1 < sectionAndKey.Length; i += 2)
        {
            var def = new ConfigDefinition(sectionAndKey[i], sectionAndKey[i + 1]);
            float f;
            if (dict.Contains(def) && float.TryParse((dict[def] as string ?? "").Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f))
            {
                { var returnedAt70 = f; if (RuleLog.IsOn()) ModLog.Verbose($"ModSettings.OldFloat: returns {returnedAt70} (line 70)"); return returnedAt70; }
            }
        }

        { var returnedAt74 = fallback; if (RuleLog.IsOn()) ModLog.Verbose($"ModSettings.OldFloat: returns {returnedAt74} (line 74)"); return returnedAt74; }
    }

    public static void Bind(ConfigFile c)
    {
        EnableMod = c.Bind("General", "Enable Mod", Old(c, "General", "Enable mod", "General", "EnableAll"), ConfigOrder.First("Master switch. Off = CargoToGo does nothing new (no packing, repacking, key hints or tab); cargos you already carry stay safe and can still be unpacked."));

        PullFromContainers = c.Bind("General", "Enable Containers", Old(c, "Containers", "Enable"), "Allow packing materials from nearby chests.");
        ContainerRange = c.Bind("Containers", "Range", 20f, "Radius (m) around the player to search for chests when packing a cargo. 0 turns CargoToGo's own search off (combine with UseAzuCraftyBoxes to rely only on AzuCraftyBoxes' range).");
        UseAzuCraftyBoxes = c.Bind("Containers", "UseAzuCraftyBoxes", true, "If AzuCraftyBoxes is installed, also pack from the containers it reaches: its own Container Range, chest toggles, item include/exclude rules, 'leave one', drawers and bags. Works even when Range is 0. Needs Containers Enable.");
        ContainersFirst = c.Bind("Containers", "ContainersFirst", true, "Take materials from chests before the player's inventory.");

        PackInWorld = c.Bind("General", "Enable Pack In World", Old(c, "PackInWorld", "Enable"), "Ctrl+Alt + left click with a piece selected (in the world) packs a cargo instead of placing the piece.");
        Repack = c.Bind("General", "Enable Repack", Old(c, "Repack", "Enable"), "ALT + destroy turns a piece that was built from a cargo back into a cargo.");
        AltDestroyAnyPiece = c.Bind("Repack", "AnyPiece", false, "ALT + destroy returns a cargo even for pieces that were not built from a cargo.");
        PackEffects = c.Bind("General", "Enable Effects", Old(c, "Effects", "Enable"), "Play build dust / sound / the piece's break effect when a cargo is packed.");
        ShowKeyHints = c.Bind("General", "Enable Key Hints", Old(c, "KeyHints", "Enable"), "Show the Pack cargo / Repack shortcuts in the build-mode key hint bar (keyboard only).");
        ShowCargoTab = c.Bind("General", "Enable Cargo Tab", Old(c, "CargoTab", "Enable"), "Add a 5th \"Cargos\" tab to the build menu that lists the pieces you currently hold a cargo for. Needs a game restart.");
        // last in [General] (user 2026-11-17): bound after every switch, and the F1 order tag puts it at the bottom
        WeightMultiplier = c.Bind("General", "Weight multiplier", OldFloat(c, 1f, "General", "Weight multiplier", "General", "WeightMultiplier"), ConfigOrder.Last("Cargo weight = contents weight x this. Needs a game restart."));

        DebugLog = c.Bind("DebugLog", "Enable", false, "Also write DEBUG lines (blocked actions, tagging, ...) to the BepInEx log.");
        EnableMod.SettingChanged += (s, e) => ModLog.Info("Config: Enable Mod = " + EnableMod.Value + (EnableMod.Value ? ", the mod is on" : ", the mod is inert"));
    }
}
