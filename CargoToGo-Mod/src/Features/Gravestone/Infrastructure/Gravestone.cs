using System;
using CargoToGo.Features.Packing.Infrastructure;
using KydlCore.Common;
using CargoToGo.Core;

namespace CargoToGo.Features.Gravestone.Infrastructure;

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

/// <summary>The state and decisions behind the gravestone patches (src/Integrations/Harmony/GravestonePatches.cs): while the game itself moves items
/// the rule "a cargo stays in your inventory" is off (Rules.AllowDepth), and one log line per death that involved cargos.</summary>
internal static class GravestoneTracker
{
    internal static bool Active;
    internal static int Before;     // cargos in the inventory when the player died
    internal static int InGrave;    // cargos in the freshly created gravestone, -1 = no gravestone was set up

    /// <summary>Expected outcome: the number of cargo items in the inventory (0 for null).</summary>
    internal static int CountCargos(Inventory inv)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"GravestoneTracker.CountCargos(inv={inv})");
        var n = 0;
        if (inv == null) { if (RuleLog.IsOn()) ModLog.Verbose($"GravestoneTracker.CountCargos: returns 0 (line 46) - inputs (inv={inv})"); return 0; }
        foreach (var it in inv.GetAllItems()) if (CargoFactory.IsCargo(it)) n++;
        { var returnedAt48 = n; if (RuleLog.IsOn()) ModLog.Verbose($"GravestoneTracker.CountCargos: returns {returnedAt48} (line 48)"); return returnedAt48; }
    }

    /// <summary>Called before Player.CreateTombStone. Expected outcome: true (the caller hands it to <see cref="End"/>; the rule is switched off until then).</summary>
    internal static bool Begin(Player player)
    {
        Rules.AllowDepth++;
        Active = true;
        InGrave = -1;
        Before = 0;
        try { Before = CountCargos(player.GetInventory()); }
        catch (Exception e) { ModLog.Error("CreateTombStone hook (before) failed" + " | " + e); }
        return true;
    }

    /// <summary>Called after Player.CreateTombStone, also when it threw. Expected outcome: the rule is back on and one line says how many cargos made it to the grave.</summary>
    internal static void End(Player player, bool began)
    {
        if (began) Rules.AllowDepth--;
        try
        {
            if (!Active) { if (RuleLog.IsOn()) ModLog.Verbose($"GravestoneTracker.End: returns (line 69) - inputs (player={player}, began={began})"); return; }
            Active = false;
            if (Before == 0 && InGrave <= 0) { if (RuleLog.IsOn()) ModLog.Verbose($"GravestoneTracker.End: returns (line 71) - inputs (player={player}, began={began})"); return; }   // no cargos involved
            var left = CountCargos(player.GetInventory());
            var who = Packer.PName(player);
            if (left == 0 && InGrave == Before)
                ModLog.Info("Death of " + who + ": " + Before + " cargo(s) moved to the gravestone.");
            else
                ModLog.Warn("Death of " + who + ": " + Before + " cargo(s) in the inventory, " + Math.Max(InGrave, 0) + " in the new gravestone, " + left +
                         " still in the inventory (the world's death rules can delete items; another mod may also handle the gravestone).");
        }
        catch (Exception e) { ModLog.Error("CreateTombStone hook (after) failed" + " | " + e); }
    }

    /// <summary>Called at the end of TombStone.Setup: what the gravestone holds.</summary>
    internal static void SetupDone(TombStone stone)
    {
        try
        {
            if (!Active) { if (RuleLog.IsOn()) ModLog.Verbose($"GravestoneTracker.SetupDone: returns (line 88) - inputs (stone={stone})"); return; }
            var container = stone.GetComponent<Container>();
            var inv = container != null ? container.GetInventory() : null;
            InGrave = CountCargos(inv);
        }
        catch (Exception e) { ModLog.Error("TombStone.Setup hook failed" + " | " + e); }
    }
}
