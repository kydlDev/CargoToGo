using Kydl.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace CargoToGo
{
    // ====================================================================================================
    // Cargos and death.
    //
    // What vanilla does (checked in the game's IL):
    //   Player.OnDeath -> Player.CreateTombStone -> Inventory.MoveInventoryToGrave(playerInventory).
    //   MoveInventoryToGrave puts every unequipped, non-quest item straight into the gravestone's item list
    //   (List.Add, it never calls AddItem) and removes them from the player. A cargo is an ordinary unequipped
    //   item, so it travels to the gravestone like everything else.
    //   The gravestone's Container then saves its items into the ZDO, and every time that container is loaded
    //   again (walking back to the grave after the zone was unloaded, relogging, ownership changing hands, ...)
    //   Container.Load -> Inventory.Load -> Inventory.AddItem(prefabHash, ...) -> the private
    //   Inventory.AddItem(item, amount, x, y, skipValidPositionCheck).
    //   That private AddItem is exactly what the "a cargo may only enter the player's own inventory" rule
    //   (Rules.Blocked, Inv_Add3) refuses for every other inventory. So the cargo was dropped from the grave as
    //   soon as it was loaded, and the next save of the grave erased it for good: "cargos are destroyed on death".
    //
    // The fix: the rule is for things the PLAYER does. While the game itself is restoring saved items
    // (Inventory.Load) or building the gravestone (Player.CreateTombStone) the rule is switched off
    // (Rules.AllowDepth). Everything the player can do stays as before:
    //   - "Take all" / moving the cargo over: allowed, the target is the player's own inventory.
    //   - dropping it on the ground, moving it into another container (or around inside the grave), using it,
    //     unpacking it, building from it while it sits in the grave: blocked (Hum_Drop, Grid_Drop, Inv_Move*,
    //     Hum_Use, Gui_RightClick, and FindCargoFor only ever looks at the player's own inventory).
    // ====================================================================================================

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
        public static bool Active;
        public static int Before;     // cargos in the inventory when the player died
        public static int InGrave;    // cargos in the freshly created gravestone, -1 = no gravestone was set up

        public static int CountCargos(Inventory inv)
        {
            var n = 0;
            if (inv == null) return 0;
            foreach (var it in inv.GetAllItems()) if (CargoFactory.IsCargo(it)) n++;
            return n;
        }

        private static void Prefix(Player __instance, out bool __state)
        {
            Rules.AllowDepth++;
            __state = true;
            Active = true;
            InGrave = -1;
            Before = 0;
            try { Before = CountCargos(__instance.GetInventory()); }
            catch (Exception e) { ModLog.Error("CreateTombStone hook (before) failed" + " | " + e); }
        }

        private static void Finalizer(Player __instance, bool __state)
        {
            if (__state) Rules.AllowDepth--;
            try
            {
                if (!Active) return;
                Active = false;
                if (Before == 0 && InGrave <= 0) return;   // no cargos involved
                var left = CountCargos(__instance.GetInventory());
                var who = Packer.PName(__instance);
                if (left == 0 && InGrave == Before)
                    ModLog.Info("Death of " + who + ": " + Before + " cargo(s) moved to the gravestone.");
                else
                    ModLog.Warn("Death of " + who + ": " + Before + " cargo(s) in the inventory, " + Math.Max(InGrave, 0) + " in the new gravestone, " + left +
                             " still in the inventory (the world's death rules can delete items; another mod may also handle the gravestone).");
            }
            catch (Exception e) { ModLog.Error("CreateTombStone hook (after) failed" + " | " + e); }
        }
    }

    /// <summary>Called at the end of CreateTombStone, after the items were moved: tells the hook above what the gravestone holds.</summary>
    [HarmonyPatch(typeof(TombStone), nameof(TombStone.Setup))]
    internal static class TombStone_Setup
    {
        private static void Postfix(TombStone __instance)
        {
            try
            {
                if (!Player_CreateTombStone.Active) return;
                var container = __instance.GetComponent<Container>();
                var inv = container != null ? container.GetInventory() : null;
                Player_CreateTombStone.InGrave = Player_CreateTombStone.CountCargos(inv);
            }
            catch (Exception e) { ModLog.Error("TombStone.Setup hook failed" + " | " + e); }
        }
    }
}
