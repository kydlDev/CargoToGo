using System;
using System.Linq;
using KydlCore.Features.Diagnostics.Contracts;
using CargoToGo.Features.Packing.Infrastructure;
using HarmonyLib;
using KydlCore.Common;
using CargoToGo.Core;

namespace CargoToGo.Integrations.Harmony;

// MIGRATION_PLAN A5: every Harmony patch lives in src/Integrations/Harmony. A cargo stays in the player's own inventory; Ctrl+Alt + right click unpacks it; portal rules (Rules in Features/Packing).
[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
internal static class Inv_Add1
{ private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result) { if (!Rules.Blocked(__instance, item)) return true; __result = false; return false; } }

[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData), typeof(Vector2i) })]
internal static class Inv_Add2
{ private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result) { if (!Rules.Blocked(__instance, item)) return true; __result = false; return false; } }

[HarmonyPatch(typeof(Inventory), "AddItem", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) })]
internal static class Inv_Add3
{ private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result) { if (!Rules.Blocked(__instance, item)) return true; __result = false; return false; } }

[HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), new[] { typeof(Inventory), typeof(ItemDrop.ItemData) })]
internal static class Inv_Move1
{ private static bool Prefix(Inventory __instance, ItemDrop.ItemData item) { if (!Rules.Blocked(__instance, item)) return true; Rules.Warn(Loc.T("cg_cant_leave")); return false; } }

[HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), new[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) })]
internal static class Inv_Move2
{ private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result) { if (!Rules.Blocked(__instance, item)) return true; Rules.Warn(Loc.T("cg_cant_leave")); __result = false; return false; } }

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
internal static class Grid_Drop
{ private static bool Prefix(InventoryGrid __instance, ItemDrop.ItemData item, ref bool __result) { if (!Rules.Blocked(__instance.GetInventory(), item)) return true; Rules.Warn(Loc.T("cg_cant_leave")); __result = false; return false; } }

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem))]
internal static class Hum_Drop
{
    private static bool Prefix(Inventory inventory, ItemDrop.ItemData item, ref bool __result)
    {
        if (RuleLog.IsOn()) ModLog.Every("Hum_Drop.Prefix", 2d, $"Hum_Drop.Prefix(inventory={inventory}, item={item}, __result={__result})");
        if (!CargoFactory.IsCargo(item)) { if (RuleLog.IsOn()) ModLog.Every("Hum_Drop.Prefix:40", 2d, $"Hum_Drop.Prefix: returns true (line 40) - inputs (inventory={inventory}, item={item}, __result={__result})"); return true; }
        var me = Player.m_localPlayer;
        var foreign = me != null && inventory != null && inventory != me.GetInventory();   // e.g. dragged out of a gravestone
        // the mod takes over what the player may do with a cargo: the flight recorder keeps where they were and what they carried (PROJECT_RULES 28)
        FlightRecorder.Track(me, "cargo carrier", () => me != null ? "cargos " + me.GetInventory().GetAllItems().Count(CargoFactory.IsCargo) + ", last veto " + (foreign ? "foreign drop" : "drop") : "player gone");
        Rules.Warn(Loc.T(foreign ? "cg_cant_drop_foreign" : "cg_cant_drop"));
        __result = false;
        { if (RuleLog.IsOn()) ModLog.Every("Hum_Drop.Prefix:45", 2d, $"Hum_Drop.Prefix: returns false (line 45) - inputs (inventory={inventory}, item={item}, __result={__result})"); return false; }
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
internal static class Hum_Use
{ private static bool Prefix(ItemDrop.ItemData item) { if (!CargoFactory.IsCargo(item)) return true; Rules.Warn(Loc.T("cg_use_only_build")); return false; } }

// ---------------------------------------------------------------- Ctrl+Alt+Right-click destroys a cargo (materials come back)
[HarmonyPatch(typeof(InventoryGui), "OnRightClickItem")]
internal static class Gui_RightClick
{
    private static bool Prefix(InventoryGrid grid, ItemDrop.ItemData item)
    {
        try
        {
            if (!CargoFactory.IsCargo(item) || !Packer.Ctrl || !Packer.Alt) { if (RuleLog.IsOn()) ModLog.Every("Gui_RightClick.Prefix:61", 2d, $"Gui_RightClick.Prefix: returns true (line 61) - inputs (grid={grid}, item={item})"); return true; } // normal path -> UseItem is blocked above
            var me = Player.m_localPlayer;
            if (me == null) { if (RuleLog.IsOn()) ModLog.Every("Gui_RightClick.Prefix:63", 2d, $"Gui_RightClick.Prefix: returns false (line 63) - inputs (grid={grid}, item={item})"); return false; }
            if (grid.GetInventory() != me.GetInventory()) { Rules.Warn(Loc.T("cg_unpack_own")); { if (RuleLog.IsOn()) ModLog.Every("Gui_RightClick.Prefix:64", 2d, $"Gui_RightClick.Prefix: returns false (line 64) - inputs (grid={grid}, item={item})"); return false; } }
            Packer.Unpack(me, item);
            { if (RuleLog.IsOn()) ModLog.Every("Gui_RightClick.Prefix:66", 2d, $"Gui_RightClick.Prefix: returns false (line 66) - inputs (grid={grid}, item={item})"); return false; }
        }
        catch (Exception e) { ModLog.Error("OnRightClickItem hook failed" + " | " + e); return true; }
    }
}

// ---------------------------------------------------------------- portal rules
// Vanilla Inventory.IsTeleportable(allowAllItems) already applies everything the world / portal decides:
// TeleportAll world modifier and the portal's own allow-all flag skip the item check, otherwise every item's
// m_teleportable must be true; NoPortals / NoBossPortals are handled in TeleportWorld.Teleport.
// So we deliberately do NOT override its result. We only make sure each cargo's m_teleportable reflects the
// materials inside it at this moment, so cargos holding ores / ingots are blocked exactly like the loose items.
[HarmonyPatch(typeof(Inventory), "IsTeleportable")]
internal static class Inv_IsTeleportable
{
    private static void Prefix(Inventory __instance)
    {
        try
        {
            foreach (var it in __instance.GetAllItems())
                if (CargoFactory.IsCargo(it)) CargoFactory.RefreshTeleportable(it);
        }
        catch (Exception e) { ModLog.Error("Inventory.IsTeleportable hook failed" + " | " + e); }
    }
}
