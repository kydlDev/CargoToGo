using Kydl.Common;
using System;
using HarmonyLib;
using UnityEngine;

namespace CargoToGo
{
    // ---------------------------------------------------------------- registration
    [HarmonyPatch(typeof(ObjectDB), "Awake"), HarmonyPriority(Priority.Last)]
    internal static class ObjectDB_Awake { private static void Postfix(ObjectDB __instance) { try { CargoFactory.Ensure(__instance); } catch (Exception e) { ModLog.Error("ObjectDB.Awake hook failed" + " | " + e); } } }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB)), HarmonyPriority(Priority.Last)]
    internal static class ObjectDB_Copy { private static void Postfix(ObjectDB __instance) { try { CargoFactory.Ensure(__instance); } catch (Exception e) { ModLog.Error("ObjectDB.CopyOtherDB hook failed" + " | " + e); } } }

    // Catches pieces that other mods add to piece tables after ObjectDB init (not saved inventories though).
    [HarmonyPatch(typeof(ZNetScene), "Awake"), HarmonyPriority(Priority.Last)]
    internal static class ZNetScene_Awake { private static void Postfix() { try { CargoFactory.Ensure(ObjectDB.instance); } catch (Exception e) { ModLog.Error("ZNetScene.Awake hook failed" + " | " + e); } } }

    // ---------------------------------------------------------------- building with cargo
    internal static class PlaceState
    {
        public static Piece Piece;
        public static ItemDrop.ItemData Cargo;
        public static void Clear() { Piece = null; Cargo = null; }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    internal static class Player_TryPlacePiece
    {
        private static bool Prefix(Player __instance, Piece piece, ref bool __result)
        {
            try
            {
                PlaceState.Clear();
                if (__instance != Player.m_localPlayer || piece == null) return true;

                if (ModSettings.On && ModSettings.PackInWorld.Value && Packer.PackChord)
                {
                    Packer.TryPack(__instance, piece);
                    __result = false;           // never place while Ctrl+Alt is held
                    return false;
                }
                if (!ModSettings.On || __instance.NoCostCheat()) return true;
                var cargo = CargoFactory.FindCargoFor(__instance.GetInventory(), piece);
                if (cargo != null)
                {
                    PlaceState.Piece = piece; PlaceState.Cargo = cargo;
                    ModLog.Verbose("Placing '" + Utils.GetPrefabName(piece.gameObject) + "' with a cargo.");
                }
            }
            catch (Exception e) { ModLog.Error("TryPlacePiece hook failed" + " | " + e); }
            return true;
        }
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    internal static class Player_UpdatePlacement { private static void Postfix() { PlaceState.Clear(); } }

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    internal static class Player_ConsumeResources
    {
        private static bool Prefix(Player __instance, Piece.Requirement[] requirements)
        {
            try
            {
                if (PlaceState.Cargo == null || PlaceState.Piece == null) return true;
                if (!ReferenceEquals(requirements, PlaceState.Piece.m_resources)) return true;
                var cargo = PlaceState.Cargo;
                PlaceState.Cargo = null;
                var removed = __instance.GetInventory().RemoveItem(cargo);
                if (removed) ModLog.Info("Cargo used: '" + Utils.GetPrefabName(PlaceState.Piece.gameObject) + "' built from a cargo by " + Packer.PName(__instance) + ".");
                else ModLog.Warn("Cargo for '" + Utils.GetPrefabName(PlaceState.Piece.gameObject) + "' could not be removed from the inventory; falling back to normal material use.");
                return !removed; // consumed the cargo -> skip normal material use
            }
            catch (Exception e) { ModLog.Error("ConsumeResources hook failed" + " | " + e); return true; }
        }
    }

    // Mark the freshly placed piece as "built from cargo".
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    internal static class Piece_SetCreator
    {
        private static void Postfix(Piece __instance)
        {
            try
            {
                if (PlaceState.Cargo == null || PlaceState.Piece == null) return;
                if (Utils.GetPrefabName(__instance.gameObject) != Utils.GetPrefabName(PlaceState.Piece.gameObject)) return;
                var nv = __instance.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid()) { nv.GetZDO().Set(Packer.BuiltFromCargoKey, true); ModLog.Verbose("Tagged placed '" + __instance.name + "' as built from cargo."); }
            }
            catch (Exception e) { ModLog.Error("SetCreator hook failed" + " | " + e); }
        }
    }

    // Having a cargo (or ALT-packing from chests) counts as "can build" for the ghost / placement check.
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
    internal static class Player_HaveRequirements
    {
        private static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            try
            {
                if (__result || !ModSettings.On || mode == Player.RequirementMode.IsKnown || piece == null || __instance != Player.m_localPlayer) return;

                var ok = CargoFactory.FindCargoFor(__instance.GetInventory(), piece) != null;
                if (!ok && ModSettings.PackInWorld.Value && Packer.PackChord && piece == __instance.GetSelectedPiece())
                    ok = Packer.CanPack(__instance, piece);
                if (!ok) return;

                if (piece.m_craftingStation != null &&
                    !CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, __instance.transform.position)) return;
                if (!string.IsNullOrEmpty(piece.m_dlc) && !DLCMan.instance.IsDLCInstalled(piece.m_dlc)) return;
                __result = true;
            }
            catch (Exception e) { ModLog.Error("HaveRequirements hook failed" + " | " + e); }
        }
    }

    // ---------------------------------------------------------------- Ctrl+Alt + left click in the hammer menu
    [HarmonyPatch(typeof(Hud), "OnLeftClickPiece")]
    internal static class Hud_OnLeftClickPiece
    {
        private static void Postfix()
        {
            try
            {
                var p = Player.m_localPlayer;
                if (p == null || !ModSettings.On || !ModSettings.PackInWorld.Value || !Packer.PackChord) return;
                var piece = p.GetSelectedPiece();
                if (piece != null) Packer.TryPack(p, piece);
            }
            catch (Exception e) { ModLog.Error("Hud.OnLeftClickPiece hook failed" + " | " + e); }
        }
    }

    // ---------------------------------------------------------------- ALT + destroy -> back into a cargo
    [HarmonyPatch(typeof(Player), "RemovePiece")]
    internal static class Player_RemovePiece
    {
        public static Piece Pending;
        public static float PendingTime;

        private static bool Prefix(Player __instance)
        {
            Pending = null;
            try
            {
                if (!ModSettings.On || !ModSettings.Repack.Value || __instance != Player.m_localPlayer || !Packer.Alt) return true;
                var piece = __instance.GetHoveringPiece();
                if (piece == null) return true;
                if (!Packer.IsBuiltFromCargo(piece) && !ModSettings.AltDestroyAnyPiece.Value) return true;
                if (CargoFactory.GetCargoPrefab(piece) == null) { ModLog.Warn("ALT-destroy of '" + Utils.GetPrefabName(piece.gameObject) + "': no cargo prefab exists, using normal destroy."); return true; }
                if (!__instance.GetInventory().HaveEmptySlot())
                {
                    __instance.Message(MessageHud.MessageType.Center, Loc.T("cg_no_slot"));
                    ModLog.Info("ALT-destroy of '" + Utils.GetPrefabName(piece.gameObject) + "' blocked for " + Packer.PName(__instance) + ": no free inventory slot.");
                    return false;
                }
                var nv = piece.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) return true;
                nv.ClaimOwnership();            // so the removal runs on this machine
                Pending = piece;
                PendingTime = Time.time;
            }
            catch (Exception e) { ModLog.Error("RemovePiece hook failed" + " | " + e); Pending = null; }
            return true;
        }

        private static void Postfix(bool __result) { if (!__result) Pending = null; }
    }

    [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
    internal static class Piece_DropResources
    {
        private static bool Prefix(Piece __instance)
        {
            try
            {
                var pending = Player_RemovePiece.Pending;
                if (pending == null || !ReferenceEquals(pending, __instance)) return true;
                Player_RemovePiece.Pending = null;
                var pieceName = Utils.GetPrefabName(__instance.gameObject);
                if (Packer.FreeKeyActive(__instance)) { ModLog.Verbose("Repack of '" + pieceName + "' skipped: free-build world key is active, vanilla drops nothing."); return true; }
                if (Time.time - Player_RemovePiece.PendingTime > 1.5f)
                {
                    ModLog.Warn("Repack of '" + pieceName + "' timed out (removal took >1.5s); normal drops used.");
                    return true;
                }

                var p = Player.m_localPlayer;
                var prefab = CargoFactory.GetCargoPrefab(__instance);
                if (p == null || prefab == null) return true;
                var cargo = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
                cargo.m_dropPrefab = prefab;
                cargo.m_stack = 1;
                cargo.m_quality = 1;
                if (!p.GetInventory().AddItem(cargo))
                {
                    ModLog.Warn("Repack of '" + pieceName + "' by " + Packer.PName(p) + ": AddItem failed, normal material drops used.");
                    return true;   // no room: fall back to normal drops
                }
                p.Message(MessageHud.MessageType.Center, Loc.T("cg_repacked") + " " + Localization.instance.Localize(__instance.m_name));
                ModLog.Info("Cargo repacked from piece '" + pieceName + "' by " + Packer.PName(p) + " (normal drops skipped).");
                return false;      // skip the normal material drops
            }
            catch (Exception e) { ModLog.Error("Piece.DropResources hook failed" + " | " + e); return true; }
        }
    }

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
            if (!CargoFactory.IsCargo(item)) return true;
            var me = Player.m_localPlayer;
            var foreign = me != null && inventory != null && inventory != me.GetInventory();   // e.g. dragged out of a gravestone
            Rules.Warn(Loc.T(foreign ? "cg_cant_drop_foreign" : "cg_cant_drop"));
            __result = false;
            return false;
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
                if (!CargoFactory.IsCargo(item) || !Packer.Ctrl || !Packer.Alt) return true; // normal path -> UseItem is blocked above
                var me = Player.m_localPlayer;
                if (me == null) return false;
                if (grid.GetInventory() != me.GetInventory()) { Rules.Warn(Loc.T("cg_unpack_own")); return false; }
                Packer.Unpack(me, item);
                return false;
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

    // ---------------------------------------------------------------- build-mode key hint bar
    [HarmonyPatch(typeof(KeyHints), "UpdateHints")]
    internal static class KeyHints_UpdateHints
    {
        private static void Postfix(KeyHints __instance) { KeyHintUi.Tick(__instance); }
    }
}
