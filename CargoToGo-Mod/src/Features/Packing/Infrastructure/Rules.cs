using KydlCore.Common;
using CargoToGo.Core;

namespace CargoToGo.Features.Packing.Infrastructure;

// ---------------------------------------------------------------- cargo stays in the player's inventory
internal static class Rules
{
    /// <summary>&gt; 0 while the GAME (not the player) is putting items into an inventory: restoring saved data
    /// (Inventory.Load, e.g. a gravestone container loading its items) or building a gravestone on death.
    /// The "cargo stays in your inventory" rule is for player actions only, so it is off in those moments (see Gravestone.cs).</summary>
    public static int AllowDepth;

    public static bool Blocked(Inventory target, ItemDrop.ItemData item)
    {
        if (RuleLog.IsOn()) ModLog.Verbose($"Rules.Blocked(target={target}, item={item})");
        if (AllowDepth > 0) { if (RuleLog.IsOn()) ModLog.Verbose($"Rules.Blocked: returns false (line 17) - inputs (target={target}, item={item})"); return false; }
        if (!CargoFactory.IsCargo(item)) { if (RuleLog.IsOn()) ModLog.Verbose($"Rules.Blocked: returns false (line 18) - inputs (target={target}, item={item})"); return false; }
        var me = Player.m_localPlayer;
        { var returnedAt20 = me != null && target != me.GetInventory(); if (RuleLog.IsOn()) ModLog.Verbose($"Rules.Blocked: returns {returnedAt20} (line 20)"); return returnedAt20; }
    }
    public static void Warn(string text)
    {
        if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, text);
        ModLog.Verbose("Blocked action: " + text);
    }
}
