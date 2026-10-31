using BepInEx.Configuration;
using Kydl.Common;

namespace CargoToGo
{
    /// <summary>The player config (PROJECT_RULES 11): [General] EnableAll switches the whole mod, every functionality has its own
    /// [Section] Enable, [DebugLog] Enable adds detail lines to the log. All entries also show on the mod's Settings tab in the KY hub.</summary>
    internal static class ModSettings
    {
        public static ConfigEntry<bool> EnableAll;
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

        /// <summary>Is the mod switched on ([General] EnableAll)? Off = inert: no packing, no repacking, no hints, no tab.
        /// Cargos you already hold stay safe (they can still be unpacked, and still cannot be lost into a chest or the ground).</summary>
        public static bool On { get { return EnableAll == null || EnableAll.Value; } }

        public static bool Verbose { get { return DebugLog != null && DebugLog.Value; } }

        public static void Bind(ConfigFile c)
        {
            EnableAll = c.Bind("General", "EnableAll", true, "Master switch. Off = CargoToGo does nothing new (no packing, repacking, key hints or tab); cargos you already carry stay safe and can still be unpacked.");
            WeightMultiplier = c.Bind("General", "WeightMultiplier", 1f, "Cargo weight = contents weight x this. Needs a game restart.");

            PullFromContainers = c.Bind("Containers", "Enable", true, "Allow packing materials from nearby chests.");
            ContainerRange = c.Bind("Containers", "Range", 20f, "Radius (m) around the player to search for chests when packing a cargo. 0 turns CargoToGo's own search off (combine with UseAzuCraftyBoxes to rely only on AzuCraftyBoxes' range).");
            UseAzuCraftyBoxes = c.Bind("Containers", "UseAzuCraftyBoxes", true, "If AzuCraftyBoxes is installed, also pack from the containers it reaches: its own Container Range, chest toggles, item include/exclude rules, 'leave one', drawers and bags. Works even when Range is 0. Needs Containers Enable.");
            ContainersFirst = c.Bind("Containers", "ContainersFirst", true, "Take materials from chests before the player's inventory.");

            PackInWorld = c.Bind("PackInWorld", "Enable", true, "Ctrl+Alt + left click with a piece selected (in the world) packs a cargo instead of placing the piece.");
            Repack = c.Bind("Repack", "Enable", true, "ALT + destroy turns a piece that was built from a cargo back into a cargo.");
            AltDestroyAnyPiece = c.Bind("Repack", "AnyPiece", false, "ALT + destroy returns a cargo even for pieces that were not built from a cargo.");
            PackEffects = c.Bind("Effects", "Enable", true, "Play build dust / sound / the piece's break effect when a cargo is packed.");
            ShowKeyHints = c.Bind("KeyHints", "Enable", true, "Show the Pack cargo / Repack shortcuts in the build-mode key hint bar (keyboard only).");
            ShowCargoTab = c.Bind("CargoTab", "Enable", true, "Add a 5th \"Cargos\" tab to the build menu that lists the pieces you currently hold a cargo for. Needs a game restart.");

            DebugLog = c.Bind("DebugLog", "Enable", false, "Also write DEBUG lines (blocked actions, tagging, ...) to the BepInEx log.");
            EnableAll.SettingChanged += (s, e) => ModLog.Info("Config: EnableAll = " + EnableAll.Value + (EnableAll.Value ? ", the mod is on" : ", the mod is inert"));
        }
    }
}
