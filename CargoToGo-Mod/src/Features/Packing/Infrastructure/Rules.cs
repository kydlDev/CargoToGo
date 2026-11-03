using KydlCore.Common;

namespace CargoToGo.Features.Packing.Infrastructure
{
    // ---------------------------------------------------------------- cargo stays in the player's inventory
    internal static class Rules
    {
        /// <summary>&gt; 0 while the GAME (not the player) is putting items into an inventory: restoring saved data
        /// (Inventory.Load, e.g. a gravestone container loading its items) or building a gravestone on death.
        /// The "cargo stays in your inventory" rule is for player actions only, so it is off in those moments (see Gravestone.cs).</summary>
        public static int AllowDepth;

        public static bool Blocked(Inventory target, ItemDrop.ItemData item)
        {
            if (AllowDepth > 0) return false;
            if (!CargoFactory.IsCargo(item)) return false;
            var me = Player.m_localPlayer;
            return me != null && target != me.GetInventory();
        }
        public static void Warn(string text)
        {
            if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, text);
            ModLog.Verbose("Blocked action: " + text);
        }
    }
}
