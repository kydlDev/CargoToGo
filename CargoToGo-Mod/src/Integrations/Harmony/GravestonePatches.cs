using Kydl.Common;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace CargoToGo
{
    // MIGRATION_DECISIONS A5: the gravestone hooks of feature Gravestone; the state and decisions are in GravestoneTracker (why: see the comment there).

    /// <summary>Restoring saved items is not a player action. The game has two Inventory.Load overloads; both are covered
    /// (looked up by name so a changed signature in a future game version does not silently break the patch).</summary>
    [HarmonyPatch]
    internal static class Inv_Load_Allow
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Inventory)).Where(m => m.Name == "Load").Cast<MethodBase>();
        }

        private static bool Prepare()
        {
            if (TargetMethods().Any()) return true;
            ModLog.Error("Inventory.Load not found: cargos inside a gravestone can be lost when the gravestone reloads. Game version incompatible?");
            return false;
        }

        private static void Prefix(out bool __state) { Rules.AllowDepth++; __state = true; }

        // Finalizer, not Postfix: runs even if Load throws, so the rule can never stay switched off.
        private static void Finalizer(bool __state) { if (__state) Rules.AllowDepth--; }
    }

    /// <summary>Building the gravestone is the game's doing as well. Vanilla moves the items without AddItem, but anything that
    /// replaces that step (another mod) is covered too. Also writes one log line per death that involved cargos.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
    internal static class Player_CreateTombStone
    {
        private static void Prefix(Player __instance, out bool __state) { __state = GravestoneTracker.Begin(__instance); }

        private static void Finalizer(Player __instance, bool __state) { GravestoneTracker.End(__instance, __state); }
    }

    /// <summary>Called at the end of CreateTombStone, after the items were moved: tells the tracker what the gravestone holds.</summary>
    [HarmonyPatch(typeof(TombStone), nameof(TombStone.Setup))]
    internal static class TombStone_Setup
    {
        private static void Postfix(TombStone __instance) { GravestoneTracker.SetupDone(__instance); }
    }
}
